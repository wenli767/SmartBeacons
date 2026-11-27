#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $GamePath,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repo 'SmartBeacons.sln'
$managed = @()
if ($GamePath) {
    $game = (Resolve-Path -LiteralPath $GamePath).Path
    if (-not (Test-Path -LiteralPath (Join-Path $game 'Subnautica.exe') -PathType Leaf)) {
        throw 'GamePath must point to the original Windows Subnautica installation.'
    }
    $directory = Join-Path $game 'Subnautica_Data/Managed'
    foreach ($file in @('Assembly-CSharp.dll', 'Assembly-CSharp-firstpass.dll', 'Unity.TextMeshPro.dll', 'UnityEngine.UI.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $directory $file) -PathType Leaf)) {
            throw "Missing game reference: $(Join-Path $directory $file)"
        }
    }
    $managed = @("-p:GameManagedDir=$directory")
}

Push-Location $repo
try {
    $dotnet = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source
    & $dotnet restore $solution --locked-mode @managed
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
    & $dotnet build $solution -c Release --no-restore --nologo @managed
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    if (-not $SkipTests) {
        & $dotnet test $solution -c Release --no-build --no-restore --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Automated tests failed.' }
    }
}
finally {
    Pop-Location
}
