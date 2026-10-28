# Gera src\nota.ico (16, 32, 48 e 256 px, com PNG embutido).
# Uso: powershell -ExecutionPolicy Bypass -File tools\gerar-icone.ps1
param([string]$Saida = (Join-Path $PSScriptRoot '..\src\nota.ico'))

Add-Type -AssemblyName System.Drawing

function P([double]$x, [double]$y) { New-Object System.Drawing.PointF ([float]($x * $script:s)), ([float]($y * $script:s)) }

$tamanhos = 16, 32, 48, 256
$imagens = @()
foreach ($t in $tamanhos) {
    $script:s = $t / 256.0
    $bmp = New-Object System.Drawing.Bitmap $t, $t
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'

    $corpo = [System.Drawing.PointF[]]@((P 24 16), (P 184 16), (P 232 64), (P 232 240), (P 24 240))
    $dobra = [System.Drawing.PointF[]]@((P 184 16), (P 184 64), (P 232 64))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 216, 77))), $corpo)
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 180, 40))), $dobra)
    $g.DrawPolygon((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(180, 140, 20)), ([float][Math]::Max(1, 8 * $s))), $corpo)

    $linha = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120, 90, 20)), ([float][Math]::Max(1, 14 * $s))
    foreach ($y in 104, 148, 192) { $g.DrawLine($linha, (P 60 $y), (P 196 $y)) }

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $imagens += , $ms.ToArray()
    $g.Dispose(); $bmp.Dispose()
}

$saidaStream = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $saidaStream
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$tamanhos.Count)
$offset = 6 + 16 * $tamanhos.Count
for ($i = 0; $i -lt $tamanhos.Count; $i++) {
    $lado = [byte]($tamanhos[$i] -band 255)   # 256 é gravado como 0
    $w.Write($lado); $w.Write($lado); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$imagens[$i].Length); $w.Write([uint32]$offset)
    $offset += $imagens[$i].Length
}
foreach ($img in $imagens) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath (Split-Path $Saida)).Path + '\' + (Split-Path $Saida -Leaf), $saidaStream.ToArray())
Write-Host "Ícone gerado em $Saida"
