[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-storage-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-storage-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Once([string]$Text, [string]$Before, [string]$After) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne 1) { throw 'Storage mutation anchor is not unique; refusing uncalibrated control.' }
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
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkStorageTests.cs'
    [IO.File]::WriteAllText($testPath, (ConvertTo-BenchmarkStorageSource ([IO.File]::ReadAllText($testPath))))
    $storagePath = Join-Path $checkout 'src/ArifCE.Infrastructure/Services.cs'
    $original = [IO.File]::ReadAllText($storagePath).Replace("`r`n", "`n")
    $methods = @('Separate_processes_preserve_claim_links_and_distinct_ids', 'Deleted_index_rebuild_preserves_canonical_bytes_and_retrieval')
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    $read = '        var current = await ReadAsync<T>(root, directory, id, cancellationToken) ?? throw new InvalidOperationException($"{id} was not found.");'
    $lockAndRead = '        await using var mutationLock = await FileMutationLock.AcquireAsync(root, directory, id, cancellationToken);' + "`n" + $read
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw "Storage calibration restore failed; see $root" }
        foreach ($variant in @('good', 'stale-id-scan', 'stale-id-scan-without-target-check', 'unlocked-update', 'discard-update', 'omit-failed-attempts', 'rewrite-canonical-on-rebuild')) {
            $source = $original
            if ($variant.StartsWith('stale-id-scan', [StringComparison]::Ordinal)) {
                # Force the legitimately possible stale maximum to zero, deterministically.
                # Correct code must still skip every already-committed canonical ID.
                $source = Replace-Once $source '.DefaultIfEmpty().Max();' '.Select(_ => 0).DefaultIfEmpty().Max();'
                if ($variant -eq 'stale-id-scan-without-target-check') {
                    $source = Replace-Once $source 'if (!File.Exists(Path.Combine(folder, id.ToLowerInvariant() + ".json"))) return id;' 'if (id.Length > 0) return id; // deliberately reuse a committed ID'
                }
            }
            switch ($variant) {
                'unlocked-update' { $source = Replace-Once $source $lockAndRead $read }
                'discard-update' { $source = Replace-Once $source 'var updated = update(current);' 'var updated = current; // deliberately discard callback and mutation' }
                'omit-failed-attempts' {
                    $source = Replace-Once $source '.Where(IsCanonical).Order(StringComparer.Ordinal)' '.Where(IsCanonical).Where(path => !path.Contains("attempts", StringComparison.Ordinal)).Order(StringComparer.Ordinal)'
                }
                'rewrite-canonical-on-rebuild' {
                    $anchor = '        await WriteManifestAsync(root, cancellationToken);'
                    # This line occurs in two methods; use the following signature as part of the exact anchor.
                    $before = $anchor + "`n    }`n`n    public async Task UpdateIncrementalAsync"
                    $after = '        foreach (var claim in Directory.EnumerateFiles(Path.Combine(root, ".arifce", "claims"), "*.json")) await File.AppendAllTextAsync(claim, " ", cancellationToken);' + "`n" + $before
                    $source = Replace-Once $source $before $after
                }
            }
            [IO.File]::WriteAllText($storagePath, $source)
            $results = Join-Path $root $variant
            New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $LASTEXITCODE $methods
            $expected = if ($variant -in @('good', 'stale-id-scan')) { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "Storage calibration $variant expected $expected, got $($assessment.status). Logs: $results" }
            Write-Output "Storage calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true
    Write-Output "Storage calibration passed: good code, forced stale-scan success and five incorrect variants at $commit. Not a model benchmark."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-storage-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe storage calibration cleanup path.' }
    if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
exit 0
