[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Archive
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Archive -PathType Leaf)) { throw "Archive not found: $Archive" }
$root = Join-Path ([IO.Path]::GetTempPath()) ("arifce-verify-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
try {
    Expand-Archive -LiteralPath $Archive -DestinationPath $root -Force
    $sumFile = Join-Path $root 'SHA256SUMS'
    if (-not (Test-Path -LiteralPath $sumFile)) { throw 'SHA256SUMS is missing from the archive.' }
    $line = Get-Content -LiteralPath $sumFile | Select-Object -First 1
    if ($line -notmatch '^([0-9a-fA-F]{64})\s+(.+)$') { throw 'SHA256SUMS has an invalid format.' }
    $expected = $Matches[1].ToLowerInvariant()
    $binary = Join-Path $root $Matches[2].Trim()
    if (-not (Test-Path -LiteralPath $binary -PathType Leaf)) { throw "Binary named by SHA256SUMS is missing: $($Matches[2])" }
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $binary).Hash.ToLowerInvariant()
    if ($actual -ne $expected) { throw "Checksum mismatch. Expected $expected, got $actual." }
    Write-Output "Verified $([IO.Path]::GetFileName($Archive)): $actual"
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
