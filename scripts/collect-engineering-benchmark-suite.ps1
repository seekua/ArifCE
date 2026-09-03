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
$registryPath = Repo-Path $EvaluatorRegistry
$registryHash = Hash $registryPath
$registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($task in $definition.tasks) {
    foreach ($arm in @('baseline', 'arifce')) {
        $trial = Join-Path (Join-Path $suiteRoot $task.id) $arm
        & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null
        $resultPath = Join-Path $trial 'result.json'
        $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
        $session = Get-Content -LiteralPath (Join-Path $trial 'session.json') -Raw | ConvertFrom-Json
        $contract = Get-BenchmarkAcceptanceContract $definition $task
        if ($session.acceptanceContractSha256 -cne (Get-BenchmarkContractHash $contract)) { throw "Public evaluator contract mismatch: $($task.id)/$arm" }
        if ($result.taskId -ne $task.id -or $result.arm -ne $arm -or $result.fixtureCommit -ne $definition.fixtureCommit) { throw "Trial identity mismatch: $($task.id)/$arm" }
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
        $rows.Add($result)
    }
}
if ($rows.Count -ne $definition.tasks.Count * 2) { throw 'The suite is incomplete.' }
if (@($rows.runId | Sort-Object -Unique).Count -ne $rows.Count) { throw 'Every trial must have a unique run ID.' }
foreach ($task in $definition.tasks) {
    $pair = @($rows | Where-Object taskId -eq $task.id)
    if ($pair.Count -ne 2 -or $pair[0].model -ne $pair[1].model -or $pair[0].tokenBudget -ne $pair[1].tokenBudget) { throw "Matched model or token budget violation: $($task.id)" }
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
    evaluatorRegistrySha256 = $registryHash
    baseline = $baseline
    arifce = $arifce
    summary = [ordered]@{
        baselineIndependentPasses = @($baseline | Where-Object { $_.independentEvaluation.taskPassed }).Count
        arifceIndependentPasses = @($arifce | Where-Object { $_.independentEvaluation.taskPassed }).Count
        baselineTotalTokens = $baselineUsage.totalTokens
        arifceTotalTokens = $arifceUsage.totalTokens
        baselineMeasuredTrials = $baselineUsage.availableTrials
        arifceMeasuredTrials = $arifceUsage.availableTrials
        tokenComparisonAvailable = ($null -ne $baselineUsage.totalTokens -and $null -ne $arifceUsage.totalTokens)
        baselineHostTime = $baselineTime
        arifceHostTime = $arifceTime
    }
    interpretation = 'Diagnostic pinned-assertion results only. Public contracts disclose partial coverage. Not eligible for product-effectiveness claims.'
}
$outputPath = Repo-Path $Output
New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Output "Collected 20 independently evaluated trials into $Output."
