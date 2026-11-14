# Generate app.ico and app.png for Dev Lite Server
Add-Type -AssemblyName System.Drawing

$ScriptRoot = Split-Path -Parent $PSScriptRoot
if (-not $ScriptRoot) { $ScriptRoot = Get-Location }

$AssetsDir = Join-Path $ScriptRoot "assets"
if (-not (Test-Path $AssetsDir)) {
    New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null
}

$IcoPath = Join-Path $AssetsDir "app.ico"
$PngPath = Join-Path $AssetsDir "app.png"

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngStreams = [System.Collections.Generic.List[byte[]]]::new()

function Create-RoundedRectPath([System.Drawing.RectangleF]$rect, [float]$radius) {
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $d = $radius * 2.0
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Render-Icon([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)

    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $scale = $size / 256.0

    # 1. Background Squircle (Midnight Navy Slate #182535 / #0D1722)
    $cornerRadius = 48.0 * $scale
    $margin = 8.0 * $scale
    $bgRect = [System.Drawing.RectangleF]::new($margin, $margin, $size - (2 * $margin), $size - (2 * $margin))
    $bgPath = Create-RoundedRectPath $bgRect $cornerRadius

    $c1 = [System.Drawing.Color]::FromArgb(255, 24, 37, 53)  # Slate Cyan Container (#182535)
    $c2 = [System.Drawing.Color]::FromArgb(255, 13, 23, 34)  # Deep Midnight Blue (#0D1722)
    $bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new($bgRect, $c1, $c2, [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($bgBrush, $bgPath)
    $bgBrush.Dispose()

    $borderPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(180, 64, 120, 240), [Math]::Max(1.5 * $scale, 1.0)) # Vibrant Cobalt Blue (#4078F0)
    $g.DrawPath($borderPen, $bgPath)
    $borderPen.Dispose()
    $bgPath.Dispose()

    # 2. Server Stack Trays
    $trayX = 38.0 * $scale
    $trayW = 180.0 * $scale
    $trayH = 34.0 * $scale
    $trayRadius = 8.0 * $scale
    $trayYPositions = @((46.0 * $scale), (92.0 * $scale), (138.0 * $scale))

    for ($i = 0; $i -lt $trayYPositions.Length; $i++) {
        $ty = $trayYPositions[$i]
        $trayRect = [System.Drawing.RectangleF]::new($trayX, $ty, $trayW, $trayH)
        $trayPath = Create-RoundedRectPath $trayRect $trayRadius

        $trayBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(240, 17, 29, 43)) # Surface (#111D2B)
        $g.FillPath($trayBrush, $trayPath)
        $trayBrush.Dispose()

        $trayPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(140, 50, 68, 88), [Math]::Max(1.0 * $scale, 0.5)) # Border (#324458)
        $g.DrawPath($trayPen, $trayPath)
        $trayPen.Dispose()
        $trayPath.Dispose()

        $ledRadius = 4.0 * $scale
        $ledY = $ty + ($trayH / 2.0) - $ledRadius

        # Mint / Sea Green LED (#20C49C)
        $greenBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 32, 196, 156))
        $g.FillEllipse($greenBrush, $trayX + $trayW - (20.0 * $scale), $ledY, $ledRadius * 2, $ledRadius * 2)
        $greenBrush.Dispose()

        # Cobalt Blue LED (#4078F0)
        $ledColor = if ($i -eq 0) { [System.Drawing.Color]::FromArgb(255, 75, 140, 255) } else { [System.Drawing.Color]::FromArgb(200, 64, 120, 240) }
        $blueBrush = [System.Drawing.SolidBrush]::new($ledColor)
        $g.FillEllipse($blueBrush, $trayX + $trayW - (36.0 * $scale), $ledY, $ledRadius * 2, $ledRadius * 2)
        $blueBrush.Dispose()

        # Slots
        $slotH = 3.0 * $scale
        $slotW = 18.0 * $scale
        $slotY = $ty + ($trayH / 2.0) - ($slotH / 2.0)
        $slotBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(160, 25, 37, 54))
        $g.FillRectangle($slotBrush, $trayX + (16.0 * $scale), $slotY, $slotW, $slotH)
        $g.FillRectangle($slotBrush, $trayX + (40.0 * $scale), $slotY, $slotW, $slotH)
        $g.FillRectangle($slotBrush, $trayX + (64.0 * $scale), $slotY, $slotW, $slotH)
        $slotBrush.Dispose()
    }

    # 3. Lightning Bolt Polygon
    $boltPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $boltPoints = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(146.0 * $scale, 95.0 * $scale),
        [System.Drawing.PointF]::new(108.0 * $scale, 150.0 * $scale),
        [System.Drawing.PointF]::new(134.0 * $scale, 150.0 * $scale),
        [System.Drawing.PointF]::new(118.0 * $scale, 218.0 * $scale),
        [System.Drawing.PointF]::new(164.0 * $scale, 142.0 * $scale),
        [System.Drawing.PointF]::new(136.0 * $scale, 142.0 * $scale)
    )
    $boltPath.AddPolygon($boltPoints)

    # Glow (Vibrant Cobalt Blue)
    $glowPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(100, 64, 120, 240), 8.0 * $scale)
    $glowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($glowPen, $boltPath)
    $glowPen.Dispose()

    # Fill Bolt Gradient (Bright Blue to Cobalt Blue)
    $boltRect = [System.Drawing.RectangleF]::new(108.0 * $scale, 95.0 * $scale, 56.0 * $scale, 123.0 * $scale)
    $boltBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $boltRect,
        [System.Drawing.Color]::FromArgb(255, 75, 150, 255),
        [System.Drawing.Color]::FromArgb(255, 45, 95, 215),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
    )
    $g.FillPath($boltBrush, $boltPath)
    $boltBrush.Dispose()

    # Outline Bolt (Crisp Cool White / Pale Blue Tint)
    $outlinePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255, 240, 248, 255), [Math]::Max(1.5 * $scale, 1.0))
    $outlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($outlinePen, $boltPath)
    $outlinePen.Dispose()
    $boltPath.Dispose()

    $g.Dispose()
    return $bmp
}

