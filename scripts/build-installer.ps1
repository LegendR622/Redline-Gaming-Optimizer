param(
    [string]$Version = "9.16"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$iss = Join-Path $root "scripts\RedlineSetup.iss"
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "C:\Program Files\Inno Setup 6\ISCC.exe" }
$pub = Join-Path $root "publish\win-x64\GamingBooster_Pro.exe"

if (-not (Test-Path $pub)) {
    Write-Host "Publish missing - run build-release first" -ForegroundColor Yellow
    & (Join-Path $root "scripts\build-release.ps1") -Version $Version -SkipZip
}

Get-Process -Name "GamingBooster_Pro" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
& $iscc $iss
$out = Join-Path $root "dist\Redline_Gaming_Optimizer_Setup_v$Version.exe"
if (-not (Test-Path $out)) { throw "Installer not created: $out" }
Write-Host "OK: $out" -ForegroundColor Green
