<#
.SYNOPSIS
Builds the converter and, with -Test, runs the test suite.

.DESCRIPTION
A thin wrapper over dotnet so the build is one command from any folder. The
output lands in artifacts/bin, which is where the Unity editor window expects
the converter to be.

.PARAMETER Configuration
Debug (the default) or Release.

.PARAMETER Test
Runs the whole test suite once the build succeeded.

.PARAMETER Clean
Cleans the configuration before building it.

.EXAMPLE
tools\build.ps1
tools\build.ps1 -Configuration Release -Test
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $Test,

    [switch] $Clean
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'Top.slnx'

Push-Location $root

try {
    if ($Clean) {
        dotnet clean $solution -c $Configuration

        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    Write-Host "building $solution ($Configuration)"

    dotnet build $solution -c $Configuration

    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    if ($Test) {
        Write-Host 'testing'

        dotnet test $solution -c $Configuration --no-build
    }

    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
