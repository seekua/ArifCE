[CmdletBinding()]
param([string]$SourceCommit = 'HEAD')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-flight-recorder-source.ps1')
$repo = Split-Path -Parent $PSScriptRoot
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-flight-recorder-calibration-' + [Guid]::NewGuid().ToString('N'))
$succeeded = $false
function Replace-Anchor([string]$Text, [string]$Before, [string]$After) {
    if ([regex]::Matches($Text, [regex]::Escape($Before)).Count -ne 1) { throw 'Unexpected flight-recorder mutation anchor count; refusing uncalibrated control.' }
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
    $testPath = Join-Path $checkout 'tests/ArifCE.Tests/BenchmarkFlightRecorderTests.cs'
    [IO.File]::WriteAllText($testPath, (ConvertTo-BenchmarkFlightRecorderSource ([IO.File]::ReadAllText($testPath))))
    $servicePath = Join-Path $checkout 'src/ArifCE.Infrastructure/ProjectService.cs'
    $original = [IO.File]::ReadAllText($servicePath).Replace("`r`n", "`n")
    $methods = @('Recorder_redacts_every_persisted_text_field_and_promotes_failed_attempts', 'Recorder_enforces_terminal_transitions_and_rejects_invalid_links_without_writes', 'Recorder_reserves_a_terminal_step_and_enforces_a_finite_step_budget')
    $filter = ($methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
    Push-Location $checkout
    try {
        & dotnet restore tests/ArifCE.Tests/ArifCE.Tests.csproj --disable-build-servers --maxcpucount:1 *> (Join-Path $root 'restore.log')
        if ($LASTEXITCODE -ne 0) { throw "Flight-recorder calibration restore failed; see $root" }
        foreach ($variant in @('good', 'provider-secret', 'outcome-secret', 'no-attempt-promotion', 'allow-terminal-mutation', 'remove-step-budget', 'accept-unsafe-link')) {
            $service = $original
            switch ($variant) {
                'provider-secret' { $service = Replace-Anchor $service 'var safeProvider = Truncate(redactor.Redact(provider.Trim()).Text, MaxAgentIdentityLength);' 'var safeProvider = Truncate(provider.Trim(), MaxAgentIdentityLength);' }
                'outcome-secret' { $service = Replace-Anchor $service 'var safeOutcome = string.IsNullOrWhiteSpace(outcome) ? outcome : Truncate(redactor.Redact(outcome).Text, MaxAgentIdentityLength);' 'var safeOutcome = string.IsNullOrWhiteSpace(outcome) ? outcome : Truncate(outcome, MaxAgentIdentityLength);' }
                'no-attempt-promotion' { $service = Replace-Anchor $service 'if (kind == AgentStepKind.Attempt && IsFailedOutcome(outcome, exitCode) && !string.IsNullOrWhiteSpace(run.TaskId))' 'if (false && kind == AgentStepKind.Attempt && IsFailedOutcome(outcome, exitCode) && !string.IsNullOrWhiteSpace(run.TaskId))' }
                'allow-terminal-mutation' {
                    $service = Replace-Anchor $service 'if (run.Status != AgentRunStatus.Running) throw new InvalidOperationException($"Run {id} is already {run.Status}.");' '_ = run.Status;'
                    $service = Replace-Anchor $service 'if (current.Status != AgentRunStatus.Running) throw new InvalidOperationException($"Run {id} is already {current.Status}.");' '_ = current.Status;'
                }
                'remove-step-budget' {
                    $service = Replace-Anchor $service 'EnsureAgentRunCapacity(run, kind);' '_ = kind;'
                    $service = Replace-Anchor $service 'EnsureAgentRunCapacity(current, kind);' '_ = current.Steps.Count;'
                }
                'accept-unsafe-link' { $service = Replace-Anchor $service 'if (links.Count > 64 || links.Any(link => !IsRepositoryEntityId(link))) throw new ArgumentException("Related IDs must contain at most 64 valid repository entity IDs.", nameof(relatedIds));' '_ = relatedIds;' }
            }
            [IO.File]::WriteAllText($servicePath, $service)
            $results = Join-Path $root $variant
            New-Item -ItemType Directory -Path $results | Out-Null
            & dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj --configuration Release --no-restore --disable-build-servers --maxcpucount:1 --filter $filter --logger 'trx;LogFileName=evaluator.trx' --results-directory $results *> (Join-Path $results 'run.log')
            $assessment = Read-BenchmarkAssessment (Join-Path $results 'evaluator.trx') $LASTEXITCODE $methods
            $expected = if ($variant -eq 'good') { 'PASSED' } else { 'FAILED' }
            if ($assessment.status -ne $expected) { throw "Flight-recorder calibration $variant expected $expected, got $($assessment.status). Logs: $results" }
            Write-Output "Flight-recorder calibration $variant : $($assessment.status) (expected $expected)"
        }
    } finally { Pop-Location }
    $succeeded = $true
    Write-Output "Flight-recorder calibration passed: good code and six incorrect variants at $commit. Not a model benchmark."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-flight-recorder-calibration-', [StringComparison]::Ordinal)) { throw 'Unsafe flight-recorder calibration cleanup path.' }
    if ($succeeded -and (Test-Path -LiteralPath $resolved)) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
exit 0
