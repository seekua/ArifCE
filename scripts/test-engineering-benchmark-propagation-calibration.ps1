[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-propagation-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-propagation-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Once([string]$Text, [string]$Before, [string]$After) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne 1) { throw 'Propagation mutation anchor is not unique; refusing uncalibrated control.' }
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
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkPropagationTests.cs'
    [IO.File]::WriteAllText($testPath, (ConvertTo-BenchmarkPropagationSource ([IO.File]::ReadAllText($testPath))))
    $servicePath = Join-Path $checkout 'src/ArifCE.Infrastructure/ProjectService.cs'
    $original = [IO.File]::ReadAllText($servicePath).Replace("`r`n", "`n")
    $methods = @('Current_metadata_and_scoped_changes_preserve_only_valid_trust', 'Acceptance_keeps_its_original_evidence_basis_after_new_evidence', 'Broken_evidence_or_claim_cannot_leave_acceptance_current', 'Handoff_refreshes_and_repeats_warnings_without_promoting_stale_claims', 'New_acceptance_rejects_foreign_evidence_and_disputed_claim')
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw "Propagation calibration restore failed; see $root" }
        foreach ($variant in @('good', 'never-stale', 'always-stale', 'skip-acceptance-basis', 'forget-review-warning', 'skip-ownership', 'skip-handoff-refresh')) {
            $source = $original
            switch ($variant) {
                'never-stale' { $source = Replace-Once $source 'var isStale = claim.Status == ClaimStatus.Stale || !hasCurrentEvidence;' 'var isStale = claim.Id.Length < 0;' }
                'always-stale' { $source = Replace-Once $source 'var isStale = claim.Status == ClaimStatus.Stale || !hasCurrentEvidence;' 'var isStale = claim.Id.Length >= 0;' }
                'skip-acceptance-basis' { $source = Replace-Once $source 'if (basisIsCurrent) continue;' 'if (basisIsCurrent || !staleClaims.Contains(acceptance.ClaimId)) continue;' }
                'forget-review-warning' { $source = Replace-Once $source 'warnings.Add($"Acceptance {acceptance.Id} needs review; its earlier approval has not been renewed.");' 'warnings.Add("Review pending");' }
                'skip-ownership' { $source = Replace-Once $source '&& string.Equals(evidence.ClaimId, claimId, StringComparison.OrdinalIgnoreCase)' '' }
                'skip-handoff-refresh' { $source = Replace-Once $source 'var trust = await RefreshTrustAsync(root, cancellationToken);' 'var trust = new TrustRefreshResult(0, 0, Array.Empty<string>());' }
            }
            [IO.File]::WriteAllText($servicePath, $source)
            $results = Join-Path $root $variant
            New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $LASTEXITCODE $methods
            $expected = if ($variant -eq 'good') { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "Propagation calibration $variant expected $expected, got $($assessment.status). Logs: $results" }
            Write-Output "Propagation calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true
    Write-Output "Propagation calibration passed: good code and six incorrect variants at $commit. Not a model benchmark."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-propagation-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe propagation calibration cleanup path.' }
    if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
exit 0
