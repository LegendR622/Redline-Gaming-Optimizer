# Redline – Vollständiger Funktionstest (Build, Logik, UI-Tour, mehrfach)
param(
    [int]$LogicRuns = 3,
    [int]$UiRuns = 2,
    [switch]$WithCleanerScan
)

$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root "GamingBooster_Pro\GamingBooster_Pro.csproj"
$exe = Join-Path $root "publish\win-x64-new\GamingBooster_Pro.exe"
if (-not (Test-Path $exe)) { $exe = Join-Path $root "publish\win-x64\GamingBooster_Pro.exe" }
$exeBin = Join-Path $root "GamingBooster_Pro\bin\Release\net10.0-windows\win-x64\GamingBooster_Pro.exe"
$dllProj = Join-Path $root "GamingBooster_Pro"
$results = New-Object System.Collections.Generic.List[string]
$failCount = 0

function Add-Result($name, $ok, $detail = "") {
    $script:global:failCount += if (-not $ok) { 1 } else { 0 }
    $status = if ($ok) { "OK" } else { "FAIL" }
    $line = "[$status] $name"
    if ($detail) { $line += " | $detail" }
    $results.Add($line) | Out-Null
    $color = if ($ok) { "Green" } else { "Red" }
    Write-Host $line -ForegroundColor $color
}

function Stop-RedlineApp {
    Get-Process -Name "GamingBooster_Pro" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 400
}

Write-Host ""
Stop-RedlineApp

Write-Host "=== Redline Vollständiger Funktionstest V9.9 ===" -ForegroundColor Cyan
Write-Host "Logik-Läufe: $LogicRuns | UI-Läufe: $UiRuns | Cleaner-Scan: $WithCleanerScan"
Write-Host ""

# --- Build ---
try {
    Push-Location (Split-Path $proj)
    dotnet build -c Release -v q 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "build exit $LASTEXITCODE" }
    Pop-Location
    Add-Result "dotnet build" $true
} catch {
    Add-Result "dotnet build" $false $_.Exception.Message
}

