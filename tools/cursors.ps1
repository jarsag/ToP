<#
.SYNOPSIS
    Turns the client's animated cursors (.ani) into PNG frames Unity can use.

.DESCRIPTION
    A cursor in the client is a Windows animated cursor: a RIFF container holding an
    anih header, per-step rates, a play order, and frames that are ordinary .cur
    blobs. Each frame is decoded here - a bottom-up 32 bit bitmap with its own
    hotspot, falling back to the AND mask when the alpha channel is empty - and
    written as a PNG with a JSON beside it naming the rate and the hotspot of every
    frame. The textures are imported as unfiltered, uncompressed and readable, which
    is what Cursor.SetCursor needs.
#>
param(
    [string]$Client = 'C:\work\TalesOfPirateDX9\Client',
    [string]$Out = 'unity\TalesOfPirates\Assets\Resources\Ui\cursors'
)

Add-Type -AssemblyName System.Drawing

function Read-Chunks {
    param([byte[]]$Bytes, [int]$From, [int]$To)
    $chunks = @()
    $at = $From
    while ($at + 8 -le $To) {
        $id = [System.Text.Encoding]::ASCII.GetString($Bytes, $at, 4)
        $size = [BitConverter]::ToUInt32($Bytes, $at + 4)
        $chunks += [pscustomobject]@{ Id = $id; At = $at; Size = [int]$size; Body = $at + 8 }
        $at += 8 + $size + ($size % 2)
    }
    return $chunks
}

function Get-Frame {
    param([byte[]]$Bytes, [int]$At)
    # a .cur: reserved(2) type(2) count(2), then the first image's directory entry
    $count = [BitConverter]::ToUInt16($Bytes, $At + 4)
    $hotX = [BitConverter]::ToUInt16($Bytes, $At + 10)
    $hotY = [BitConverter]::ToUInt16($Bytes, $At + 12)
    $size = [BitConverter]::ToUInt32($Bytes, $At + 14)
    $image = $At + [BitConverter]::ToUInt32($Bytes, $At + 18)
    return [pscustomobject]@{ Images = $count; HotX = $hotX; HotY = $hotY; Image = $image }
}

function Get-Bitmap {
    param([byte[]]$Bytes, [int]$At)
    $w = [BitConverter]::ToInt32($Bytes, $At + 4)
    $h = [BitConverter]::ToInt32($Bytes, $At + 8) / 2   # the header counts the XOR and the AND mask together
    $bits = [BitConverter]::ToUInt16($Bytes, $At + 14)
    $palette = if ($bits -le 8) { 4 * [int][Math]::Pow(2, $bits) } else { 0 }
    $pixels = $At + [BitConverter]::ToInt32($Bytes, $At) + $palette
    $step = [int]($bits / 8)
    $stride = [int]($w * $step)
    $stride += (4 - ($stride % 4)) % 4
    $maskAt = $pixels + $stride * $h
    $maskStride = [int]([Math]::Ceiling($w / 32.0) * 4)
    $rgba = New-Object 'byte[]' ($w * $h * 4)
    $anyAlpha = $false
    for ($y = 0; $y -lt $h; $y++) {
        $row = $pixels + ($h - 1 - $y) * $stride
        for ($x = 0; $x -lt $w; $x++) {
            $p = $row + $x * $step
            $o = ($y * $w + $x) * 4
            $b = $Bytes[$p]; $g = $Bytes[$p + 1]; $r = $Bytes[$p + 2]
            $a = if ($step -ge 4) { $Bytes[$p + 3] } else { 255 }
            if ($a -ne 0) { $anyAlpha = $true }
            $rgba[$o] = $r; $rgba[$o + 1] = $g; $rgba[$o + 2] = $b; $rgba[$o + 3] = $a
        }
    }
    if (-not $anyAlpha -or $step -lt 4) {
        # no alpha at all: the AND mask is what says which pixels are see-through
        for ($y = 0; $y -lt $h; $y++) {
            $m = $maskAt + ($h - 1 - $y) * $maskStride
            for ($x = 0; $x -lt $w; $x++) {
                $bit = ($Bytes[$m + [int]($x / 8)] -shr (7 - ($x % 8))) -band 1
                if ($bit -eq 1) { $rgba[($y * $w + $x) * 4 + 3] = 0 }
            }
        }
    }
    return [pscustomobject]@{ Width = $w; Height = $h; Pixels = $rgba; Bits = $bits }
}

