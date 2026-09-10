[CmdletBinding()]
param(
    [string]$Root = 'artifacts/engineering-benchmark',
    [string]$Manifest = 'benchmarks/engineering-tasks.json',
    [string]$EvaluatorRegistry = 'benchmarks/evaluators.json',
    [string]$Output = 'docs/evidence/engineering-ab-run.json'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-telemetry.ps1')
. (Join-Path $PSScriptRoot 'benchmark-timing.ps1')
. (Join-Path $PSScriptRoot 'benchmark-contract.ps1')
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
$repo = Split-Path -Parent $PSScriptRoot
function Repo-Path([string]$Path) { if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }; return [IO.Path]::GetFullPath((Join-Path $repo $Path)) }
function Hash([string]$Path) { if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Evidence file missing: $Path" }; return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant() }
$suiteRoot = Repo-Path $Root
$definition = Get-Content -LiteralPath (Repo-Path $Manifest) -Raw | ConvertFrom-Json
$repetitions = if ($definition.schemaVersion -eq 3) { [int]$definition.repetitions } else { 1 }
if ($repetitions -lt 1) { throw 'Benchmark repetitions must be positive.' }
$requireTelemetry = $definition.schemaVersion -eq 3 -and [bool]$definition.requireMeasuredTelemetry
$requiredPermissionProfile = if ($definition.schemaVersion -eq 3) { [string]$definition.requiredPermissionProfile } else { $null }
$registryPath = Repo-Path $EvaluatorRegistry
$registryHash = Hash $registryPath
$registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($task in $definition.tasks) {
    for ($trialNumber = 1; $trialNumber -le $repetitions; $trialNumber++) {
      foreach ($arm in @('baseline', 'arifce')) {
        $trial = Join-Path (Join-Path $suiteRoot $task.id) $arm
        if ($repetitions -gt 1) { $trial = Join-Path $trial ('trial-' + $trialNumber.ToString('D2')) }
        & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null
        $resultPath = Join-Path $trial 'result.json'
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        $session = Get-Content -LiteralPath (Join-Path $trial 'session.json') -Raw | ConvertFrom-Json
        $contract = Get-BenchmarkAcceptanceContract $definition $task
        if ($session.acceptanceContractSha256 -cne (Get-BenchmarkContractHash $contract)) { throw "Public evaluator contract mismatch: $($task.id)/$arm" }
        if ($result.taskId -ne $task.id -or $result.arm -ne $arm -or $result.fixtureCommit -ne $definition.fixtureCommit -or [int]$result.trial -ne $trialNumber) { throw "Trial identity mismatch: $($task.id)/$arm/$trialNumber" }
        if ($null -ne $requiredPermissionProfile -and $result.permissionProfile -ne $requiredPermissionProfile) { throw "Permission profile mismatch: $($task.id)/$arm/$trialNumber" }
        if ($requireTelemetry -and ($result.tokenSource -eq 'unavailable' -or $null -eq $result.tokensConsumed -or $null -eq $result.timeMeasurement)) { throw "Measured host timing and token telemetry are required: $($task.id)/$arm/$trialNumber" }
        if ($requireTelemetry -and $null -eq $result.tokenBudgetCompliant) { throw "Measured token-budget compliance is required: $($task.id)/$arm/$trialNumber" }
        if ($null -eq $result.apiCompatibility) { throw "Public API compatibility gate missing: $($task.id)/$arm" }
        $apiGate = & (Join-Path $PSScriptRoot 'run-engineering-api-gate.ps1') -TrialRoot $trial -VerifyOnly
        if (($apiGate.passed | ConvertTo-Json -Compress) -cne ($result.apiCompatibility.passed | ConvertTo-Json -Compress)) { throw "Public API compatibility outcome mismatch: $($task.id)/$arm" }
        if ($null -eq $result.independentEvaluation) { throw "Independent evaluation missing: $($task.id)/$arm" }
        if ($result.independentEvaluation.registrySha256 -ne $registryHash) { throw "Evaluator registry mismatch: $($task.id)/$arm" }
        $sourcePath = Join-Path $trial 'independent-evaluator/IndependentTests.cs'
        $projectPath = Join-Path $trial 'independent-evaluator/IndependentEvaluator.csproj'
        $logPath = Join-Path $trial 'independent-evaluator/evaluator.log'
        if ((Hash $sourcePath) -ne $result.independentEvaluation.injectedSourceSha256 -or (Hash $projectPath) -ne $result.independentEvaluation.projectSha256 -or (Hash $logPath) -ne $result.independentEvaluation.outputSha256) { throw "Independent evaluator artifact mismatch: $($task.id)/$arm" }
        $trxPath = Join-Path $trial 'independent-evaluator/results/evaluator.trx'
        if ($null -eq $result.independentEvaluation.assessment) { throw 'Legacy exit-only evaluations require rerunning before scored collection.' }
        if (-not (Test-Path -LiteralPath $trxPath) -or (Hash $trxPath) -cne $result.independentEvaluation.testResultsSha256) { throw "Missing or changed test results: $($task.id)/$arm" }
        $entry = @($registry.evaluators | Where-Object taskId -eq $task.id)
        if ($entry.Count -ne 1) { throw "Expected one evaluator registry entry for $($task.id)." }
        $assessment = Read-BenchmarkAssessment $trxPath $result.independentEvaluation.exitCode @($entry.methods)
        if ($assessment.status -eq 'ERROR') { throw "Unscorable evaluator error: $($task.id)/$arm. Preserve this run; do not count it as an assertion failure." }
        foreach ($field in @('status','taskPassed','reason')) {
            if (($assessment.$field | ConvertTo-Json -Compress) -cne ($result.independentEvaluation.assessment.$field | ConvertTo-Json -Compress)) { throw "Evaluator assessment mismatch: $($task.id)/$arm" }
        }
        if (($result.independentEvaluation.taskPassed | ConvertTo-Json -Compress) -cne ($assessment.taskPassed | ConvertTo-Json -Compress)) { throw "Evaluator outcome mismatch: $($task.id)/$arm" }
        $workflowPassed = $arm -eq 'baseline' -or [bool]$result.arifceWorkflowPassed
        $comparisonEligible = [bool]$result.candidateChanged -and [bool]$result.evaluation.checksPassed -and [bool]$result.apiCompatibility.passed -and [bool]$result.independentEvaluation.taskPassed -and [bool]$result.contextEfficiency.policyPassed -and $workflowPassed
        $result | Add-Member -NotePropertyName comparisonEligible -NotePropertyValue $comparisonEligible
        $rows.Add($result)
      }
    }
}
if ($rows.Count -ne $definition.tasks.Count * 2 * $repetitions) { throw 'The suite is incomplete.' }
if (@($rows.runId | Sort-Object -Unique).Count -ne $rows.Count) { throw 'Every trial must have a unique run ID.' }
foreach ($task in $definition.tasks) {
    for ($trialNumber = 1; $trialNumber -le $repetitions; $trialNumber++) {
        $pair = @($rows | Where-Object { $_.taskId -eq $task.id -and [int]$_.trial -eq $trialNumber })
        if ($pair.Count -ne 2 -or $pair[0].model -ne $pair[1].model -or $pair[0].tokenBudget -ne $pair[1].tokenBudget -or $pair[0].permissionProfile -ne $pair[1].permissionProfile) { throw "Matched model, token budget or permission profile violation: $($task.id)/$trialNumber" }
    }
}
$baseline = @($rows | Where-Object arm -eq 'baseline')
$arifce = @($rows | Where-Object arm -eq 'arifce')
$baselineUsage = Get-BenchmarkTokenSummary $baseline
$arifceUsage = Get-BenchmarkTokenSummary $arifce
$baselineTime = Get-BenchmarkHostTimeSummary $baseline
$arifceTime = Get-BenchmarkHostTimeSummary $arifce
$report = [ordered]@{
    schemaVersion = 5
    productClaimEligible = $false
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    fixtureCommit = $definition.fixtureCommit
    taskCount = $definition.tasks.Count
    matchedPairCount = $definition.tasks.Count * $repetitions
    repetitions = $repetitions
    permissionProfile = $requiredPermissionProfile
    evaluatorRegistrySha256 = $registryHash
    baseline = $baseline
    arifce = $arifce
    summary = [ordered]@{
        baselineIndependentPasses = @($baseline | Where-Object { $_.independentEvaluation.taskPassed }).Count
        arifceIndependentPasses = @($arifce | Where-Object { $_.independentEvaluation.taskPassed }).Count
        baselineSuccessfulTasks = @($baseline | Where-Object comparisonEligible).Count
        arifceSuccessfulTasks = @($arifce | Where-Object comparisonEligible).Count
        baselineWithinTokenBudget = @($baseline | Where-Object { $_.tokenBudgetCompliant -eq $true }).Count
        arifceWithinTokenBudget = @($arifce | Where-Object { $_.tokenBudgetCompliant -eq $true }).Count
        baselineSuccessfulPrimaryTokens = [long](($baseline | Where-Object comparisonEligible | ForEach-Object { $_.tokenMeasurement.primaryTokens } | Measure-Object -Sum).Sum ?? 0)
        arifceSuccessfulPrimaryTokens = [long](($arifce | Where-Object comparisonEligible | ForEach-Object { $_.tokenMeasurement.primaryTokens } | Measure-Object -Sum).Sum ?? 0)
        baselineFailedPrimaryTokens = [long](($baseline | Where-Object { -not $_.comparisonEligible } | ForEach-Object { $_.tokenMeasurement.primaryTokens } | Measure-Object -Sum).Sum ?? 0)
        arifceFailedPrimaryTokens = [long](($arifce | Where-Object { -not $_.comparisonEligible } | ForEach-Object { $_.tokenMeasurement.primaryTokens } | Measure-Object -Sum).Sum ?? 0)
        baselineTotalTokens = $baselineUsage.totalTokens
        arifceTotalTokens = $arifceUsage.totalTokens
        baselinePrimaryTokens = $baselineUsage.primaryTokens
        arifcePrimaryTokens = $arifceUsage.primaryTokens
        baselineMeasuredTrials = $baselineUsage.availableTrials
        arifceMeasuredTrials = $arifceUsage.availableTrials
        tokenComparisonAvailable = ($null -ne $baselineUsage.totalTokens -and $null -ne $arifceUsage.totalTokens)
        baselineHostTime = $baselineTime
        arifceHostTime = $arifceTime
    }
    interpretation = 'Diagnostic pinned-assertion results only. Successful-task token comparison includes only candidates that pass repository tests, the public API compile gate, independent evaluation, harness policy, and the required ArifCE workflow. The declared token target is reported but is not currently a success gate. Not eligible for product-effectiveness claims until a complete matched study passes.'
}
$outputPath = Repo-Path $Output
New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Output "Collected $($rows.Count) independently evaluated trials across $($definition.tasks.Count * $repetitions) matched pairs into $Output."
