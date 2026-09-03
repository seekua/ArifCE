[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-contract-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-contract-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Anchor([string]$Text, [string]$Before, [string]$After) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne 1) { throw 'Unexpected contract mutation anchor count; refusing uncalibrated control.' }
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
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkContractTests.cs'
    [IO.File]::WriteAllText($testPath, (ConvertTo-BenchmarkContractSource ([IO.File]::ReadAllText($testPath))))
    $servicePath = Join-Path $checkout 'src/ArifCE.Infrastructure/ProjectService.cs'
    $scopePath = Join-Path $checkout 'src/ArifCE.Infrastructure/EvidenceScopeTracker.cs'
    $originalService = [IO.File]::ReadAllText($servicePath).Replace("`r`n", "`n")
    $originalScope = [IO.File]::ReadAllText($scopePath).Replace("`r`n", "`n")
    $methods = @('Contract_persists_linkage_confidence_history_and_risk_requirements', 'Contract_rejects_invalid_target_or_link_before_execution_and_writes', 'Contract_evidence_uses_qualified_scope_and_existing_acceptance_lifecycle', 'Contract_project_scope_tracks_exact_transitive_dependents')
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw "Contract calibration restore failed; see $root" }
        foreach ($variant in @('good', 'foreign-claim', 'promote-confidence', 'drop-invariants', 'drop-human-requirement', 'ignore-additional-scope', 'omit-closure-digest')) {
            $service = $originalService
            $scope = $originalScope
            switch ($variant) {
                'foreign-claim' { $service = Replace-Anchor $service 'if (!contract.ClaimId.Equals(claim.Id, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Change contract {contract.Id} is linked to claim {contract.ClaimId}, not {claim.Id}.");' '_ = contract.ClaimId;' }
                'promote-confidence' { $service = Replace-Anchor $service 'return "HEURISTIC";' 'return "EXACT";' }
                'drop-invariants' { $service = Replace-Anchor $service 'history, invariants ?? [], required.Distinct' 'history, [], required.Distinct' }
                'drop-human-requirement' { $service = Replace-Anchor $service 'if (policy.HumanApproval) required.Add("Record explicit human acceptance with rationale.");' '_ = policy.HumanApproval;' }
                'ignore-additional-scope' { $scope = Replace-Anchor $scope 'closure.Paths.Concat(additionalPaths ?? [])' 'closure.Paths.Concat(Array.Empty<string>())' }
                'omit-closure-digest' { $scope = Replace-Anchor $scope 'var dependencies = new List<EvidenceDependency> { new($"symbol:{contract.Target}", closure.Digest, "CODE_GRAPH_CLOSURE") };' 'var dependencies = new List<EvidenceDependency>();' }
            }
            [IO.File]::WriteAllText($servicePath, $service)
            [IO.File]::WriteAllText($scopePath, $scope)
            $results = Join-Path $root $variant
            New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $LASTEXITCODE $methods
            $expected = if ($variant -eq 'good') { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "Contract calibration $variant expected $expected, got $($assessment.status). Logs: $results" }
            Write-Output "Contract calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true
    Write-Output "Contract calibration passed: good code and six incorrect variants at $commit. Not a model benchmark."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-contract-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe contract calibration cleanup path.' }
    if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
exit 0
