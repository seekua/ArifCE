[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-benchmark-smoke-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $fixtureCommit = (& git -C $repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $fixtureCommit -notmatch '^[0-9a-f]{40}$') { throw 'Unable to resolve the CI fixture commit.' }
    $manifest = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/engineering-tasks.json') -Raw | ConvertFrom-Json
    $manifest.fixtureCommit = $fixtureCommit
    $manifestPath = Join-Path $root 'smoke-manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    foreach ($arm in @('baseline', 'arifce')) {
        & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') `
            -TaskId 'trust-dirty-content' `
            -Arm $arm `
            -Model 'fixture-model-v1' `
            -TokenBudget 50000 `
            -Manifest $manifestPath `
            -OutputRoot $root | Out-Null
        $trial = Join-Path (Join-Path $root 'trust-dirty-content') $arm
        $session = Get-Content -LiteralPath (Join-Path $trial 'session.json') -Raw | ConvertFrom-Json
        $checkout = Join-Path $trial 'checkout'
        if ($session.state -ne 'PREPARED' -or $session.arm -ne $arm) { throw "$arm session metadata is invalid." }
        $historyCount = [int](& git -C $checkout rev-list --count HEAD)
        $remoteCount = @(& git -C $checkout remote | Where-Object { $_ }).Count
        if ($LASTEXITCODE -ne 0 -or $historyCount -ne 1 -or $remoteCount -ne 0) { throw "$arm checkout is not isolated." }
        $checkoutTree = (& git -C $checkout rev-parse 'HEAD^{tree}').Trim()
        if ($LASTEXITCODE -ne 0 -or $session.fixtureTree -ne $checkoutTree -or $session.isolatedHistoryCount -ne 1) { throw "$arm isolation metadata is invalid." }
        $status = @(& git -C $checkout status --short)
        if ($LASTEXITCODE -ne 0 -or $status.Count -ne 0) { throw "$arm checkout is not clean." }
    }

    $baseline = Get-Content -LiteralPath (Join-Path $root 'trust-dirty-content/baseline/session.json') -Raw | ConvertFrom-Json
    $arifce = Get-Content -LiteralPath (Join-Path $root 'trust-dirty-content/arifce/session.json') -Raw | ConvertFrom-Json
    if ($baseline.fixtureTree -ne $arifce.fixtureTree -or $baseline.isolatedCommit -ne $arifce.isolatedCommit) {
        throw 'Matched arms do not have identical fixture snapshots.'
    }
    $duplicateRejected = $false
    try {
        & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId 'trust-dirty-content' -Arm 'baseline' -Model 'fixture-model-v1' -TokenBudget 50000 -Manifest $manifestPath -OutputRoot $root | Out-Null
    }
    catch { $duplicateRejected = $true }
    if (-not $duplicateRejected) { throw 'An existing trial was overwritten.' }
    Write-Output 'Engineering benchmark trial isolation smoke test passed.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
