# Cinema-Effekte auf Promo-Rohvideo (2K, kompakte Dateigroesse)
param(
    [Parameter(Mandatory = $true)][string]$InputVideo,
    [Parameter(Mandatory = $true)][string]$OutputVideo,
    [string]$MusicPath = "",
    [int]$DurationSec = 78
)
$ErrorActionPreference = "Stop"
$ff = (Get-Command ffmpeg).Source

$w = 2560
$h = 1440
$fadeOut = [Math]::Max(0, $DurationSec - 4)

# Leichter Rot-Look, Schaerfe, Vignette, Ein-/Ausblendung, dezenter Zoom
$vFilter = @"
[0:v]scale=${w}:${h}:flags=lanczos:force_original_aspect_ratio=decrease,
pad=${w}:${h}:(ow-iw)/2:(oh-ih)/2:color=0x07070a,
eq=contrast=1.05:brightness=0.02:saturation=1.1:gamma=1.0,
unsharp=7:7:0.85:7:7:0.0,
vignette=angle=PI/5,
fade=t=in:st=0:d=1.2:color=black,
fade=t=out:st=${fadeOut}:d=3.5:color=black,
drawtext=fontfile=C\\:/Windows/Fonts/arialbd.ttf:text='REDLINE V9.2':fontsize=58:fontcolor=white:borderw=3:bordercolor=0xEB1230@0.95:x=(w-text_w)/2:y=42:enable='between(t,1.5,7)',
drawtext=fontfile=C\\:/Windows/Fonts/arial.ttf:text='100%%25 FREE - Pro Coming Soon':fontsize=36:fontcolor=0xF0F0F0:x=(w-text_w)/2:y=112:enable='between(t,2,8)',
drawtext=fontfile=C\\:/Windows/Fonts/arial.ttf:text='Gaming Optimizer for Windows':fontsize=30:fontcolor=0xC8C8C8:x=(w-text_w)/2:y=h-72:enable='between(t,4,12)',
format=yuv420p[v]
"@ -replace "`r`n", ""

if ($MusicPath -and (Test-Path $MusicPath)) {
    & $ff -y -i $InputVideo -i $MusicPath `
        -filter_complex "${vFilter};[1:a]volume=0.45,afade=t=in:st=0:d=2,afade=t=out:st=${fadeOut}:d=4[a]" `
        -map "[v]" -map "[a]" -c:v libx264 -preset medium -crf 22 -maxrate 6M -bufsize 12M `
        -c:a aac -b:a 128k -movflags +faststart -shortest $OutputVideo
} else {
    & $ff -y -i $InputVideo `
        -vf $vFilter `
        -c:v libx264 -preset medium -crf 22 -maxrate 6M -bufsize 12M -an -movflags +faststart $OutputVideo
}

Write-Host "Effekte OK: $OutputVideo" -ForegroundColor Green
