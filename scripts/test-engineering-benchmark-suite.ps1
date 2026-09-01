[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-suite-smoke-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $manifest = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/engineering-tasks.json') -Raw | ConvertFrom-Json
    $manifest.fixtureCommit = (& git -C $repo rev-parse HEAD).Trim()
    $manifest.tasks = @($manifest.tasks | Select-Object -First 1)
    $manifest.minimumTasks = 1
    $manifest.requiredCategories = @($manifest.tasks[0].category)
    $manifestPath = Join-Path $root 'manifest.json'
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $suite = Join-Path $root 'suite'
    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-suite.ps1') -Model fixture-v1 -TokenBudget 1000 -Manifest $manifestPath -OutputRoot $suite | Out-Null
    if (@(Get-ChildItem -LiteralPath $suite -Filter session.json -Recurse).Count -ne 2) { throw 'Suite preparer did not create two matched arms.' }
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'collect-engineering-benchmark-suite.ps1') -Root $suite -Manifest $manifestPath -Output (Join-Path $root 'result.json') | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Collector accepted incomplete trials.' }
    Write-Output 'Engineering benchmark suite preparation and rejection smoke passed.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
