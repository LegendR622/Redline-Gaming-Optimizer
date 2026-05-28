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

Releases: [github.com/LegendR622/Redline-Gaming-Optimizer/releases](https://github.com/LegendR622/Redline-Gaming-Optimizer/releases)

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
.\scripts\build-release.ps1 -Version 9.1
```

Then upload the zip from `dist\` to GitHub Releases and push `version.json` on `main`.

## License

© 2026 Tobias Immisch. All rights reserved.
