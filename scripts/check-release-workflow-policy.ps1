$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$releasePath = Join-Path $repo '.github/workflows/release-binaries.yml'
$ciPath = Join-Path $repo '.github/workflows/ci.yml'
$release = Get-Content -Raw -LiteralPath $releasePath
$ci = Get-Content -Raw -LiteralPath $ciPath

$requiredRids = @('win-x64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')
foreach ($rid in $requiredRids) {
    if ($release -notmatch "(?m)^\s+rid:\s+$([regex]::Escape($rid))\s*$") { throw "Release workflow is missing runtime $rid." }
    if ($ci -notmatch "(?m)^\s+rid:\s+$([regex]::Escape($rid))\s*$") { throw "CI workflow is missing runtime $rid." }
}

if ([regex]::Matches($release, '(?m)^\s+contents:\s+write\s*$').Count -ne 1) {
    throw 'Release workflow must grant contents: write to exactly one job.'
}
if ($release -notmatch '(?ms)^permissions:\s*\r?\n\s+contents:\s+read\s*$') {
    throw 'Release workflow must remain read-only by default.'
}
if ($release -match '(?i)--clobber') { throw 'Release assets must never be overwritten automatically.' }
if ($release -notmatch '(?ms)workflow_dispatch:\s*\r?\n\s+inputs:\s*\r?\n\s+tag:') {
    throw 'Manual release runs must require an explicit tag input.'
}
if ($release -notmatch 'Tag .* does not match CLI package version' -or $release -notmatch 'Release asset conflict; refusing to overwrite') {
    throw 'Release workflow is missing tag/version or immutable-asset guards.'
}

Write-Output 'Release workflow policy passed: five targets, scoped write permission, immutable assets, explicit tag.'
