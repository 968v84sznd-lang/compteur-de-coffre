using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChestCounter;

/// <summary>
/// Sends the totals of the selected profile to the website (Cloudflare Worker, route POST /api/sync)
/// after each counted match. Same protocol as the old overlay: { id, total, games, best }.
/// The id (XXXX-XXXX) is created once per Epic account and kept in config.json.
/// </summary>
public static class SiteSync
{
    public const string DefaultUrl = "https://compteur-de-coffres.jtzfjnw2rb.workers.dev";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static readonly object Gate = new();
    private static readonly Dictionary<string, string> LastSent = new();
    private static bool _busy;

    /// <summary>Last result, shown on the home screen: null = nothing sent yet.</summary>
    public static string? Status { get; private set; }

    /// <summary>True when the last send succeeded.</summary>
    public static bool Ok { get; private set; }

    public static string BaseUrl(Config c) => (string.IsNullOrWhiteSpace(c.SiteUrl) ? DefaultUrl : c.SiteUrl).TrimEnd('/');

    /// <summary>Id of the current profile on the site, created on demand. Null when there is no profile yet.</summary>
    public static string? CurrentId(Store store, Config config, TrackerData data)
    {
        var profile = Profiles.Current(data, config);
        if (profile == null)
        {
            return null;
        }
        config.SiteIds ??= new Dictionary<string, string>();
        if (!config.SiteIds.TryGetValue(profile.Key, out var id) || string.IsNullOrEmpty(id))
        {
            id = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)) + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(2));
            config.SiteIds[profile.Key] = id;
            try { store.SaveConfig(config); } catch { /* ignore */ }
        }
        return id;
    }

    /// <summary>Epic display name of the current profile: the site uses it as the leaderboard name until the player picks another.</summary>
    private static string? profile_Name(TrackerData data, Config config)
    {
        var p = Profiles.Current(data, config);
        return p != null && !string.IsNullOrWhiteSpace(p.Name) ? p.Name : null;
    }

    /// <summary>Uses an id the player already has on the site (old profile) for the current account.</summary>
    public static bool SetId(Store store, Config config, TrackerData data, string id)
    {
        id = (id ?? "").Trim().ToUpperInvariant();
        var profile = Profiles.Current(data, config);
        if (profile == null || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[0-9A-F]{4}-[0-9A-F]{4}$"))
        {
            return false;
        }
        config.SiteIds ??= new Dictionary<string, string>();
        config.SiteIds[profile.Key] = id;
        try { store.SaveConfig(config); } catch { /* ignore */ }
        lock (Gate) { LastSent.Clear(); }
        return true;
    }

    public static string? PageUrl(Store store, Config config, TrackerData data)
    {
        var id = CurrentId(store, config, data);
        return id == null ? null : BaseUrl(config) + "/?id=" + id;
    }

    /// <summary>Sends the totals if they changed since the last successful send. Never blocks and never throws.</summary>
    public static void Push(Store store, Config config, TrackerData data)
    {
        if (!config.SiteSync)
        {
            return;
        }
        try
        {
            var id = CurrentId(store, config, data);
            if (id == null)
            {
                return;
            }
            var mine = Profiles.View(data, config).Matches;
            var total = mine.Sum(m => m.ChestsOpened);
            var games = mine.Count;
            var best = mine.Count == 0 ? 0 : mine.Max(m => m.ChestsOpened);
            // Per chest type: opened / destroyed (keys = stored French kind names, the site maps them to labels).
            var types = new Dictionary<string, int[]>();
            foreach (var chest in mine.SelectMany(m => m.Chests).Where(x => ChestKinds.IsChest(x.Kind)))
            {
                var kind = ChestKinds.Canonical(chest.Kind);
                if (!types.TryGetValue(kind, out var pair))
                {
                    types[kind] = pair = new int[2];
                }
                pair[chest.Destroyed ? 1 : 0]++;
            }
            var typesBody = types.ToDictionary(kv => kv.Key, kv => new { o = kv.Value[0], d = kv.Value[1] });
            var name = profile_Name(data, config);
            var body = JsonSerializer.Serialize(new { id, total, games, best, name, types = typesBody });
            var signature = body;
            var url = BaseUrl(config) + "/api/sync";

            lock (Gate)
            {
                if (_busy || (LastSent.TryGetValue(id, out var last) && last == signature))
                {
                    return;
                }
                _busy = true;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(body, Encoding.UTF8, "application/json"),
                    };
                    req.Headers.UserAgent.ParseAdd("ChestCounter-Site/" + AppInfo.VersionText);
                    using var res = await Http.SendAsync(req);
                    if (res.IsSuccessStatusCode)
                    {
                        lock (Gate) { LastSent[id] = signature; }
                        Ok = true;
                        Status = L.T($"site synchronisé ({total} coffres)", $"site synced ({total} chests)", $"sitio sincronizado ({total} cofres)");
                        if (!config.SitePageOpened)
                        {
                            config.SitePageOpened = true;
                            try { store.SaveConfig(config); } catch { /* ignore */ }
                            OpenPage(BaseUrl(config) + "/?id=" + id);
                        }
                    }
                    else
                    {
                        Ok = false;
                        Status = L.T($"site : erreur {(int)res.StatusCode}", $"site: error {(int)res.StatusCode}", $"sitio: error {(int)res.StatusCode}");
                    }
                }
                catch
                {
                    // offline: tried again at the next scan, nothing is lost
                    Ok = false;
                    Status = L.T("site injoignable (nouvel essai à la prochaine partie)", "site unreachable (will retry after the next match)", "sitio inaccesible (se reintentará tras la próxima partida)");
                }
                finally
                {
                    lock (Gate) { _busy = false; }
                }
            });
        }
        catch
        {
            // the counter keeps working without the site
        }
    }

    /// <summary>Waits (at most <paramref name="ms"/>) for a send in progress: used by --once, which exits right after.</summary>
    public static void WaitIdle(int ms)
    {
        var until = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < until)
        {
            lock (Gate) { if (!_busy) return; }
            Thread.Sleep(100);
        }
    }

    public static void OpenPage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }
}
