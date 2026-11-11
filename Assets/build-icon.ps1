Add-Type -AssemblyName System.Drawing

$outDir = "D:\Apps\KeepAwake\Assets"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $radius = [float]($s * 0.22)
    $d = [float]($radius * 2)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($s - $d - 1, 0, $d, $d, 270, 90)
    $path.AddArc($s - $d - 1, $s - $d - 1, $d, $d, 0, 90)
    $path.AddArc(0, $s - $d - 1, $d, $d, 90, 90)
    $path.CloseFigure()

    $fill = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#3E8E6E"))
    $g.FillPath($fill, $path)

    $stroke = [float]([Math]::Max(1.4, $s * 0.085))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $stroke)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $box = [float]($s * 0.27)
    $side = [float]($s * 0.46)
    $top = [float]($s * 0.31)
    $g.DrawArc($pen, $box, $top, $side, $side, 310, 280)
    $g.DrawLine($pen, [float]($s * 0.5), [float]($s * 0.23), [float]($s * 0.5), [float]($s * 0.50))

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , @($s, $ms.ToArray())

    $pen.Dispose(); $fill.Dispose(); $path.Dispose(); $g.Dispose(); $bmp.Dispose(); $ms.Dispose()
}

$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ico)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$pngs.Count)

$offset = 6 + (16 * $pngs.Count)
foreach ($entry in $pngs) {
    $s = $entry[0]; $data = $entry[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($entry in $pngs) { $bw.Write($entry[1]) }
$bw.Flush()

[System.IO.File]::WriteAllBytes((Join-Path $outDir "AppIcon.ico"), $ico.ToArray())
$bw.Dispose(); $ico.Dispose()
Write-Output "Icon written: $((Get-Item (Join-Path $outDir 'AppIcon.ico')).Length) bytes"