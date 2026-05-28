# EIN Promo-Build: aufraeumen, 2K aufnehmen, Effekte, NUR eine MP4 auf dem Desktop
param([string]$MusicPath = "", [int]$RecordSeconds = 78)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$desktop = [Environment]::GetFolderPath("Desktop")
$finalDesktop = Join-Path $desktop "REDLINE.mp4"
$scriptsDir = Join-Path $root "scripts"

Write-Host "=== 1/4 Desktop aufraeumen ===" -ForegroundColor Cyan
@(
    "REDLINE_Intro.mp4",
    "REDLINE_Promo.mp4",
    "REDLINE.mp4",
    "Redline Gaming Optimizer.lnk",
    "Redline Gaming Optimizer V9.lnk",
    "Redline UPDATE-TEST (V9.0).lnk",
    "GamingBooster_Pro.exe"
) | ForEach-Object {
    $p = Join-Path $desktop $_
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "Geloescht: $_" }
}

Get-ChildItem $desktop -Filter "*.mp4" -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match 'REDLINE|Redline|GamingBooster|Intro|Promo' } |
    ForEach-Object { Remove-Item $_.FullName -Force; Write-Host "Geloescht MP4: $($_.Name)" }

Get-ChildItem $desktop -Filter "GamingBooster_Pro.exe" -File -Recurse -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Item $_.FullName -Force; Write-Host "Geloescht EXE: $($_.FullName)" }

$deskFolder = Join-Path $desktop "GamingBooster_Pro"
$repoCsproj = Join-Path $deskFolder "GamingBooster_Pro\GamingBooster_Pro.csproj"
if ((Test-Path $deskFolder) -and -not (Test-Path $repoCsproj)) {
    Remove-Item $deskFolder -Recurse -Force
    Write-Host "Geloescht Ordner-Kopie: GamingBooster_Pro"
}

# Eine App auf Desktop (nur EXE, keine zweite Verknuepfung)
$exe = Join-Path $root "publish\win-x64\GamingBooster_Pro.exe"
$deskExe = Join-Path $desktop "Redline Gaming Optimizer.exe"
if (Test-Path $exe) {
    Copy-Item $exe $deskExe -Force
    Write-Host "Desktop-App: $deskExe"
}

Write-Host "=== 2/4 Aufnahme (2K wird beim Export gesetzt) ===" -ForegroundColor Cyan
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptsDir "create-intro-mp4.ps1") `
    -RecordSeconds $RecordSeconds -SecondsPerPage 5200 -SkipDesktopCopy

$raw = Join-Path $root "video\promo_raw.mp4"
$staged = Join-Path $root "video\REDLINE_staged.mp4"
if (-not (Test-Path $raw)) { throw "Rohvideo fehlt: $raw" }

Write-Host "=== 3/4 Effekte (Cinematic 2560x1440) ===" -ForegroundColor Cyan
$music = $MusicPath
if (-not $music) { $music = Join-Path $root "video\bg_music.mp3" }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $scriptsDir "apply-promo-effects.ps1") `
    -InputVideo $raw -OutputVideo $staged -MusicPath $music -DurationSec $RecordSeconds

Write-Host "=== 4/4 Nur eine MP4 auf Desktop ===" -ForegroundColor Cyan
if (Test-Path $finalDesktop) { Remove-Item $finalDesktop -Force }
Copy-Item $staged $finalDesktop -Force
$mb = [math]::Round((Get-Item $finalDesktop).Length / 1MB, 2)
Write-Host ""
Write-Host "FERTIG: $finalDesktop  ($mb MB, 2K)" -ForegroundColor Green
Start-Process $finalDesktop
