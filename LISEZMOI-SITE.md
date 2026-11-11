# Compteur de Coffres – version synchronisée avec ton site

Basé sur **Fortnite Chest Counter 1.4.0** (LED-N, licence MIT, voir `LICENSE`). Il lit les replays Fortnite
(aucune lecture d'écran, aucune modification du jeu) et envoie ton total à
https://compteur-de-coffres.jtzfjnw2rb.workers.dev

## Ce qui a changé
- `SiteSync.cs` (nouveau) : après chaque partie comptée, envoie `{ id, total, games, best }` à `POST /api/sync`
  (le même format que ton ancien compteur : **le Worker n'a pas besoin d'être modifié**).
- Un identifiant `XXXX-XXXX` est créé automatiquement pour chaque compte Epic (stocké dans `config.json`).
  La page `/?id=XXXX-XXXX` s'ouvre toute seule à la première synchro. Menu d'accueil : **Ma page sur le site**.
- Réglages : **Synchro avec le site : oui/non**.
- Les mises à jour GitHub sont désactivées (sinon elles remplaceraient ton exe par l'original sans synchro).
- Si le site est injoignable, un nouvel essai est fait automatiquement après la partie suivante.

## Compiler (je n'ai pas pu compiler ici : pas de SDK .NET dans mon environnement)
**Option A – Windows** : installe le SDK .NET 10 puis double-clique `build.bat` → `dist\ChestCounter.exe`.
**Option B – GitHub** : crée un dépôt, pousse ce dossier ; l'onglet *Actions* produit `ChestCounter.exe`.

## Important
Fortnite doit enregistrer les replays : *Paramètres → Jeu → Rediffusions → Enregistrer : Activé*.
Les coffres sont comptés quelques secondes après la fin de chaque partie (pas pendant).

## Version 1.4.1
- Le total, le nombre de parties, le record **et le détail par type de coffre** (ouverts / détruits) sont envoyés au site.
- Ton **pseudo Epic** est envoyé aussi : le site te l'attribue automatiquement, donc tu apparais **tout de suite au classement**.
- Écran d'accueil du compteur : nouvelle ligne **Site** (état de la synchro + ton identifiant), titre « Compteur de Coffres ».
- Réglages → **Identifiant du site** : pour retrouver un ancien profil du site (ex : 9BFD-CFF3).
- `worker.js` : le bouton **Installer** envoie le `.exe` (renseigne `EXE_URL` en haut du fichier).

## Nouvelle interface + tout mène à ton site
- Plus aucun lien vers GitHub / le projet d'origine dans le compteur : menu **Ouvrir mon site**, **Classement en ligne**, **Guide d'installation (site)**.
- Au démarrage (et depuis l'icône près de l'horloge), c'est **ta page du site** qui s'ouvre.
- Nouvel écran : cadre doré avec l'adresse du site, **gros total en chiffres géants**, onglets et menu redessinés, ligne « Site » (état de la synchro + ID).
- Le rapport local (rapport.html) garde les mêmes infos mais prend les couleurs du site (noir et or).
- Mises à jour GitHub supprimées (elles auraient remplacé ton exe par l'original).
