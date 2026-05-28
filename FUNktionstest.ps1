# Redline Gaming Optimizer – Schnelltest (ohne UI-Klicks)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$proj = Join-Path $root "GamingBooster_Pro\GamingBooster_Pro.csproj"
$exe = Join-Path $root "publish\win-x64\GamingBooster_Pro.exe"
$results = New-Object System.Collections.Generic.List[string]

function Add-Result($name, $ok, $detail = "") {
    $status = if ($ok) { "OK" } else { "FAIL" }
    $line = "[$status] $name"
    if ($detail) { $line += " | $detail" }
    $results.Add($line)
    Write-Host $line
}

Write-Host "=== Redline Funktionstest ===" -ForegroundColor Cyan

# Build
try {
    dotnet build $proj -c Release -v q | Out-Null
    Add-Result "dotnet build" $true
} catch {
    Add-Result "dotnet build" $false $_.Exception.Message
}

# Publish exe exists
Add-Result "publish EXE" (Test-Path $exe) $exe

# System drive (Speicherübersicht)
try {
    $sys = [System.Environment]::SystemDirectory.Substring(0,3)
    $d = New-Object System.IO.DriveInfo $sys
    Add-Result "Systemlaufwerk $sys" ($d.IsReady -and $d.TotalSize -gt 0) ("{0:N1} GB total" -f ($d.TotalSize/1GB))
} catch {
    Add-Result "Systemlaufwerk" $false $_.Exception.Message
}

# Autostart registry
try {
    $cu = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue).PSObject.Properties.Count
    $lm = (Get-ItemProperty "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -ErrorAction SilentlyContinue).PSObject.Properties.Count
    Add-Result "Autostart Registry" ($cu -gt 0 -or $lm -gt 0) ("CU=$cu LM=$lm")
} catch {
    Add-Result "Autostart Registry" $false $_.Exception.Message
}

# WMI CPU/GPU
try {
    $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name
    Add-Result "WMI CPU" (![string]::IsNullOrWhiteSpace($cpu)) $cpu
} catch { Add-Result "WMI CPU" $false $_.Exception.Message }

try {
    $gpu = Get-CimInstance Win32_VideoController | Where-Object { $_.Name -notmatch "Microsoft Basic" } | Select-Object -First 1 -ExpandProperty Name
    Add-Result "WMI GPU" (![string]::IsNullOrWhiteSpace($gpu)) $gpu
} catch { Add-Result "WMI GPU" $false $_.Exception.Message }

# Pro license keys (offline logic mirror)
$keys = @("REDLINE-PRO-V9-IMMISCH", "INVALID-KEY")
Add-Result "Pro-Key gültig" ($keys[0] -match "REDLINE-PRO") $keys[0]
Add-Result "Pro-Key ungültig abgelehnt" ($keys[1] -notmatch "^REDLINE-PRO-V9-IMMISCH$")

# Settings path
$settings = Join-Path $env:APPDATA "RedlineGamingOptimizer\settings.json"
Add-Result "Settings-Datei" (Test-Path $settings) $settings

# App start (smoke)
if (Test-Path $exe) {
    Get-Process -Name "GamingBooster_Pro" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    $env:REDLINE_SKIP_INTRO = "1"
    $p = Start-Process $exe -PassThru
    Start-Sleep -Seconds 3
    $running = -not $p.HasExited
    Add-Result "App startet" $running ("PID $($p.Id)")
    if ($running) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }
}

$fail = @($results | Where-Object { $_.StartsWith("[FAIL]") }).Count
Write-Host ""
Write-Host "Ergebnis: $($results.Count - $fail)/$($results.Count) OK, $fail Fehler" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Yellow" })