if (-not (Test-Path $exe)) {
    try {
        Stop-RedlineApp
        Push-Location (Split-Path $proj)
        dotnet publish -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -o "..\publish\win-x64" 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            dotnet publish -c Release -r win-x64 --self-contained true `
                -p:PublishSingleFile=true -o "..\publish\win-x64-new" 2>&1 | Out-Null
            $alt = Join-Path $root "publish\win-x64-new\GamingBooster_Pro.exe"
            if (Test-Path $alt) { $exe = $alt }
        }
        Pop-Location
        Add-Result "dotnet publish" (Test-Path $exe)
    } catch { Add-Result "dotnet publish" $false $_.Exception.Message }
}
if (-not (Test-Path $exe) -and (Test-Path $exeBin)) { $exe = $exeBin }
# Immer die frisch gebaute EXE nutzen (Publish-Ordner kann veraltet sein)
if (Test-Path $exeBin) { $exe = $exeBin }
Add-Result "Test-EXE" (Test-Path $exe) $exe

# --- version.json ---
$verFile = Join-Path $root "version.json"
if (Test-Path $verFile) {
    $vj = Get-Content $verFile -Raw | ConvertFrom-Json
    Add-Result "version.json Version" ($vj.version -eq "9.9") ("v$($vj.version)")
    Add-Result "version.json downloadUrl" ($null -ne $vj.downloadUrl) $vj.downloadUrl
} else {
    Add-Result "version.json" $false "fehlt"
}

# --- WMI Speicher ---
try {
    $sys = [System.Environment]::SystemDirectory.Substring(0, 2)
    $disk = Get-CimInstance Win32_LogicalDisk -Filter ("DeviceID='$sys'") | Select-Object -First 1
    Add-Result "Festplatte WMI $sys" ($disk.Size -gt 0) ("$([math]::Round($disk.Size/1GB,1)) GB")
} catch {
    Add-Result "Festplatte WMI" $false $_.Exception.Message
}

# --- Cleaner-Pfade ---
$local = [Environment]::GetFolderPath("LocalApplicationData")
$cleanerPaths = @(
    (Join-Path $local "D3DSCache"),
    $env:TEMP,
    "C:\Windows\Temp"
)
$okPaths = ($cleanerPaths | Where-Object { Test-Path $_ }).Count
Add-Result "Cleaner Pfade" ($okPaths -ge 2) ("$okPaths/$($cleanerPaths.Count)")

# --- AppData ---
$appDir = Join-Path $env:APPDATA "RedlineGamingOptimizer"
if (-not (Test-Path $appDir)) { New-Item -ItemType Directory -Path $appDir -Force | Out-Null }
Add-Result "AppData Ordner" (Test-Path $appDir) $appDir

# --- Online version.json (optional – CDN kann kurz HTML liefern) ---
try {
    $r = Invoke-WebRequest -Uri "https://cdn.jsdelivr.net/gh/LegendR622/Redline-Gaming-Optimizer@main/version.json" -UseBasicParsing -TimeoutSec 15
    if ($r.Content.Trim().StartsWith("{")) {
        $online = ($r.Content | ConvertFrom-Json).version
        Add-Result "Online version.json" ($online.Length -gt 0) "v$online"
    } else {
        Add-Result "Online version.json" $true "übersprungen (CDN-Antwort kein JSON)"
    }
} catch {
    Add-Result "Online version.json" $true "übersprungen ($($_.Exception.Message))"
}

if (-not (Test-Path $exe)) {
    Write-Host ""
    Write-Host "Abbruch: keine EXE zum UI-Test." -ForegroundColor Red
    exit 1
}

Stop-RedlineApp

$env:REDLINE_SELFTEST_OFFLINE = "1"

# --- Logik --selftest (mehrfach) ---
$logicOk = 0
for ($i = 1; $i -le $LogicRuns; $i++) {
    Stop-RedlineApp
    $p = Start-Process -FilePath $exe -ArgumentList "--selftest","--nosplash" -PassThru -Wait -WindowStyle Hidden
    $ok = ($p.ExitCode -eq 0)
    if ($ok) { $logicOk++ }
    Add-Result "Logik --selftest Lauf $i/$LogicRuns" $ok ("ExitCode $($p.ExitCode)")
}
Add-Result "Logik gesamt ($LogicRuns x)" ($logicOk -eq $LogicRuns) "$logicOk/$LogicRuns OK"

$logLogic = Join-Path $env:TEMP "redline-selftest.log"
Add-Result "SelfTest Logdatei" (Test-Path $logLogic) $logLogic

Stop-RedlineApp

# --- UI-Selftest (alle Seiten 2x + Cleaner-Logik) ---
$uiOk = 0
for ($i = 1; $i -le $UiRuns; $i++) {
    Stop-RedlineApp
    $env:REDLINE_SKIP_INTRO = "1"
    $env:REDLINE_UI_SELFTEST = "1"
    if ($WithCleanerScan) { $env:REDLINE_UI_SCAN = "1" } else { Remove-Item Env:REDLINE_UI_SCAN -ErrorAction SilentlyContinue }
    $p = Start-Process -FilePath $exe -ArgumentList "--nosplash" -PassThru -Wait -WindowStyle Hidden
    Remove-Item Env:REDLINE_UI_SELFTEST -ErrorAction SilentlyContinue
    $ok = ($p.ExitCode -eq 0)
    if ($ok) { $uiOk++ }
    Add-Result "UI-Selftest Lauf $i/$UiRuns" $ok ("ExitCode $($p.ExitCode)")
}
Add-Result "UI-Selftest gesamt" ($uiOk -eq $UiRuns) "$uiOk/$UiRuns OK"

$logUi = Join-Path $env:TEMP "redline-ui-selftest.log"
if (Test-Path $logUi) {
    $last = Get-Content $logUi -Tail 5
    Add-Result "UI-Log Ergebniszeile" ($last -match "ALLE UI-TESTS OK") ($last[-1])
}

Stop-RedlineApp

# --- Demo-Tour (alle Menüseiten, kein Crash) ---
$env:REDLINE_SKIP_INTRO = "1"
$env:REDLINE_DEMO_PAGE_MS = "900"
$pTour = Start-Process -FilePath $exe -ArgumentList "--nosplash","--demo-tour" -PassThru
Start-Sleep -Seconds 28
$crashed = $pTour.HasExited -and $pTour.ExitCode -ne 0
$stillRunning = -not $pTour.HasExited
if ($stillRunning) {
    Stop-Process -Id $pTour.Id -Force -ErrorAction SilentlyContinue
    Add-Result "Demo-Tour (kein Absturz)" $true "lief 45s, PID $($pTour.Id)"
} else {
    Add-Result "Demo-Tour (kein Absturz)" (-not $crashed) ("Exit $($pTour.ExitCode)")
}
Remove-Item Env:REDLINE_DEMO_PAGE_MS -ErrorAction SilentlyContinue

Stop-RedlineApp

# --- Schnellstart Cleaner (3x) ---
for ($i = 1; $i -le 3; $i++) {
    $env:REDLINE_SKIP_INTRO = "1"
    $env:REDLINE_START_PAGE = "Cleaner"
    $p = Start-Process -FilePath $exe -ArgumentList "--nosplash" -PassThru
    Start-Sleep -Seconds 3
    $ok = -not $p.HasExited
    Add-Result "Start Cleaner Lauf $i" $ok ("PID $($p.Id)")
    if ($ok) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}
Remove-Item Env:REDLINE_START_PAGE -ErrorAction SilentlyContinue

# --- Desktop EXE ---
$desk = Join-Path $env:USERPROFILE "Desktop\Redline Gaming Optimizer.exe"
Add-Result "Desktop EXE" (Test-Path $desk) $desk

# --- Zusammenfassung ---
$lines = $results | Where-Object { $_ -like "[FAIL]*" }
$totalFail = @($lines).Count
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Gesamt: $($results.Count - $totalFail)/$($results.Count) bestanden, $totalFail Fehler" -ForegroundColor $(if ($totalFail -eq 0) { "Green" } else { "Yellow" })
Write-Host "Logs: $logLogic | $logUi" -ForegroundColor DarkGray

$outLog = Join-Path $root "FUNktionstest.log"
$results | Set-Content $outLog -Encoding UTF8
Write-Host "Report: $outLog"

if ($totalFail -gt 0) {
    Write-Host ""
    Write-Host "Fehlgeschlagene Tests:" -ForegroundColor Red
    $lines | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    exit 1
}
exit 0
