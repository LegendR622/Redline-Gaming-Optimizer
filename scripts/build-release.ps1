param(
    [Parameter(Mandatory = $true)]
    [string]$Version,
    [switch]$SkipZip
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$proj = Join-Path $root "GamingBooster_Pro\GamingBooster_Pro.csproj"
$pubDir = Join-Path $root "publish\win-x64"
$distDir = Join-Path $root "dist"
$setupName = "Redline_Gaming_Optimizer_Setup_v$Version.exe"
$setupPath = Join-Path $distDir $setupName

Write-Host "Publishing Redline V$Version ..." -ForegroundColor Cyan
Get-Process -Name "GamingBooster_Pro" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $pubDir

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

if (-not $SkipZip) {
    $zipName = "Redline_V$Version`_win-x64.zip"
    $zipPath = Join-Path $distDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $pubDir "*") -DestinationPath $zipPath -Force
    Write-Host "OK: $zipPath" -ForegroundColor Green
}

& (Join-Path $root "scripts\build-installer.ps1") -Version $Version

$versionJson = @{
    version = $Version
    downloadUrl = "https://github.com/LegendR622/Redline-Gaming-Optimizer/releases/download/v$Version/$setupName"
    notes = "Redline Gaming Optimizer V$Version - Setup EXE installer"
    packageType = "setup"
} | ConvertTo-Json -Depth 3

$versionFile = Join-Path $root "version.json"
Set-Content -Path $versionFile -Value $versionJson -Encoding UTF8

Write-Host "OK: $setupPath" -ForegroundColor Green
Write-Host "OK: $versionFile" -ForegroundColor Green
Write-Host ""
Write-Host "GitHub: nur $setupName hochladen (Release v$Version)."
