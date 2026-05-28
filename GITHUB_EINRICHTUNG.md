# Redline auf GitHub online stellen

Repository: **https://github.com/LegendR622/Redline-Gaming-Optimizer**

## 1. Git installieren (falls noch nicht)

Download: https://git-scm.com/download/win

GitHub CLI (optional): https://cli.github.com/

## 2. Repo erstellen

1. Auf GitHub einloggen
2. **New repository** → Name: `Redline-Gaming-Optimizer`
3. **Public** oder Private
4. **Ohne** README (liegt schon lokal)

## 3. Ersten Upload

PowerShell im Ordner `C:\Users\Tobi\Desktop\GamingBooster_Pro`:

```powershell
git init
git add .
git commit -m "Redline Gaming Optimizer V9 - Free Edition mit Auto-Update"
git branch -M main
git remote add origin https://github.com/LegendR622/Redline-Gaming-Optimizer.git
git push -u origin main
```

Bei Login: GitHub Personal Access Token als Passwort nutzen.

## 4. Erstes Release (für Auto-Update)

```powershell
.\scripts\build-release.ps1 -Version 9.0
```

Dann auf GitHub: **Releases** → **Draft a new release**

- Tag: `v9.0`
- Asset hochladen: `dist\Redline_V9.0_win-x64.zip`

`version.json` auf `main` muss dieselbe Version und URL haben (wird vom Build-Skript gesetzt).

## 5. Neues Update veröffentlichen

1. Version in `MainWindow.xaml.cs` → `CurrentAppVersion` erhöhen (z. B. `9.1`)
2. `.\scripts\build-release.ps1 -Version 9.1`
3. Git tag + push:

```powershell
git add .
git commit -m "Release V9.1"
git tag v9.1
git push origin main
git push origin v9.1
```

Mit GitHub Actions (`.github/workflows/release.yml`) wird beim Tag-Push das ZIP automatisch gebaut und `version.json` aktualisiert.

## 6. In der App testen

**Update** in der Sidebar → **UPDATE AUTOMATISCH INSTALLIEREN**

Die App lädt `version.json` von GitHub und installiert das ZIP automatisch.
