#Requires -Version 7.2
[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo 'artifacts' }
$version = ([xml](Get-Content -Raw -LiteralPath (Join-Path $repo 'src/SmartBeacons/SmartBeacons.csproj'))).Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') { throw 'Invalid release version in the project file.' }
$dll = Join-Path $repo 'src/SmartBeacons/bin/Release/net472/SmartBeacons.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Run Build.ps1 to generate the Release DLL first.' }

$assembly = [System.Reflection.AssemblyName]::GetAssemblyName($dll)
$builtVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($dll).ProductVersion
if ($assembly.Name -cne 'SmartBeacons' -or ($builtVersion -split '\+', 2)[0] -cne $version) {
    throw 'The compiled assembly name or version differs from the project. Rebuild before packaging.'
}

$destination = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$zip = Join-Path $destination "SmartBeacons-v$version.zip"
$temporary = Join-Path $destination ".$([Guid]::NewGuid().ToString('N')).zip"
$entry = 'BepInEx/plugins/SmartBeacons/SmartBeacons.dll'

try {
    $archive = [System.IO.Compression.ZipFile]::Open($temporary, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $item = $archive.CreateEntry($entry, [System.IO.Compression.CompressionLevel]::Optimal)
        $item.LastWriteTime = [DateTimeOffset]::new(2025, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $source = [System.IO.File]::OpenRead($dll)
        try {
            $target = $item.Open()
            try { $source.CopyTo($target) }
            finally { $target.Dispose() }
        }
        finally { $source.Dispose() }
    }
    finally { $archive.Dispose() }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($temporary)
    try {
        if ($archive.Entries.Count -ne 1 -or $archive.Entries[0].FullName -cne $entry) {
            throw 'Unexpected installation archive layout.'
        }
        $stream = $archive.Entries[0].Open()
        try { $embeddedHash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash }
        finally { $stream.Dispose() }
        if ($embeddedHash -cne (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash) {
            throw 'Packaged DLL bytes differ from the Release build.'
        }
    }
    finally { $archive.Dispose() }

    [System.IO.File]::Move($temporary, $zip, $true)
    Write-Output $zip
}
finally {
    if (Test-Path -LiteralPath $temporary) { [System.IO.File]::Delete($temporary) }
}
