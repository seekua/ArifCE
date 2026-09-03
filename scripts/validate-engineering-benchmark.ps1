[CmdletBinding()]
param(
    [string]$Manifest = 'benchmarks/engineering-tasks.json',
    [string]$EvaluatorRegistry = 'benchmarks/evaluators.json',
    [string]$Baseline,
    [string]$Arifce,
    [string]$Output = 'docs/evidence/engineering-ab-run.json',
    [switch]$ValidateManifestOnly
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-contract.ps1')
$repo = Split-Path -Parent $PSScriptRoot
function Resolve-RepoPath([string]$Path) { if ([IO.Path]::IsPathRooted($Path)) { return $Path }; return Join-Path $repo $Path }
function Require-Property($Object, [string]$Name, [string]$Context) { if ($null -eq $Object.PSObject.Properties[$Name] -or $null -eq $Object.$Name) { throw "$Context is missing '$Name'." } }

$manifestPath = Resolve-RepoPath $Manifest
$definition = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach ($name in @('schemaVersion','repository','fixtureCommit','minimumTasks','requiredCategories','tasks')) { Require-Property $definition $name 'Manifest' }
if ($definition.schemaVersion -notin @(1,2)) { throw 'Unsupported benchmark manifest schema.' }
if ($definition.tasks.Count -lt $definition.minimumTasks -or $definition.tasks.Count -lt 10) { throw 'The engineering benchmark requires at least 10 tasks.' }
$ids = @($definition.tasks | ForEach-Object id)
if (($ids | Sort-Object -Unique).Count -ne $ids.Count) { throw 'Benchmark task IDs must be unique.' }
$categories = @($definition.tasks | ForEach-Object category | Sort-Object -Unique)
foreach ($category in $definition.requiredCategories) { if ($category -notin $categories) { throw "Required category '$category' is missing." } }
foreach ($task in $definition.tasks) { foreach ($name in @('id','category','instruction','verification')) { Require-Property $task $name "Task $($task.id)" } }
foreach ($task in $definition.tasks) { Get-BenchmarkAcceptanceContract $definition $task | Out-Null }
$evaluatorPath = Resolve-RepoPath $EvaluatorRegistry
$evaluatorDefinition = Get-Content -LiteralPath $evaluatorPath -Raw | ConvertFrom-Json
foreach ($name in @('schemaVersion','policy','evaluators')) { Require-Property $evaluatorDefinition $name 'Evaluator registry' }
if ($evaluatorDefinition.schemaVersion -ne 1) { throw 'Unsupported evaluator registry schema.' }
$evaluatorRows = @($evaluatorDefinition.evaluators)
if ($evaluatorRows.Count -ne $definition.tasks.Count) { throw 'Evaluator registry must contain exactly one evaluator per task.' }
$evaluatorIds = @($evaluatorRows | ForEach-Object taskId | Sort-Object)
if (($evaluatorIds -join "`n") -ne (($ids | Sort-Object) -join "`n")) { throw 'Evaluator task IDs do not match the benchmark manifest.' }
foreach ($evaluator in $evaluatorRows) {
    foreach ($name in @('taskId','sourceCommit','sourceFile','fixture','methods')) { Require-Property $evaluator $name "Evaluator $($evaluator.taskId)" }
    if ($evaluator.sourceCommit -notmatch '^[0-9a-f]{40}$') { throw "Evaluator $($evaluator.taskId) must pin a full Git commit." }
    if ($evaluator.sourceFile -notmatch '^tests/ArifCE\.Tests/[A-Za-z0-9.]+\.cs$') { throw "Evaluator $($evaluator.taskId) has an unsafe source file." }
    if ($evaluator.fixture -notin @('behavior','llm','mcp','safety')) { throw "Evaluator $($evaluator.taskId) has an unknown fixture." }
    if (@($evaluator.methods).Count -eq 0 -or @($evaluator.methods | Where-Object { $_ -notmatch '^[A-Za-z][A-Za-z0-9_]+$' }).Count -gt 0) { throw "Evaluator $($evaluator.taskId) has invalid method names." }
}
if ($ValidateManifestOnly) { Write-Output "Validated engineering benchmark manifest and independent evaluator registry with $($definition.tasks.Count) tasks."; return }
if ([string]::IsNullOrWhiteSpace($Baseline) -or [string]::IsNullOrWhiteSpace($Arifce)) { throw '-Baseline and -Arifce are required unless -ValidateManifestOnly is used.' }

function Read-Arm([string]$Path, [string]$ExpectedArm) {
    $rows = @(Get-Content -LiteralPath (Resolve-RepoPath $Path) -Raw | ConvertFrom-Json)
    if ($rows.Count -ne $definition.tasks.Count) { throw "$ExpectedArm must contain exactly $($definition.tasks.Count) rows." }
    $rowIds = @($rows | ForEach-Object taskId | Sort-Object)
    if (($rowIds -join "`n") -ne (($ids | Sort-Object) -join "`n")) { throw "$ExpectedArm task IDs do not match the manifest." }
    foreach ($row in $rows) {
        foreach ($name in @('taskId','arm','fixtureCommit','model','tokenBudget','success','durationMs','tokensConsumed','filesRead','contextReconstructionMs','repeatedInvestigations','repeatedFailedApproaches','incorrectAssumptions','regressions','handoffRecoveryMs','verificationFailures','notes')) { Require-Property $row $name "$ExpectedArm/$($row.taskId)" }
        if ($row.arm -ne $ExpectedArm) { throw "Task $($row.taskId) has arm '$($row.arm)', expected '$ExpectedArm'." }
        if ($row.fixtureCommit -ne $definition.fixtureCommit) { throw "Task $($row.taskId) does not use fixture commit $($definition.fixtureCommit)." }
        if ([string]::IsNullOrWhiteSpace($row.model) -or $row.tokenBudget -le 0) { throw "Task $($row.taskId) must name a model and positive token budget." }
        foreach ($metric in @('durationMs','tokensConsumed','filesRead','contextReconstructionMs','repeatedInvestigations','repeatedFailedApproaches','incorrectAssumptions','regressions','handoffRecoveryMs','verificationFailures')) { if ($row.$metric -lt 0) { throw "Task $($row.taskId) has negative metric '$metric'." } }
    }
    return $rows
}

$baselineRows = Read-Arm $Baseline 'baseline'
$arifceRows = Read-Arm $Arifce 'arifce'
foreach ($taskId in $ids) {
    $left = $baselineRows | Where-Object taskId -eq $taskId | Select-Object -First 1
    $right = $arifceRows | Where-Object taskId -eq $taskId | Select-Object -First 1
    if ($left.model -ne $right.model -or $left.tokenBudget -ne $right.tokenBudget) { throw "Task $taskId must use the same model and token budget in both arms." }
}

$result = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    repository = $definition.repository
    fixtureCommit = $definition.fixtureCommit
    taskCount = $definition.tasks.Count
    baseline = $baselineRows
    arifce = $arifceRows
    summary = [ordered]@{
        baselineSuccesses = @($baselineRows | Where-Object success -eq $true).Count
        arifceSuccesses = @($arifceRows | Where-Object success -eq $true).Count
        baselineAverageDurationMs = [Math]::Round(($baselineRows | Measure-Object durationMs -Average).Average, 2)
        arifceAverageDurationMs = [Math]::Round(($arifceRows | Measure-Object durationMs -Average).Average, 2)
        baselineTotalTokens = ($baselineRows | Measure-Object tokensConsumed -Sum).Sum
        arifceTotalTokens = ($arifceRows | Measure-Object tokensConsumed -Sum).Sum
    }
    interpretation = 'Matched raw engineering-task measurements. Association is not causation; publish failures and negative results unchanged.'
}
$outputPath = Resolve-RepoPath $Output
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Output "Wrote $Output with $($definition.tasks.Count) matched engineering tasks."
