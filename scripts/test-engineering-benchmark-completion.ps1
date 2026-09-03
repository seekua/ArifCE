[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-benchmark-completion-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $manifest = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/engineering-tasks.json') -Raw | ConvertFrom-Json
    $manifest.fixtureCommit = (& git -C $repo rev-parse HEAD).Trim()
    $manifestPath = Join-Path $root 'manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId 'trust-dirty-content' -Arm baseline -Model fixture-model-v1 -TokenBudget 50000 -Manifest $manifestPath -OutputRoot $root | Out-Null
    $trial = Join-Path $root 'trust-dirty-content/baseline'
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
    $manualRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -RawLog $rawLog -TokensConsumed 120 -TokenSource provider | Out-Null } catch { $manualRejected = $true }
    if (-not $manualRejected -or (Test-Path -LiteralPath (Join-Path $trial 'result.json'))) { throw 'Unbound manual token counts were accepted.' }
    & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -RawLog $rawLog -UsageFormat codex-exec-jsonl | Out-Null
    $result = Get-Content -LiteralPath (Join-Path $trial 'result.json') -Raw | ConvertFrom-Json
    if ($null -ne $result.PSObject.Properties['success']) { throw 'Completion must not emit a hand-authored task-success field.' }
    if (-not $result.evaluation.checksPassed -or $result.evaluation.exitCode -ne 0) { throw 'Deterministic evaluator did not pass.' }
    if ($result.tokensConsumed -ne 120 -or $result.tokenSource -ne 'agent-host') { throw 'Host token telemetry was not recorded.' }
    $result.tokensConsumed = 999
    $result | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $trial 'result.json') -Encoding utf8
    $counterRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null } catch { $counterRejected = $true }
    if (-not $counterRejected) { throw 'Tampered token total was accepted.' }
    $result.tokensConsumed = 120
    $result | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $trial 'result.json') -Encoding utf8
    $registry = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/evaluators.json') -Raw | ConvertFrom-Json
    $registry.evaluators = @($registry.evaluators | Where-Object taskId -eq 'trust-dirty-content')
    $registry.evaluators[0].sourceCommit = $manifest.fixtureCommit
    $registryPath = Join-Path $root 'smoke-evaluators.json'
    $registry | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $registryPath -Encoding utf8
    & (Join-Path $PSScriptRoot 'run-engineering-task-evaluator.ps1') -TrialRoot $trial -EvaluatorRegistry $registryPath -SourceRepository $repo | Out-Null
    $result = Get-Content -LiteralPath (Join-Path $trial 'result.json') -Raw | ConvertFrom-Json
    if (-not $result.independentEvaluation.taskPassed -or $result.independentEvaluation.exitCode -ne 0) { throw 'Independent trusted evaluator did not pass.' }
    $evaluatorProject = Join-Path $trial 'independent-evaluator/IndependentEvaluator.csproj'
    $projectHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $evaluatorProject).Hash.ToLowerInvariant()
    if ($result.independentEvaluation.projectSha256 -ne $projectHash) { throw 'Independent evaluator project provenance is missing or invalid.' }
    Add-Content -LiteralPath (Join-Path $trial 'agent.log') -Value 'tamper'
    $tamperRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null } catch { $tamperRejected = $true }
    if (-not $tamperRejected) { throw 'Tampered provenance was accepted.' }

    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId 'trust-dirty-content' -Arm arifce -Model fixture-model-v1 -TokenBudget 50000 -Manifest $manifestPath -OutputRoot $root | Out-Null
    $unchangedTrial = Join-Path $root 'trust-dirty-content/arifce'
    & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $unchangedTrial -RawLog $rawLog -TokenSource unavailable -AllowNoCandidate | Out-Null
    $unchanged = Get-Content -LiteralPath (Join-Path $unchangedTrial 'result.json') -Raw | ConvertFrom-Json
    if ($unchanged.candidateChanged -ne $false) { throw 'No-candidate run was not recorded honestly.' }
    if ($null -ne $unchanged.tokensConsumed) { throw 'Unavailable usage must be null rather than zero.' }
    Write-Output 'Engineering benchmark completion provenance smoke test passed.'
}
finally {
    $tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $root = [IO.Path]::GetFullPath($root)
    if (-not $root.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($root).StartsWith('arifce-benchmark-completion-', [StringComparison]::Ordinal)) { throw 'Unsafe completion fixture cleanup path.' }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
