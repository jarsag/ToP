<#
.SYNOPSIS
Runs the converter: whole families, one unit, or a listing of what a client offers.

.DESCRIPTION
Finds the converter built by tools\build.ps1 and runs it from the repository
root, so a relative --out means the same folder here as it does in Unity. Paths
on the command line are passed through unchanged. With no arguments at all the
converter's own wizard runs, which is the point of the script: no need to know
the command line to browse a client.

.PARAMETER Source
The original client root - the folder holding scripts/table beside model or map.

.PARAMETER Out
The converted tree root. Default artifacts/content, which is where the Unity
preview reads from.

.PARAMETER Kinds
Whole families to convert: character, item, scene, table, map.

.PARAMETER Kind
One family to convert a single unit of, with -Unit.

.PARAMETER Unit
The one unit of -Kind: an id where the family numbers its units, a name
otherwise - a map by its file name.

.PARAMETER List
What to list instead of converting: clients or catalog.

.PARAMETER Near
Where -List clients looks. Default the repository root.

.PARAMETER Json
Prints a listing as JSON, the shape the Unity editor window reads.

.PARAMETER Clients
Shorthand for -List clients.

.PARAMETER Catalog
Shorthand for -List catalog.

.PARAMETER Pick
Runs the interactive wizard even when other arguments are present.

.PARAMETER NoObjects
Converts a map without the objects standing on it. By default a map brings
them, because a map with nothing on it is bare ground.

.PARAMETER Configuration
Debug (the default) or Release: which build of the converter to run.

.PARAMETER Build
Builds before running, whether or not the output is already there.

.PARAMETER Test
With -Build, runs the test suite as part of the build.

.EXAMPLE
tools\convert.ps1
Opens the wizard: pick a client, a category, then one unit.

.EXAMPLE
tools\convert.ps1 -Clients
Lists the client roots found around the repository.

.EXAMPLE
tools\convert.ps1 -Source 'C:\work\TalesOfPirateDX9\Client' -Kind map -Unit PKmap
Converts one map, plus the tables it cannot be read without.

.EXAMPLE
tools\convert.ps1 -Source 'C:\work\TalesOfPirateDX9\Client' -Kinds map,scene -Build
Converts every map and every scene object, building first.
#>
[CmdletBinding()]
param(
    [string] $Source,

    [string] $Out,

    [string[]] $Kinds,

    [string] $Kind,

    [string] $Unit,

    [string] $List,

    [string] $Near,

    [switch] $Json,

    [switch] $Clients,

    [switch] $Catalog,

    [switch] $Pick,

    [switch] $NoObjects,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $Build,

    [switch] $Test
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$configuration = $Configuration.ToLowerInvariant()
$dll = Join-Path $root "artifacts/bin/Top.Conversion.Cli/$configuration/Top.Conversion.Cli.dll"

if ($Clients) {
    $List = 'clients'
}

if ($Catalog) {
    $List = 'catalog'
}

if ($Build -or -not (Test-Path $dll)) {
    & (Join-Path $PSScriptRoot 'build.ps1') -Configuration $Configuration -Test:$Test

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$arguments = @()

if ($Source) {
    $arguments += @('--source', $Source)
}

if ($Out) {
    $arguments += @('--out', $Out)
}

if ($Kinds) {
    $arguments += @('--kinds', ($Kinds -join ','))
}

if ($Kind) {
    $arguments += @('--kind', $Kind)
}

if ($Unit) {
    $arguments += @('--unit', $Unit)
}

if ($List) {
    $arguments += @('--list', $List)
}

if ($Near) {
    $arguments += @('--near', $Near)
}

if ($Json) {
    $arguments += '--json'
}

if ($Pick) {
    $arguments += '--pick'
}

if ($NoObjects) {
    $arguments += '--no-objects'
}

Push-Location $root

try {
    if ($arguments.Count -eq 0) {
        dotnet $dll
    }
    else {
        dotnet $dll @arguments
    }

    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
