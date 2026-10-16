$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class LightMoveIconNative {
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
}
'@
$repo=Split-Path $PSScriptRoot -Parent
$assets=Join-Path $repo 'doc\assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
$bitmap=New-Object Drawing.Bitmap 128,128
$graphics=[Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([Drawing.Color]::Transparent)
$path=New-Object Drawing.Drawing2D.GraphicsPath
foreach($arc in @(@(2,2,180),@(78,2,270),@(78,78,0),@(2,78,90))) { $path.AddArc($arc[0],$arc[1],48,48,$arc[2],90) }
$path.CloseFigure()
$fill=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(25,32,43))
$graphics.FillPath($fill,$path)
$pen=New-Object Drawing.Pen ([Drawing.Color]::FromArgb(100,210,231)),8
$pen.StartCap=$pen.EndCap=[Drawing.Drawing2D.LineCap]::Round
$graphics.DrawBezier($pen,22,94,51,95,56,37,106,29)
$point=New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255,104,121))
$graphics.FillEllipse($point,53,55,19,19)
$white=New-Object Drawing.Pen ([Drawing.Color]::FromArgb(255,225,231)),2
$graphics.DrawEllipse($white,53,55,19,19)
$bitmap.Save((Join-Path $assets 'lightmove-icon.png'),[Drawing.Imaging.ImageFormat]::Png)
$handle=$bitmap.GetHicon()
$icon=[Drawing.Icon]::FromHandle($handle)
$stream=[IO.File]::Create((Join-Path $repo 'grapher\lightmove.ico'))
try { $icon.Save($stream) } finally { $stream.Dispose(); $icon.Dispose(); [void][LightMoveIconNative]::DestroyIcon($handle) }
$white.Dispose();$point.Dispose();$pen.Dispose();$fill.Dispose();$path.Dispose();$graphics.Dispose();$bitmap.Dispose()