$root = Join-Path $Client 'cursor'
$target = Join-Path $PWD $Out
if (-not (Test-Path $root)) { "no cursor folder at $root"; exit 1 }
if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target -Force | Out-Null }

$template = Get-ChildItem (Join-Path $PWD 'unity\TalesOfPirates\Assets\Resources\Ui') -Filter '*.png.meta' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($template) { $meta = Get-Content $template.FullName -Raw } else { $meta = $null }

$made = 0
foreach ($file in Get-ChildItem $root -Filter '*.ani' | Sort-Object Name) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $top = Read-Chunks -Bytes $bytes -From 12 -To $bytes.Length
    $anih = $top | Where-Object { $_.Id -eq 'anih' } | Select-Object -First 1
    $rate = $top | Where-Object { $_.Id -eq 'rate' } | Select-Object -First 1
    $seq = $top | Where-Object { $_.Id -eq 'seq ' } | Select-Object -First 1
    $list = $top | Where-Object { $_.Id -eq 'LIST' } | Select-Object -First 1
    if (-not $anih -or -not $list) { "$($file.Name): not an animated cursor, skipped"; continue }

    $frames = @($list | ForEach-Object { Read-Chunks -Bytes $bytes -From ($_.Body + 4) -To ($_.Body + $_.Size) } | Where-Object { $_.Id -eq 'icon' })
    $rates = @()
    if ($rate) { for ($i = 0; $i -lt [int]($rate.Size / 4); $i++) { $rates += [BitConverter]::ToUInt32($bytes, $rate.Body + $i * 4) } }
    $order = @(0..([Math]::Max(0, $frames.Count - 1)))
    if ($seq) { $order = @(); for ($i = 0; $i -lt [int]($seq.Size / 4); $i++) { $order += [BitConverter]::ToUInt32($bytes, $seq.Body + $i * 4) } }

    $folder = Join-Path $target $file.BaseName
    if (-not (Test-Path $folder)) { New-Item -ItemType Directory -Path $folder -Force | Out-Null }

    $steps = @()
    $written = 0
    for ($s = 0; $s -lt $order.Count; $s++) {
        $frame = $frames[$order[$s]]
        if (-not $frame) { continue }
        $head = Get-Frame -Bytes $bytes -At $frame.Body
        $bmp = Get-Bitmap -Bytes $bytes -At $head.Image
        $name = 'f{0:D2}.png' -f $written
        $path = Join-Path $folder $name
        $bitmap = New-Object System.Drawing.Bitmap($bmp.Width, $bmp.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            for ($x = 0; $x -lt $bmp.Width; $x++) {
                $o = ($y * $bmp.Width + $x) * 4
                $c = [System.Drawing.Color]::FromArgb($bmp.Pixels[$o + 3], $bmp.Pixels[$o], $bmp.Pixels[$o + 1], $bmp.Pixels[$o + 2])
                $bitmap.SetPixel($x, $y, $c)
            }
        }
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        $ms = if ($s -lt $rates.Count -and $rates[$s] -gt 0) { [Math]::Round($rates[$s] * 1000 / 60) } else { 17 }
        $steps += [pscustomobject]@{ frame = $name; ms = $ms; hotspot = @($head.HotX, $head.HotY) }
        $written++
        if ($meta) {
            $text = $meta -replace 'guid: [0-9a-f]{32}', ('guid: ' + [guid]::NewGuid().ToString('N'))
            $text = $text -replace 'filterMode: -?\d+', 'filterMode: 0'
            $text = $text -replace 'enableMipMap: \d', 'enableMipMap: 0'
            $text = $text -replace 'isReadable: \d', 'isReadable: 1'
            $text = $text -replace 'alphaIsTransparency: \d', 'alphaIsTransparency: 1'
            $text = $text -replace 'textureCompression: \d', 'textureCompression: 0'
            [System.IO.File]::WriteAllText("$path.meta", $text)
        }
        $made++
    }
    $json = [pscustomobject]@{ steps = $steps } | ConvertTo-Json -Depth 4 -Compress
    [System.IO.File]::WriteAllText((Join-Path $folder "$($file.BaseName).json"), $json)
    "{0,-8} {1} frame(s), {2} files written" -f $file.BaseName, $written, $written
}
"total frames: $made"
"=== one of them, for look ==="
Get-Content (Join-Path $target 'pick\pick.json') -ErrorAction SilentlyContinue
Get-ChildItem (Join-Path $target 'pick') -ErrorAction SilentlyContinue | ForEach-Object { "  {0} {1:N0} b" -f $_.Name, $_.Length }