# 1. Render PNG 256x256
$masterBmp = Render-Icon 256
$masterBmp.Save($PngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$masterBmp.Dispose()
Write-Host "Generated PNG at: $PngPath"

# 2. Render all sizes for ICO
foreach ($s in $sizes) {
    $bmp = Render-Icon $s
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams.Add($ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

# 3. Write standard Windows ICO binary
$fs = [System.IO.FileStream]::new($IcoPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$bw = [System.IO.BinaryWriter]::new($fs)

$bw.Write([UInt16]0) # Reserved
$bw.Write([UInt16]1) # Type 1 = ICO
$bw.Write([UInt16]$sizes.Length) # Image count

$offset = 6 + (16 * $sizes.Length)

for ($i = 0; $i -lt $sizes.Length; $i++) {
    $s = $sizes[$i]
    $w = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    $h = if ($s -ge 256) { [byte]0 } else { [byte]$s }

    $bw.Write($w)
    $bw.Write($h)
    $bw.Write([byte]0) # Color count
    $bw.Write([byte]0) # Reserved
    $bw.Write([UInt16]1) # Color planes
    $bw.Write([UInt16]32) # Bits per pixel
    $bw.Write([UInt32]$pngStreams[$i].Length) # Image size
    $bw.Write([UInt32]$offset) # Offset

    $offset += $pngStreams[$i].Length
}

for ($i = 0; $i -lt $sizes.Length; $i++) {
    $bw.Write($pngStreams[$i])
}

$bw.Dispose()
$fs.Dispose()

# Also copy to www/favicon.ico if www folder exists
$wwwDir = Join-Path $ScriptRoot "www"
$FaviconPath = Join-Path $wwwDir "favicon.ico"
if (Test-Path $wwwDir) {
    Copy-Item -Path $IcoPath -Destination $FaviconPath -Force
}

Write-Host "[OK] Generated multi-resolution ICO (16, 24, 32, 48, 64, 128, 256) at: $IcoPath" -ForegroundColor Green
