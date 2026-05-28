# Desktop: NUR eine Redline-App (EXE), keine doppelte Verknuepfung
$desktop = [Environment]::GetFolderPath("Desktop")
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$deskExe = Join-Path $desktop "Redline Gaming Optimizer.exe"

@(
    "REDLINE_Intro.mp4", "REDLINE_Promo.mp4", "REDLINE.mp4",
    "Redline Gaming Optimizer.lnk", "Redline Gaming Optimizer V9.lnk",
    "Redline UPDATE-TEST (V9.0).lnk", "GamingBooster_Pro.exe"
) | ForEach-Object {
    $p = Join-Path $desktop $_
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "Geloescht: $_" }
}

Get-ChildItem $desktop -Filter "*.mp4" -File -EA SilentlyContinue |
    Where-Object { $_.Name -match 'REDLINE|Redline|GamingBooster|Intro|Promo' } |
    Remove-Item -Force

$df = Join-Path $desktop "GamingBooster_Pro"
$repoCsproj = Join-Path $df "GamingBooster_Pro\GamingBooster_Pro.csproj"
if ((Test-Path $df) -and -not (Test-Path $repoCsproj)) {
    Remove-Item $df -Recurse -Force
    Write-Host "Ordner-Kopie geloescht: $df"
}

$src = Join-Path $root "publish\win-x64\GamingBooster_Pro.exe"
if (-not (Test-Path $src)) {
    Write-Host "Publish fehlt: $src"
    exit 1
}

Copy-Item $src $deskExe -Force
Write-Host "OK: eine App auf Desktop -> $deskExe"
