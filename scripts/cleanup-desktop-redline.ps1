# Desktop: NUR eine Redline-App (EXE), keine doppelte Verknuepfung
$desktop = [Environment]::GetFolderPath("Desktop")
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$deskExe = Join-Path $desktop "Redline Gaming Optimizer.exe"

@(
    "REDLINE_Intro.mp4", "REDLINE_Promo.mp4",
    "Redline Gaming Optimizer.lnk", "Redline Gaming Optimizer V9.lnk",
    "Redline UPDATE-TEST (V9.0).lnk", "GamingBooster_Pro.exe",
    "Redline_Gaming_Optimizer.exe"
) | ForEach-Object {
    $p = Join-Path $desktop $_
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "Geloescht: $_" }
}

Get-ChildItem $desktop -Filter "*.mp4" -File -EA SilentlyContinue |
    Where-Object { $_.Name -match 'REDLINE|Redline|GamingBooster|Intro|Promo' } |
    Remove-Item -Force

Get-ChildItem $desktop -Filter "*.exe" -File -EA SilentlyContinue |
    Where-Object {
        ($_.Name -match '^Redline V\d|^Redline_Gaming|^GamingBooster' -and $_.Name -ne 'Redline Gaming Optimizer.exe') -or
        ($_.Name -match '^Redline_Gaming_Optimizer_Setup' -and $_.Name -ne 'Redline_Gaming_Optimizer_Setup_v9.28.exe')
    } | ForEach-Object {
    Remove-Item $_.FullName -Force
    Write-Host "Geloescht: $($_.Name)"
}

$df = Join-Path $desktop "GamingBooster_Pro"
$repoCsproj = Join-Path $df "GamingBooster_Pro\GamingBooster_Pro.csproj"
if ((Test-Path $df) -and -not (Test-Path $repoCsproj)) {
    Remove-Item $df -Recurse -Force
    Write-Host "Ordner-Kopie geloescht: $df"
}

$src = Join-Path $root "publish\win-x64-full\GamingBooster_Pro.exe"
if (-not (Test-Path $src)) { $src = Join-Path $root "publish\win-x64\GamingBooster_Pro.exe" }
if (-not (Test-Path $src)) {
    Write-Host "Publish fehlt: $src"
    exit 1
}

Copy-Item $src $deskExe -Force
Write-Host "OK: App -> $deskExe"

$setupSrc = Join-Path $root "dist\Redline_Gaming_Optimizer_Setup_v9.28.exe"
$setupDesk = Join-Path $desktop "Redline_Gaming_Optimizer_Setup_v9.28.exe"
if (Test-Path $setupSrc) {
    Copy-Item $setupSrc $setupDesk -Force
    Write-Host "OK: Installer -> $setupDesk"
} else {
    Write-Host "Installer fehlt: $setupSrc (scripts\build-installer.ps1 ausfuehren)"
}
