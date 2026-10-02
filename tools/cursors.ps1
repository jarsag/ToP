<#
.SYNOPSIS
    Puts the client's cursors into the project as Unity textures.

.DESCRIPTION
    The frames come from the pictures in Client\cursor\OMNIVERT_Multi, one zip per
    cursor - the client's own animated cursors are 24 bit DIBs whose AND mask is easy
    to get wrong, and these are the same pictures decoded properly. What is read out
    of the .ani here is what a picture cannot carry: the order the frames are shown
    in, how long each is shown, and the hotspot of the cursor.
#>
param(
    [string]$Client = 'C:\work\TalesOfPirateDX9\Client',
    [string]$Out = 'unity\TalesOfPirates\Assets\Resources\Ui\cursors'
)

Add-Type -AssemblyName System.Drawing

$source = Join-Path $Client 'cursor'
$pictures = Join-Path $source 'OMNIVERT_Multi'
$target = Join-Path $PWD $Out
$work = Join-Path $PWD 'artifacts\cursor-frames'

if (-not (Test-Path $pictures)) { "no pictures at $pictures"; exit 1 }
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Path $work -Force | Out-Null
New-Item -ItemType Directory -Path $target -Force | Out-Null

"=== unpacking the pictures ==="
foreach ($zip in Get-ChildItem $pictures -Filter *.zip) {
    $into = Join-Path $work $zip.BaseName
    New-Item -ItemType Directory -Path $into -Force | Out-Null
    Expand-Archive -Path $zip.FullName -DestinationPath $into -Force
}

"=== reading the timings out of the .ani files ==="
$made = 0
foreach ($ani in Get-ChildItem $source -Filter *.ani | Sort-Object Name) {
    $name = $ani.BaseName
    $unpacked = Join-Path $work $name
    if (-not (Test-Path $unpacked)) { "  $name : no pictures, skipped"; continue }

    $frames = Get-ChildItem $unpacked -Filter *.png | Sort-Object Name
    if ($frames.Count -eq 0) { "  $name : no pictures, skipped"; continue }

    $bytes = [System.IO.File]::ReadAllBytes($ani.FullName)
    $text = [System.Text.Encoding]::ASCII.GetString($bytes)
    $rateAt = $text.IndexOf('rate')
    $seqAt = $text.IndexOf('seq ')
    $anihAt = $text.IndexOf('anih')
    $count = if ($anihAt -ge 0) { [BitConverter]::ToUInt32($bytes, $anihAt + 8 + 4) } else { $frames.Count }
    $steps = if ($anihAt -ge 0) { [BitConverter]::ToUInt32($bytes, $anihAt + 8 + 8) } else { $count }

    $rates = @()
    if ($rateAt -ge 0) {
        $n = [int]([BitConverter]::ToUInt32($bytes, $rateAt + 4) / 4)
        for ($i = 0; $i -lt $n; $i++) { $rates += [BitConverter]::ToUInt32($bytes, $rateAt + 8 + $i * 4) }
    }
    $order = @(0..([Math]::Max(0, $count - 1)))
    if ($seqAt -ge 0) {
        $n = [int]([BitConverter]::ToUInt32($bytes, $seqAt + 4) / 4)
        if ($n -gt 0) { $order = @(); for ($i = 0; $i -lt $n; $i++) { $order += [BitConverter]::ToUInt32($bytes, $seqAt + 8 + $i * 4) } }
    }

    # the hotspot, out of the first .cur inside the .ani
    $hotX = 0; $hotY = 0
    $iconAt = $text.IndexOf('icon')
    if ($iconAt -ge 0) {
        $cur = $iconAt + 8
        $hotX = [BitConverter]::ToUInt16($bytes, $cur + 10)
        $hotY = [BitConverter]::ToUInt16($bytes, $cur + 12)
    }

    $folder = Join-Path $target $name
    if (Test-Path $folder) { Get-ChildItem $folder -Filter 'f*.png' | Remove-Item -Force }
    New-Item -ItemType Directory -Path $folder -Force | Out-Null

    $stepsOut = @()
    for ($s = 0; $s -lt $order.Count; $s++) {
        $which = [int]$order[$s]
        if ($which -ge $frames.Count) { continue }
        $file = 'f{0:D2}.png' -f $s
        $to = Join-Path $folder $file
        Copy-Item $frames[$which].FullName $to -Force
        $ms = if ($s -lt $rates.Count -and $rates[$s] -gt 0) { [Math]::Round($rates[$s] * 1000 / 60) } else { 17 }
        $stepsOut += [pscustomobject]@{ frame = $file; ms = $ms; hotspot = @($hotX, $hotY) }
        $made++
    }
    [System.IO.File]::WriteAllText((Join-Path $folder "$name.json"),
        ([pscustomobject]@{ steps = $stepsOut } | ConvertTo-Json -Depth 4 -Compress))
    "  {0,-11} {1} picture(s), {2} step(s), hotspot ({3},{4})" -f $name, $frames.Count, $stepsOut.Count, $hotX, $hotY
}
"total frames: $made"