# 生成 PowerTray 的应用图标（exe / 快捷方式用）：圆角底块 + 电源符号，
# 多尺寸 PNG 帧打包为 .ico。几何与 src/PowerTray/Utils/IconFactory.cs 保持一致。
param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\src\PowerTray\Assets\app.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Frame([int]$size) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # 圆角底块（Windows 11 应用图标风格）
    $radius = [Math]::Max(2, [int]($size * 0.22))
    $d = $radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $topLeft = [System.Drawing.Point]::new(0, 0)
    $bottomLeft = [System.Drawing.Point]::new(0, $size)
    $brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $topLeft, $bottomLeft,
        [System.Drawing.Color]::FromArgb(255, 26, 123, 208),
        [System.Drawing.Color]::FromArgb(255, 0, 89, 168))
    $g.FillPath($brush, $path)

    # 电源符号：带顶部缺口的圆环 + 竖线
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, [float]($size * 0.075))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $m = [float]($size * 0.30)
    $rect = [System.Drawing.RectangleF]::new($m, $m, [float]($size) - 2 * $m, [float]($size) - 2 * $m)
    $g.DrawArc($pen, $rect, -55, 290)

    $cx = [float]($size / 2.0)
    $g.DrawLine($pen, $cx, [float]($size * 0.21), $cx, [float]($size * 0.50))

    $pen.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $g.Dispose()
    return $bmp
}

$outDir = Split-Path -Parent $OutputPath
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

$sizes = @(16, 24, 32, 48, 64, 256)
$frames = @()
foreach ($s in $sizes) {
    $bmp = New-Frame $s
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $frames += [PSCustomObject]@{ Size = $s; Data = $ms.ToArray() }
    $ms.Dispose()
}

$fs = [System.IO.File]::Create($OutputPath)
$bw = [System.IO.BinaryWriter]::new($fs)
try {
    $bw.Write([uint16]0)                      # reserved
    $bw.Write([uint16]1)                      # type: icon
    $bw.Write([uint16]$frames.Count)          # count

    $offset = 6 + 16 * $frames.Count
    foreach ($f in $frames) {
        $side = if ($f.Size -ge 256) { 0 } else { $f.Size }
        $bw.Write([byte]$side)                # width
        $bw.Write([byte]$side)                # height
        $bw.Write([byte]0)                    # color count
        $bw.Write([byte]0)                    # reserved
        $bw.Write([uint16]1)                  # planes
        $bw.Write([uint16]32)                 # bit count
        $bw.Write([uint32]$f.Data.Length)     # bytes in resource
        $bw.Write([uint32]$offset)            # image offset
        $offset += $f.Data.Length
    }
    foreach ($f in $frames) { $bw.Write($f.Data) }
}
finally {
    $bw.Dispose()
    $fs.Dispose()
}

Write-Output "icon written: $OutputPath ($($frames.Count) sizes)"
