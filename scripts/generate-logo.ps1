# Genera el logo de DexSuite (tema CYBERNETIC) en PNG y ICO multi-res.
# Diseño: cuadrado redondeado con gradiente cian -> violeta, texto "DX" centrado.
# Resultados:
#   src/DexSuite.App/Assets/AppIcon.png   (1024x1024)
#   src/DexSuite.App/Assets/AppIcon.ico   (multi-res 256/128/64/48/32/16)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$assetsDir = Join-Path $PSScriptRoot "..\src\DexSuite.App\Assets"
$pngPath = Join-Path $assetsDir "AppIcon.png"
$icoPath = Join-Path $assetsDir "AppIcon.ico"

# --- Colores del tema CYBERNETIC ---
$bgDark      = [System.Drawing.ColorTranslator]::FromHtml("#050511")  # fondo
$cianBright  = [System.Drawing.ColorTranslator]::FromHtml("#00E5FF")  # gradient start
$cianMid     = [System.Drawing.ColorTranslator]::FromHtml("#00B8D9")  # mid
$violetBright= [System.Drawing.ColorTranslator]::FromHtml("#B026FF")  # gradient end
$violetMid   = [System.Drawing.ColorTranslator]::FromHtml("#9D4EDD")  # mid

function New-LogoBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # ---------- Forma principal: cuadrado redondeado con gradiente ----------
    $padding = [int]($size * 0.06)
    $shapeSize = $size - ($padding * 2)
    $cornerRadius = [int]($size * 0.18)
    $rect = New-Object System.Drawing.Rectangle($padding, $padding, $shapeSize, $shapeSize)

    # Path para esquinas redondeadas
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $cornerRadius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    # Gradiente lineal diagonal cian -> violeta
    $startPoint = New-Object System.Drawing.PointF($rect.X, $rect.Y)
    $endPoint = New-Object System.Drawing.PointF($rect.Right, $rect.Bottom)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($startPoint, $endPoint, $cianBright, $violetBright)
    $g.FillPath($brush, $path)

    # Glow exterior sutil
    $glowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, $cianBright.R, $cianBright.G, $cianBright.B), [float]($size * 0.008))
    $g.DrawPath($glowPen, $path)
    $glowPen.Dispose()

    # ---------- Texto "DX" centrado ----------
    $fontSize = [float]($size * 0.42)
    try {
        $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    } catch {
        $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    }

    $text = "DX"
    $textColor = [System.Drawing.Color]::FromArgb(255, 8, 8, 22)  # casi negro, encaja con el fondo del tema
    $textBrush = New-Object System.Drawing.SolidBrush($textColor)

    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    # Sombra muy sutil para profundidad (offset 1% del tamaño)
    $shadowOffset = [float]($size * 0.008)
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 0, 0, 0))
    $shadowX = [float]([float]$padding + $shadowOffset)
    $shadowY = [float]([float]$padding + $shadowOffset)
    $shadowW = [float]$shapeSize
    $shadowH = [float]$shapeSize
    $rectShadow = New-Object System.Drawing.RectangleF($shadowX, $shadowY, $shadowW, $shadowH)
    $g.DrawString($text, $font, $shadowBrush, $rectShadow, $sf)
    $shadowBrush.Dispose()

    # Texto principal
    $px = [float]$padding
    $py = [float]$padding
    $pw = [float]$shapeSize
    $ph = [float]$shapeSize
    $rectF = New-Object System.Drawing.RectangleF($px, $py, $pw, $ph)
    $g.DrawString($text, $font, $textBrush, $rectF, $sf)

    # Cleanup
    $textBrush.Dispose()
    $font.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $g.Dispose()
    return $bmp
}

# --- Genera PNG 1024 ---
Write-Host "Generando PNG 1024x1024..."
$bmpMain = New-LogoBitmap 1024
$bmpMain.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmpMain.Dispose()
Write-Host "  OK $pngPath"

# --- Genera ICO multi-res ---
Write-Host "Generando ICO multi-resolución..."
$sizes = @(256, 128, 64, 48, 32, 16)
$bitmaps = @()
foreach ($s in $sizes) { $bitmaps += (New-LogoBitmap $s) }

# Estructura del archivo ICO (formato binario)
# Header (6 bytes) + n * IconDirEntry (16 bytes cada uno) + n * imagen PNG
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICONDIR header
$bw.Write([uint16]0)              # Reserved
$bw.Write([uint16]1)              # Type (1 = icon)
$bw.Write([uint16]$bitmaps.Count) # # de imágenes

# Cada ICONDIRENTRY ocupa 16 bytes. La data de imagen viene después.
$offset = 6 + (16 * $bitmaps.Count)
$pngStreams = @()
for ($i = 0; $i -lt $bitmaps.Count; $i++) {
    $imgStream = New-Object System.IO.MemoryStream
    $bitmaps[$i].Save($imgStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += $imgStream

    $w = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $h = $w
    $bw.Write([byte]$w)            # width (0 = 256)
    $bw.Write([byte]$h)            # height
    $bw.Write([byte]0)             # color palette (0 = sin paleta)
    $bw.Write([byte]0)             # reserved
    $bw.Write([uint16]1)           # color planes
    $bw.Write([uint16]32)          # bits per pixel
    $bw.Write([uint32]$imgStream.Length)  # tamaño en bytes
    $bw.Write([uint32]$offset)     # offset al inicio de la imagen
    $offset += [int]$imgStream.Length
}

# Append PNG data
foreach ($s in $pngStreams) {
    $bw.Write($s.ToArray())
    $s.Dispose()
}

[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
foreach ($b in $bitmaps) { $b.Dispose() }
Write-Host "  OK $icoPath"

Write-Host ""
Write-Host "Logo generado:"
Write-Host "  PNG: $pngPath"
Write-Host "  ICO: $icoPath"
