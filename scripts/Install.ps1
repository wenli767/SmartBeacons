#Requires -Version 7.2
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string] $GamePath,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GamePath).Path
if (-not (Test-Path -LiteralPath (Join-Path $game 'Subnautica.exe') -PathType Leaf)) {
    throw 'GamePath must point to the original Windows Subnautica installation.'
}
if (-not (Test-Path -LiteralPath (Join-Path $game 'BepInEx/core/BepInEx.dll') -PathType Leaf)) {
    throw 'Install the Subnautica BepInEx 5 pack before using this script.'
}

$repo = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $repo 'src/SmartBeacons/bin/Release/net472/SmartBeacons.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Run Build.ps1 before using this script.' }
$directory = Join-Path $game 'BepInEx/plugins/SmartBeacons'
$target = Join-Path $directory 'SmartBeacons.dll'
if (Test-Path -LiteralPath $target) {
    if ((Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath $dll).Hash) {
        Write-Output 'This build of Smart Beacons is already installed.'
        return
    }
    if (-not $Force) { throw 'An existing Smart Beacons DLL was found. Use -Force to replace it.' }
}
if ($PSCmdlet.ShouldProcess($target, 'Install Smart Beacons')) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    Copy-Item -LiteralPath $dll -Destination $target -Force:$Force
    Write-Output $target
}
