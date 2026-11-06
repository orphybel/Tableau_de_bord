# Lanceur — tableau de bord d'applications pour Windows

Un seul fichier : `Lanceur.exe` (Windows 10/11, 64 bits). Rien à installer, pas besoin du runtime .NET.

## Télécharger

Le programme prêt à l'emploi est dans le dossier [`Telecharger`](Telecharger/Lanceur.exe) :

1. Ouvrez [Telecharger/Lanceur.exe](Telecharger/Lanceur.exe).
2. Cliquez sur l'icône **Download raw file** (flèche vers le bas, en haut à droite du fichier).
3. Déplacez le fichier téléchargé où vous voulez, par exemple dans `E:\Lanceur`, puis double-cliquez dessus.

## Utilisation

| Action | Comment |
|---|---|
| Lancer une application | Clic sur sa tuile |
| Ajouter un bouton | Bouton **Ajouter** (Ctrl+N), ou glisser-déposer un `.exe`, un raccourci ou un dossier sur la fenêtre |
| Modifier / supprimer / dupliquer | Clic droit sur la tuile, ou crayon au survol |
| Lancer en administrateur | Clic droit → *Exécuter en tant qu'administrateur*, ou option permanente dans l'éditeur |
| Réordonner | Glisser une tuile sur une autre |
| Changer de groupe | Glisser une tuile sur un groupe de la barre latérale |
| Groupes | **+** à côté de « GROUPES » ; double-clic pour renommer ; clic droit pour monter, descendre ou supprimer |
| Rechercher | Tapez n'importe où ; **Entrée** lance le premier résultat ; **Échap** efface |
| Changer de groupe au clavier | Ctrl+1 … Ctrl+9 |
| Paramètres | Ctrl+, |
| Afficher / masquer de n'importe où | Ctrl+Alt+Espace (modifiable) |
| Épingler au premier plan | Icône punaise en haut à droite |

## Ce qui se paramètre

- **Chaque bouton** : nom, programme (`.exe`, `.lnk`, `.bat`, `.ps1`, dossier, URL), arguments, dossier de démarrage, icône personnalisée (`.ico`, `.png`, `.jpg` ou icône d'un autre `.exe`), couleur, groupe, exécution en administrateur. Les variables d'environnement sont acceptées (`%USERPROFILE%\…`).
- **Apparence** : titre, thème (sombre / clair / comme Windows), couleur d'accentuation (préréglages ou `#RRGGBB`), taille des tuiles.
- **Comportement** : toujours au premier plan, masquer après un lancement, icône dans la zone de notification (avec un menu de toutes vos applications), fermeture vers la zone de notification, démarrage avec Windows (éventuellement réduit), raccourci clavier global.
- **Données** : export / import de la configuration.

## Où est stockée la configuration

- Par défaut : `%APPDATA%\Lanceur\config.json` (JSON lisible, modifiable à la main lorsque l'application est fermée).
- **Mode portable** : si un fichier `lanceur.config.json` se trouve à côté de `Lanceur.exe`, c'est lui qui est utilisé. Pratique sur une clé USB.
- Les erreurs inattendues sont consignées dans `%APPDATA%\Lanceur\erreurs.log`.

## Premier lancement : avertissement Windows

L'exécutable n'est pas signé numériquement (un certificat de signature de code est payant). Windows SmartScreen affichera donc probablement « Windows a protégé votre ordinateur » : cliquez sur **Informations complémentaires**, puis sur **Exécuter quand même**. Cet avertissement disparaît en général après le premier lancement.

## Recompiler depuis les sources

Prérequis : [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
cd src
dotnet publish -c Release -o ../publish
```

Le fichier produit est `publish/Lanceur.exe`.

Structure :

```
src/
  App.xaml(.cs)            démarrage, instance unique, journal d'erreurs
  MainWindow.xaml(.cs)     tableau de bord : groupes, tuiles, recherche, glisser-déposer, zone de notification
  Dialogs/                 éditeur de bouton, paramètres, boîtes de message
  Models/Models.cs         modèle de données (sérialisé en JSON)
  Services/                configuration, icônes (Shell Windows), lancement, thème, raccourci global, démarrage auto
  Themes/Styles.xaml       styles visuels
```
