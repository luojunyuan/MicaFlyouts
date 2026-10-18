[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PublishDirectory,

    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory)]
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$archivePath = Join-Path $publishPath "MicaFlyouts-$Version-$RuntimeIdentifier.zip"
$temporaryArchivePath = "$archivePath.tmp"
$requiredFiles = @(
    'MicaFlyouts.exe',
    'Updater.exe',
    'Microsoft.WindowsAppRuntime.Bootstrap.dll'
)

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishPath $requiredFile) -PathType Leaf)) {
        throw "The publish directory is missing required file '$requiredFile'."
    }
}

$files = @(
    Get-ChildItem -LiteralPath $publishPath -Recurse -File |
        Where-Object { $_.Extension -notin @('.pdb', '.xml', '.zip', '.tmp') } |
        Sort-Object FullName
)
$directories = @(
    Get-ChildItem -LiteralPath $publishPath -Recurse -Directory |
        Sort-Object FullName
)
$separator = [string][char]92

function Get-ArchiveRelativePath([string]$path) {
    return $path.Substring($publishPath.Length).TrimStart([char]92, [char]47).Replace($separator, '/')
}

try {
    $archive = [System.IO.Compression.ZipFile]::Open(
        $temporaryArchivePath,
        [System.IO.Compression.ZipArchiveMode]::Create
    )
    try {
        foreach ($directory in $directories) {
            $entryName = (Get-ArchiveRelativePath $directory.FullName) + '/'
            [void]$archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::NoCompression)
        }

        foreach ($file in $files) {
            $entryName = Get-ArchiveRelativePath $file.FullName
            [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $file.FullName,
                $entryName,
                [System.IO.Compression.CompressionLevel]::Optimal
            )
        }
    }
    finally {
        $archive.Dispose()
    }

    $verification = [System.IO.Compression.ZipFile]::OpenRead($temporaryArchivePath)
    try {
        $invalidPaths = @($verification.Entries | Where-Object { $_.FullName.IndexOf([char]92) -ge 0 })
        if ($invalidPaths.Count -gt 0) {
            throw "The archive contains non-standard backslash paths: $($invalidPaths[0].FullName)"
        }

        foreach ($requiredFile in $requiredFiles) {
            if (-not ($verification.Entries | Where-Object { $_.FullName -ceq $requiredFile })) {
                throw "The archive is missing required root file '$requiredFile'."
            }
        }
    }
    finally {
        $verification.Dispose()
    }

    Move-Item -LiteralPath $temporaryArchivePath -Destination $archivePath -Force
    Write-Output $archivePath
}
catch {
    Remove-Item -LiteralPath $temporaryArchivePath -Force -ErrorAction SilentlyContinue
    throw
}
