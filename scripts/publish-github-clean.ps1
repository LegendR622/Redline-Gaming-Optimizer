# Loescht alle alten GitHub-Releases und veroeffentlicht nur die aktuelle Setup-EXE
param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"
$repo = "LegendR622/Redline-Gaming-Optimizer"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$setup = Join-Path $root "dist\Redline_Gaming_Optimizer_Setup_v$Version.exe"

if (-not (Test-Path $setup)) {
    & (Join-Path $root "scripts\build-release.ps1") -Version $Version -SkipZip
}

Write-Host "Loesche alte Releases..." -ForegroundColor Yellow
$tags = gh release list -R $repo --limit 100 --json tagName -q ".[].tagName"
foreach ($tag in $tags) {
    Write-Host "  delete $tag"
    gh release delete $tag -R $repo -y --cleanup-tag 2>$null
    if ($LASTEXITCODE -ne 0) { gh release delete $tag -R $repo -y }
}

Write-Host "Neues Release v$Version (nur Setup-EXE)..." -ForegroundColor Cyan
gh release create "v$Version" $setup -R $repo `
    --title "Redline Gaming Optimizer V$Version" `
    --notes "Download only the Setup EXE installer. No ZIP folder needed."

Write-Host ("OK: https://github.com/" + $repo + "/releases/tag/v" + $Version) -ForegroundColor Green
