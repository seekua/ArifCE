[CmdletBinding()]
param([ValidateSet('trust-dirty-content','llm-secret-boundary','acceptance-risk-policy','canonical-concurrency','stale-propagation','deterministic-code-graph','change-impact-contract','structured-flight-recorder','mcp-validation','unfinished-verification-policy')][string]$TaskId = 'trust-dirty-content')

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-benchmark-completion-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $manifest = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/engineering-tasks.json') -Raw | ConvertFrom-Json
    $manifest.schemaVersion = 2
    $manifest.fixtureCommit = (& git -C $repo rev-parse HEAD).Trim()
    $manifestPath = Join-Path $root 'manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId $TaskId -Arm baseline -Model fixture-model-v1 -TokenBudget 100 -Manifest $manifestPath -OutputRoot $root | Out-Null
    $trial = Join-Path $root "$TaskId/baseline"
    $checkout = Join-Path $trial 'checkout'
    Set-Content -LiteralPath (Join-Path $checkout 'BENCHMARK-SMOKE.txt') -Value 'candidate change' -Encoding utf8
    & git -C $checkout add BENCHMARK-SMOKE.txt
    & git -C $checkout commit --quiet -m 'Benchmark completion smoke candidate'
    if ($LASTEXITCODE -ne 0) { throw 'Unable to commit smoke candidate.' }
    Push-Location $checkout
    try { & dotnet restore ArifCE.slnx --disable-build-servers --maxcpucount:1 | Out-Null } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw 'Unable to prepare restored assets for the completion smoke candidate.' }
    $rawLog = Join-Path $root 'raw-agent.log'
    # Synthetic host protocol events test ingestion, not product effectiveness.
    Set-Content -LiteralPath $rawLog -Value @('{"type":"thread.started","thread_id":"fixture-thread"}', '{"type":"turn.started"}', '{"type":"turn.completed","usage":{"input_tokens":100,"cached_input_tokens":60,"output_tokens":20}}') -Encoding utf8
    $hostFixture = Join-Path $root 'host.ps1'
    'param([string]$Log); [Console]::In.ReadToEnd() | Out-Null; Get-Content -LiteralPath $Log' | Set-Content -LiteralPath $hostFixture
    & (Join-Path $PSScriptRoot 'invoke-engineering-benchmark-host.ps1') -TrialRoot $trial -Executable (Get-Process -Id $PID).Path -HostArguments @('-NoProfile','-File',$hostFixture,$rawLog) | Out-Null
    $rawLog = Join-Path $trial 'agent.log'
    $manualRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -RawLog $rawLog -TokensConsumed 120 -TokenSource provider | Out-Null } catch { $manualRejected = $true }
    if (-not $manualRejected -or (Test-Path -LiteralPath (Join-Path $trial 'result.json'))) { throw 'Unbound manual token counts were accepted.' }
    & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -RawLog $rawLog -UsageFormat codex-exec-jsonl | Out-Null
    $result = Get-Content -LiteralPath (Join-Path $trial 'result.json') -Raw | ConvertFrom-Json
    if ($null -ne $result.PSObject.Properties['success']) { throw 'Completion must not emit a hand-authored task-success field.' }
    if (-not $result.evaluation.checksPassed -or $result.evaluation.exitCode -ne 0) { throw 'Deterministic evaluator did not pass.' }
    if ($result.tokensConsumed -ne 120 -or $result.tokenSource -ne 'agent-host') { throw 'Host token telemetry was not recorded.' }
    if ($result.tokenBudgetCompliant -ne $true -or $result.tokenMeasurement.primaryTokens -ne 60) { throw 'The declared token ceiling must use non-cached input plus output.' }
    if ($null -eq $result.timeMeasurement -or $result.timeMeasurement.hostExitCode -ne 0 -or $null -ne $result.timeMeasurement.activeWorkMs) { throw 'Host timing was omitted or misclassified.' }
    $result.timeMeasurement.hostElapsedMs++
    $result | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $trial 'result.json') -Encoding utf8
    $timeRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null } catch { $timeRejected = $true }
    if (-not $timeRejected) { throw 'Tampered host elapsed time was accepted.' }
    $result.timeMeasurement.hostElapsedMs--
    $result.tokensConsumed = 999
    $result | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $trial 'result.json') -Encoding utf8
    $counterRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null } catch { $counterRejected = $true }
    if (-not $counterRejected) { throw 'Tampered token total was accepted.' }
    $result.tokensConsumed = 120
    $result.tokenBudgetCompliant = $false
    $result | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $trial 'result.json') -Encoding utf8
    $budgetRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null } catch { $budgetRejected = $true }
    if (-not $budgetRejected) { throw 'Tampered token-budget compliance was accepted.' }
    $result.tokenBudgetCompliant = $true
    $result | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $trial 'result.json') -Encoding utf8
    $registry = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/evaluators.json') -Raw | ConvertFrom-Json
    $registry.evaluators = @($registry.evaluators | Where-Object taskId -eq $TaskId)
    if ($registry.evaluators[0].fixture -notin @('safety','storage','freshness','propagation','graph','contract','flight-recorder','mcp-boundary','verification')) { $registry.evaluators[0].sourceCommit = $manifest.fixtureCommit }
    $registryPath = Join-Path $root 'smoke-evaluators.json'
    $registry | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $registryPath -Encoding utf8
    & (Join-Path $PSScriptRoot 'run-engineering-task-evaluator.ps1') -TrialRoot $trial -EvaluatorRegistry $registryPath -SourceRepository $repo | Out-Null
    $result = Get-Content -LiteralPath (Join-Path $trial 'result.json') -Raw | ConvertFrom-Json
    if (-not $result.independentEvaluation.taskPassed -or $result.independentEvaluation.exitCode -ne 0) { throw 'Independent trusted evaluator did not pass.' }
    if ($result.independentEvaluation.assessment.status -ne 'PASSED' -or [string]::IsNullOrWhiteSpace($result.independentEvaluation.testResultsSha256)) { throw 'Executed test evidence is missing.' }
    $evaluatorProject = Join-Path $trial 'independent-evaluator/IndependentEvaluator.csproj'
    $projectHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $evaluatorProject).Hash.ToLowerInvariant()
    if ($result.independentEvaluation.projectSha256 -ne $projectHash) { throw 'Independent evaluator project provenance is missing or invalid.' }
    Add-Content -LiteralPath (Join-Path $trial 'agent.log') -Value 'tamper'
    $tamperRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null } catch { $tamperRejected = $true }
    if (-not $tamperRejected) { throw 'Tampered provenance was accepted.' }

    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId $TaskId -Arm arifce -Model fixture-model-v1 -TokenBudget 50000 -Manifest $manifestPath -OutputRoot $root | Out-Null
    $unchangedTrial = Join-Path $root "$TaskId/arifce"
    $failedWorkflowLog = Join-Path $root 'failed-arifce-workflow.jsonl'
    $failedWorkflowCommand = 'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll rebuild; context; search; task; claim; verify; handoff'
    $failedWorkflowEvents = @(
        @{ type='thread.started'; thread_id='fixture-arifce-thread' },
        @{ type='turn.started' },
        @{ type='item.completed'; item=@{ type='command_execution'; command=$failedWorkflowCommand; aggregated_output='index failure'; exit_code=1 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll rebuild'; aggregated_output='ok'; exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll context --task TASK-1'; aggregated_output='ok'; exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll search query'; aggregated_output='ok'; exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll claim list'; aggregated_output='ok'; exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll verify'; aggregated_output='ok'; exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll handoff'; aggregated_output='ok'; exit_code=0 } },
        @{ type='turn.completed'; usage=@{ input_tokens=100; cached_input_tokens=60; output_tokens=20 } }
    )
    [IO.File]::WriteAllLines($failedWorkflowLog, @($failedWorkflowEvents | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 6 }))
    & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $unchangedTrial -RawLog $failedWorkflowLog -UsageFormat codex-exec-jsonl -AllowNoCandidate | Out-Null
    $unchanged = Get-Content -LiteralPath (Join-Path $unchangedTrial 'result.json') -Raw | ConvertFrom-Json
    if ($unchanged.candidateChanged -ne $false) { throw 'No-candidate run was not recorded honestly.' }
    if ($unchanged.arifceWorkflowPassed -ne $false) { throw 'Failed ArifCE commands were accepted as a completed product workflow.' }

    $successfulWorkflowTask = if ($TaskId -eq 'trust-dirty-content') { 'llm-secret-boundary' } else { 'trust-dirty-content' }
    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId $successfulWorkflowTask -Arm arifce -Model fixture-model-v1 -TokenBudget 50000 -Manifest $manifestPath -OutputRoot $root | Out-Null
    $successfulWorkflowTrial = Join-Path $root "$successfulWorkflowTask/arifce"
    $successfulWorkflowLog = Join-Path $root 'successful-arifce-workflow.jsonl'
    $successfulWorkflowEvents = [System.Collections.Generic.List[object]]::new()
    $successfulWorkflowEvents.Add(@{ type='thread.started'; thread_id='fixture-successful-arifce-thread' })
    $successfulWorkflowEvents.Add(@{ type='turn.started' })
    foreach ($command in @(
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll rebuild',
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll task create benchmark --risk LOW --objective objective --scope src --scope tests --invariant invariant --done-when TEST_RUN:tests',
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll context --task TASK-0001 --budget 4000',
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll search relevant',
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll claim create complete --task TASK-0001 --risk LOW',
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll verify CLAIM-0001 --command "dotnet test ArifCE.slnx --configuration Release --no-restore"',
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll task complete TASK-0001 --claim CLAIM-0001 --satisfy 1:EVIDENCE-0001',
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll task check TASK-0001',
        'dotnet ./src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll handoff --task TASK-0001'
    )) {
        $successfulWorkflowEvents.Add(@{ type='item.completed'; item=@{ type='command_execution'; command=$command; aggregated_output='ok'; exit_code=0 } })
    }
    $successfulWorkflowEvents.Add(@{ type='turn.completed'; usage=@{ input_tokens=100; cached_input_tokens=60; output_tokens=20 } })
    [IO.File]::WriteAllLines($successfulWorkflowLog, @($successfulWorkflowEvents | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 6 }))
    & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $successfulWorkflowTrial -RawLog $successfulWorkflowLog -UsageFormat codex-exec-jsonl -AllowNoCandidate | Out-Null
    $successfulWorkflow = Get-Content -LiteralPath (Join-Path $successfulWorkflowTrial 'result.json') -Raw | ConvertFrom-Json
    if ($successfulWorkflow.arifceWorkflowPassed -ne $true) { throw 'Independently successful ArifCE workflow operations were not accepted.' }
    Write-Output 'Engineering benchmark completion provenance smoke test passed.'
}
finally {
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $root = [IO.Path]::GetFullPath($root)
    if (-not $root.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($root).StartsWith('arifce-benchmark-completion-', [StringComparison]::Ordinal)) { throw 'Unsafe completion fixture cleanup path.' }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
