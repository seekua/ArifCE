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
    $rawLog = Join-Path $root 'raw-agent.log'
    Set-Content -LiteralPath $rawLog -Value 'Fixture agent activity log.' -Encoding utf8
    & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -RawLog $rawLog -TokenSource unavailable | Out-Null
    $result = Get-Content -LiteralPath (Join-Path $trial 'result.json') -Raw | ConvertFrom-Json
    if ($null -ne $result.PSObject.Properties['success']) { throw 'Completion must not emit a hand-authored task-success field.' }
    if (-not $result.evaluation.checksPassed -or $result.evaluation.exitCode -ne 0) { throw 'Deterministic evaluator did not pass.' }
    Add-Content -LiteralPath (Join-Path $trial 'agent.log') -Value 'tamper'
    $tamperRejected = $false
    try { & (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null } catch { $tamperRejected = $true }
    if (-not $tamperRejected) { throw 'Tampered provenance was accepted.' }
    Write-Output 'Engineering benchmark completion provenance smoke test passed.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
