# Compteur de Coffres

Compte automatiquement les coffres Fortnite ouverts, partie après partie, et synchronise le total avec le site
**https://compteur-de-coffres.jtzfjnw2rb.workers.dev** (stats, classement, guide).

Le compteur lit les fichiers *replay* que Fortnite enregistre sur le PC une fois la partie terminée.
Il ne modifie pas le jeu et n'accède pas à sa mémoire.

- Compiler : `build.bat` (SDK .NET 10) ou l'onglet *Actions* de GitHub → `ChestCounter.exe`
- Détails des changements : `LISEZMOI-SITE.md`

Basé sur *Fortnite Chest Counter* de LED-N (licence MIT, voir `LICENSE` et `lib/LICENSE-FortniteReplayDecompressor`).
