[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselineTrial,
    [Parameter(Mandatory)][string]$ArifceTrial,
    [Parameter(Mandatory)][string]$Output,
    [ValidateRange(-1,100)][int]$PlusUsedBefore = -1,
    [ValidateRange(-1,100)][int]$PlusUsedAfter = -1
)

$ErrorActionPreference = 'Stop'
function Read-Trial([string]$Path, [string]$Arm) {
    $root = [IO.Path]::GetFullPath($Path)
    $result = Get-Content -LiteralPath (Join-Path $root 'result.json') -Raw | ConvertFrom-Json
    if ($result.arm -ne $Arm) { throw "Expected $Arm trial, got $($result.arm)." }
    if ($null -eq $result.tokenMeasurement -or $null -eq $result.contextEfficiency) { throw "$Arm trial lacks measured token/context telemetry." }
    $requirementsComplete = $null -ne $result.independentEvaluation -and [bool]$result.independentEvaluation.taskPassed
    $repositoryTestsPassed = [bool]$result.evaluation.checksPassed
    $apiGatePassed = $null -ne $result.apiCompatibility -and [bool]$result.apiCompatibility.passed
    $independentPassed = $null -ne $result.independentEvaluation -and [bool]$result.independentEvaluation.taskPassed
    $noRegression = $repositoryTestsPassed -and $independentPassed
    $infrastructureClean = [bool]$result.contextEfficiency.policyPassed -and ($Arm -eq 'baseline' -or [bool]$result.arifceWorkflowPassed)
    $eligible = [bool]$result.candidateChanged -and $requirementsComplete -and $repositoryTestsPassed -and $apiGatePassed -and $independentPassed -and $noRegression -and $infrastructureClean
    [pscustomobject][ordered]@{
        arm = $Arm
        evaluatorResult = if ($null -eq $result.independentEvaluation) { 'NOT_RUN' } else { [string]$result.independentEvaluation.assessment.status }
        requirementsComplete = $requirementsComplete
        repositoryTestsPassed = $repositoryTestsPassed
        apiGatePassed = $apiGatePassed
        independentEvaluatorPassed = $independentPassed
        noRegression = $noRegression
        infrastructureClean = $infrastructureClean
        comparisonEligible = $eligible
        nonCachedInput = [long]$result.tokenMeasurement.nonCachedInputTokens
        output = [long]$result.tokenMeasurement.outputTokens
        primaryTokens = [long]$result.tokenMeasurement.primaryTokens
        cacheIncludedTotal = [long]$result.tokenMeasurement.totalTokens
        churnRatio = $result.tokenMeasurement.churnRatio
        modelToolRounds = [int]$result.contextEfficiency.modelToolRounds
        fileReadTokens = [long]$result.contextEfficiency.fileReadTokens
        repeatedFileContextEstimate = [long]$result.contextEfficiency.repeatedFileContextTokens
        searchCost = [long]$result.contextEfficiency.searchTokens
        editRetryCost = [long]$result.contextEfficiency.editRetryTokens
        buildTestCost = [long]$result.contextEfficiency.buildTestTokens
        arifceOverhead = [long]$result.contextEfficiency.arifceOverheadTokens
        usefulContextRatio = $result.contextEfficiency.usefulContextRatioEstimate
        contextAmplificationFactor = $result.contextEfficiency.contextAmplificationFactorEstimate
        totalDurationMs = [long]$result.timeMeasurement.hostElapsedMs
        failedRunTokens = if ($eligible) { $null } else { [long]$result.tokenMeasurement.primaryTokens }
        policyViolations = @($result.contextEfficiency.policyViolations)
    }
}

$baseline = Read-Trial $BaselineTrial baseline
$arifce = Read-Trial $ArifceTrial arifce
$sameIdentity = $true
$leftSession = Get-Content -LiteralPath (Join-Path ([IO.Path]::GetFullPath($BaselineTrial)) 'session.json') -Raw | ConvertFrom-Json
$rightSession = Get-Content -LiteralPath (Join-Path ([IO.Path]::GetFullPath($ArifceTrial)) 'session.json') -Raw | ConvertFrom-Json
foreach ($name in @('taskId','fixtureCommit','model','tokenBudget','permissionProfile','acceptanceContractSha256','apiContractSha256','fixtureTree')) {
    if (($leftSession.$name | ConvertTo-Json -Compress) -cne ($rightSession.$name | ConvertTo-Json -Compress)) { $sameIdentity = $false }
}
if (-not $sameIdentity) { throw 'Trials are not a matched pair with identical task, model, fixture, contracts and host profile.' }
$eligiblePair = $baseline.comparisonEligible -and $arifce.comparisonEligible
$prevented = if ($eligiblePair) { [Math]::Max(0, $baseline.primaryTokens - $arifce.primaryTokens - $arifce.arifceOverhead) } else { $null }
$report = [ordered]@{
    schemaVersion = 1
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    taskId = $leftSession.taskId
    fixtureCommit = $leftSession.fixtureCommit
    model = $leftSession.model
    matchedConditions = $sameIdentity
    comparisonEligible = $eligiblePair
    successfulTaskTokenComparison = if ($eligiblePair) { [ordered]@{ baseline = $baseline.primaryTokens; arifce = $arifce.primaryTokens; delta = $arifce.primaryTokens - $baseline.primaryTokens } } else { $null }
    baseline = $baseline
    arifce = $arifce
    arifcePreventedCostEstimate = $prevented
    codexPlus = if ($PlusUsedBefore -ge 0 -and $PlusUsedAfter -ge 0) { [ordered]@{ usedBeforePercent = $PlusUsedBefore; usedAfterPercent = $PlusUsedAfter; deltaPercentagePoints = $PlusUsedAfter - $PlusUsedBefore; limitation = 'Account-level integer snapshot; not attributable solely to these trials.' } } else { $null }
    metricLimitations = @(
        'Only pairs passing repository tests, public API gate, independent evaluator, regression checks and harness policy are compared as successful tasks.',
        'File/search/edit/build figures estimate tokens from visible JSONL text at four characters per token.',
        'Useful Context Ratio treats first unique file-read and search output as useful; semantic necessity is not observable from host telemetry.',
        'ArifCE prevented cost is a conservative primary-token delta after subtracting visible ArifCE command-output overhead; it is not causal proof.'
    )
}
$outputPath = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Output "Matched pair report written: $outputPath (eligible=$eligiblePair)"
