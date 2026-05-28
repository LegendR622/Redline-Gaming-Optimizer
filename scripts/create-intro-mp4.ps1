# Erstellt REDLINE_Promo.mp4: Intro + alle Seiten + echte Hintergrundmusik
param(
    [string]$MusicPath = "",
    [int]$SecondsPerPage = 5200,
    [int]$RecordSeconds = 78,
    [switch]$Vertical,
    [switch]$SkipDesktopCopy
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Join-Path $root "publish\win-x64\GamingBooster_Pro.exe"
$outDir = Join-Path $root "video"
$mp4 = Join-Path $outDir "REDLINE_Promo.mp4"
$musicOut = Join-Path $outDir "bg_music.mp3"
$rawVideo = Join-Path $outDir "promo_raw.mp4"
$defaultMusic = Join-Path $outDir "bg_music.mp3"

function Get-Ffmpeg() {
    $w = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($w) { return $w.Source }
    throw "ffmpeg fehlt: winget install Gyan.FFmpeg"
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct WinRect { public int Left; public int Top; public int Right; public int Bottom; }
public static class WinPos {
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out WinRect lpRect);
}
"@

if (-not (Test-Path $exe)) { throw "App fehlt: $exe. Zuerst: dotnet publish ..." }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$ff = Get-Ffmpeg

$title = "Redline Gaming Optimizer"
$appW = 1500
$appH = 860

function Get-MusicFile {
    param([string]$Custom)
    if ($Custom -and (Test-Path $Custom)) { return $Custom }
    if ((Test-Path $defaultMusic) -and ((Get-Item $defaultMusic).Length -gt 100000)) { return $defaultMusic }
    Write-Host "Lade Hintergrundmusik (SoundHelix, CC)..." -ForegroundColor Cyan
    $tmp = Join-Path $outDir "_dl_music.mp3"
    Invoke-WebRequest "https://www.soundhelix.com/examples/mp3/SoundHelix-Song-8.mp3" -OutFile $tmp -UseBasicParsing -TimeoutSec 90 -UserAgent "Mozilla/5.0"
    if ((Get-Item $tmp).Length -lt 100000) { throw "Musik-Download fehlgeschlagen." }
    Copy-Item $tmp $defaultMusic -Force
    return $defaultMusic
}

Write-Host "Starte Redline DEMO-TOUR (alle Seiten)..." -ForegroundColor Cyan
Stop-Process -Name "GamingBooster_Pro","Redline Gaming Optimizer" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600

$env:REDLINE_DEMO_PAGE_MS = "$SecondsPerPage"
Remove-Item Env:REDLINE_SKIP_INTRO -ErrorAction SilentlyContinue
Remove-Item Env:REDLINE_START_PAGE -ErrorAction SilentlyContinue
Remove-Item Env:REDLINE_DEMO_TOUR -ErrorAction SilentlyContinue

$p = Start-Process -FilePath $exe -ArgumentList "--demo-tour" -WorkingDirectory (Split-Path $exe) -PassThru
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 200; $i++) {
    Start-Sleep -Milliseconds 200
    $proc = Get-Process -ErrorAction SilentlyContinue | Where-Object {
        ($_.ProcessName -eq "GamingBooster_Pro" -or $_.ProcessName -eq "Redline Gaming Optimizer") -and $_.MainWindowHandle -ne 0
    } | Select-Object -First 1
    if ($proc) { $hwnd = [IntPtr]$proc.MainWindowHandle; break }
}
if ($hwnd -eq [IntPtr]::Zero) { throw "Fenster nicht gefunden." }

$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$posX = [int](($screen.Width - $appW) / 2)
$posY = [int](($screen.Height - $appH) / 2)
[void][WinPos]::MoveWindow($hwnd, $posX, $posY, $appW, $appH, $true)
[void][WinPos]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 1400

$rect = New-Object WinRect
[void][WinPos]::GetWindowRect($hwnd, [ref]$rect)
$posX = $rect.Left
$posY = $rect.Top
$appW = [Math]::Max(960, $rect.Right - $rect.Left)
$appH = [Math]::Max(640, $rect.Bottom - $rect.Top)
Write-Host "Fenster-Aufnahme: ${appW}x${appH} @ ($posX,$posY)" -ForegroundColor DarkGray

Write-Host "Aufnahme ${RecordSeconds}s (Intro + 11 Seiten, inkl. Scroll)..." -ForegroundColor Cyan
& $ff -y -f gdigrab -framerate 24 -draw_mouse 0 `
    -offset_x $posX -offset_y $posY -video_size "${appW}x${appH}" -i desktop -t $RecordSeconds `
    -vf "format=yuv420p" `
    -c:v libx264 -preset fast -crf 18 -pix_fmt yuv420p $rawVideo

Stop-Process -Name "GamingBooster_Pro","Redline Gaming Optimizer" -Force -ErrorAction SilentlyContinue
Remove-Item Env:REDLINE_DEMO_TOUR -ErrorAction SilentlyContinue

if ($MusicPath -and (Test-Path $MusicPath)) { Copy-Item $MusicPath $defaultMusic -Force }

Write-Host "Rohvideo: $rawVideo" -ForegroundColor Green
if (-not $SkipDesktopCopy) {
    Write-Host "Hinweis: Fuer finale MP4 build-promo-final.ps1 nutzen." -ForegroundColor Yellow
}
