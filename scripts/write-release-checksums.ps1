[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ArchiveDirectory,
    [switch]$VerifyArchives
)

$ErrorActionPreference = 'Stop'
$expectedRids = @('win-x64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')
$directory = (Resolve-Path -LiteralPath $ArchiveDirectory).Path
$archives = @(Get-ChildItem -LiteralPath $directory -Filter 'arifce-*.zip' -File | Sort-Object Name)

if ($archives.Count -ne $expectedRids.Count) {
    throw "Expected $($expectedRids.Count) release archives, found $($archives.Count)."
}

foreach ($rid in $expectedRids) {
    $expectedName = "arifce-$rid.zip"
    if (-not ($archives.Name -contains $expectedName)) {
        throw "Required release archive is missing: $expectedName"
    }
}

if ($VerifyArchives) {
    $verifier = Join-Path $PSScriptRoot 'verify-release-artifacts.ps1'
    foreach ($archive in $archives) {
        & $verifier -Archive $archive.FullName
    }
}

$lines = foreach ($archive in $archives) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive.FullName).Hash.ToLowerInvariant()
    "{0}  {1}" -f $hash, $archive.Name
}
$checksumPath = Join-Path $directory 'SHA256SUMS'
$lines | Set-Content -LiteralPath $checksumPath -Encoding ascii

foreach ($line in Get-Content -LiteralPath $checksumPath) {
    if ($line -notmatch '^([0-9a-f]{64})\s{2}(.+\.zip)$') { throw "Invalid release checksum entry: $line" }
    $archivePath = Join-Path $directory $Matches[2]
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
    if ($actual -ne $Matches[1]) { throw "Release checksum mismatch for $($Matches[2])." }
}

Write-Output "Verified $($archives.Count) release archives and wrote $checksumPath"
