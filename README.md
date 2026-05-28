# Redline Gaming Optimizer V9

Premium PC gaming optimizer for Windows (WPF, .NET 10).

- **Free Edition** – full free features, Pro coming soon
- **Auto-Update** – built-in updater via GitHub `version.json`
- **Author** – Tobias Immisch

## Features

- Gaming Optimizer & AI Profiles
- Cleaner, Security, Driver & Network tools
- Autostart manager, Repair tools, BIOS/UEFI check

## Download

**Nur eine Datei:** `Redline_Gaming_Optimizer_Setup_v9.17.exe` (Windows-Installer, erkennt alte Installation und ersetzt sie)

[GitHub Releases](https://github.com/LegendR622/Redline-Gaming-Optimizer/releases) – keine ZIP-Ordner mehr nötig.

## Run locally

```powershell
dotnet publish GamingBooster_Pro\GamingBooster_Pro.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
.\publish\win-x64\GamingBooster_Pro.exe
```

Skip intro: `set REDLINE_SKIP_INTRO=1` or `--nosplash`

## Auto-Update

The app reads `version.json` from this repo on the `main` branch.

In the app: **Update** → **UPDATE AUTOMATISCH INSTALLIEREN**

## Create a new release

```powershell
.\scripts\publish-github-clean.ps1 -Version 9.16
```

Baut Setup-EXE, löscht alte Releases, veröffentlicht nur die Installer-EXE.

## License

© 2026 Tobias Immisch. All rights reserved.
