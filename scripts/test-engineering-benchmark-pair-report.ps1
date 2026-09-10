[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-pair-report-' + [Guid]::NewGuid().ToString('N'))
try {
    foreach ($arm in @('baseline','arifce')) {
        $trial = Join-Path $root $arm
        New-Item -ItemType Directory -Path $trial -Force | Out-Null
        [ordered]@{
            taskId='fixture-task'; fixtureCommit='abcdef0'; model='fixture-model'; tokenBudget=50000; permissionProfile='fixture-profile'; acceptanceContractSha256='contract'; apiContractSha256='api'; fixtureTree='tree'
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $trial 'session.json') -Encoding utf8
        $primary = if ($arm -eq 'baseline') { 60 } else { 50 }
        [ordered]@{
            arm=$arm; candidateChanged=$true
            evaluation=[ordered]@{checksPassed=$true}
            apiCompatibility=[ordered]@{passed=$true}
            independentEvaluation=[ordered]@{taskPassed=$true;assessment=[ordered]@{status='PASSED'}}
            contextEfficiency=[ordered]@{policyPassed=$true;modelToolRounds=4;fileReadTokens=20;repeatedFileContextTokens=0;searchTokens=5;editRetryTokens=0;buildTestTokens=2;arifceOverheadTokens=if($arm -eq 'arifce'){3}else{0};usefulContextRatioEstimate=0.25;contextAmplificationFactorEstimate=4;policyViolations=@()}
            arifceWorkflowPassed=if($arm -eq 'arifce'){$true}else{$null}
            tokenMeasurement=[ordered]@{nonCachedInputTokens=$primary-10;outputTokens=10;primaryTokens=$primary;totalTokens=$primary*4;churnRatio=4}
            timeMeasurement=[ordered]@{hostElapsedMs=1000}
        } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $trial 'result.json') -Encoding utf8
    }
    $output = Join-Path $root 'report.json'
    & (Join-Path $PSScriptRoot 'compare-engineering-benchmark-pair.ps1') -BaselineTrial (Join-Path $root baseline) -ArifceTrial (Join-Path $root arifce) -Output $output -PlusUsedBefore 10 -PlusUsedAfter 12 | Out-Null
    $report = Get-Content -LiteralPath $output -Raw | ConvertFrom-Json
    if (-not $report.comparisonEligible -or $report.successfulTaskTokenComparison.delta -ne -10 -or $report.arifcePreventedCostEstimate -ne 7) { throw 'Eligible pair token comparison is incorrect.' }
    if ($report.codexPlus.deltaPercentagePoints -ne 2) { throw 'Optional Plus snapshot was not preserved.' }
    $arifce = Get-Content -LiteralPath (Join-Path $root 'arifce/result.json') -Raw | ConvertFrom-Json
    $arifce.independentEvaluation.taskPassed = $false
    $arifce.independentEvaluation.assessment.status = 'FAILED'
    $arifce | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $root 'arifce/result.json') -Encoding utf8
    $failedOutput = Join-Path $root 'failed.json'
    & (Join-Path $PSScriptRoot 'compare-engineering-benchmark-pair.ps1') -BaselineTrial (Join-Path $root baseline) -ArifceTrial (Join-Path $root arifce) -Output $failedOutput | Out-Null
    $failed = Get-Content -LiteralPath $failedOutput -Raw | ConvertFrom-Json
    if ($failed.comparisonEligible -or $null -ne $failed.successfulTaskTokenComparison -or $failed.arifce.failedRunTokens -ne 50) { throw 'Failed run entered successful-task token comparison.' }
    Write-Output 'Benchmark matched-pair report success/failure separation passed.'
}
finally { if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
