[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-freshness-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-freshness-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Once([string]$Text, [string]$Before, [string]$After) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne 1) { throw 'Freshness mutation anchor is not unique; refusing uncalibrated control.' }
    return $Text.Replace($Before, $After)
}
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $commit = (& git -C $repo rev-parse "$SourceCommit^{commit}").Trim()
    if ($LASTEXITCODE -ne 0 -or $commit -notmatch '^[0-9a-f]{40}$') { throw 'Cannot resolve calibration source.' }
    $archive = Join-Path $root 'source.zip'
    & git -C $repo archive --format=zip "--output=$archive" $commit
    if ($LASTEXITCODE -ne 0) { throw 'Unable to export calibration source.' }
    $checkout = Join-Path $root 'checkout'
    Expand-Archive -LiteralPath $archive -DestinationPath $checkout
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkFreshnessTests.cs'
    [IO.File]::WriteAllText($testPath, (ConvertTo-BenchmarkFreshnessSource ([IO.File]::ReadAllText($testPath))))
    $storagePath = Join-Path $checkout 'src/ArifCE.Infrastructure/Services.cs'
    $domainPath = Join-Path $checkout 'src/ArifCE.Core/Domain.cs'
    $storageOriginal = [IO.File]::ReadAllText($storagePath).Replace("`r`n", "`n")
    $domainOriginal = [IO.File]::ReadAllText($domainPath).Replace("`r`n", "`n")
    $methods = @('Freshness_tracks_nested_untracked_bytes', 'Freshness_tracks_literal_paths_and_renames', 'Freshness_distinguishes_current_stale_unknown_and_git_failure', 'Freshness_rejects_unexpanded_git_directories')
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw "Freshness calibration restore failed; see $root" }
        foreach ($variant in @('good', 'path-only', 'always-current', 'always-stale', 'ignore-untracked', 'include-internal-metadata', 'directory-as-missing')) {
            $storage = $storageOriginal; $domain = $domainOriginal
            switch ($variant) {
                'path-only' { $storage = Replace-Once $storage 'SHA256.HashData(File.ReadAllBytes(full))' 'SHA256.HashData(Encoding.UTF8.GetBytes(relative))' }
                'always-current' { $domain = Replace-Once $domain ': EvidenceFreshness.Stale;' ': EvidenceFreshness.Current;' }
                'always-stale' { $domain = Replace-Once $domain '? EvidenceFreshness.Current' '? EvidenceFreshness.Stale' }
                'ignore-untracked' { $storage = Replace-Once $storage '--untracked-files=all' '--untracked-files=no' }
                'include-internal-metadata' { $storage = Replace-Once $storage 'paths.Where(path => !IsInternalArifcePath(path))' 'paths.Where(path => true)' }
                'directory-as-missing' { $storage = Replace-Once $storage 'if (Directory.Exists(full)) throw new InvalidOperationException("Git reported a directory whose contents cannot be safely snapshotted.");' '// calibration: permit directories to masquerade as missing files' }
            }
            [IO.File]::WriteAllText($storagePath, $storage)
            [IO.File]::WriteAllText($domainPath, $domain)
            $results = Join-Path $root $variant
            New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $LASTEXITCODE $methods
            $expected = if ($variant -eq 'good') { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "Freshness calibration $variant expected $expected, got $($assessment.status). Logs: $results" }
            Write-Output "Freshness calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true
    Write-Output "Freshness calibration passed: good code and six incorrect variants at $commit. Not a model benchmark."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-freshness-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe freshness calibration cleanup path.' }
    if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
exit 0
