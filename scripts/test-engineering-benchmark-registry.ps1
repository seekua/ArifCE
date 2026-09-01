[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-evaluator-registry-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $source = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'benchmarks/evaluators.json') -Raw | ConvertFrom-Json
    $missingPath = Join-Path $root 'missing.json'
    $source.evaluators = @($source.evaluators | Select-Object -SkipLast 1)
    $source | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $missingPath -Encoding utf8
    $missingRejected = $false
    try { & (Join-Path $PSScriptRoot 'validate-engineering-benchmark.ps1') -EvaluatorRegistry $missingPath -ValidateManifestOnly | Out-Null } catch { $missingRejected = $true }
    if (-not $missingRejected) { throw 'A missing task evaluator was accepted.' }

    $unsafe = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'benchmarks/evaluators.json') -Raw | ConvertFrom-Json
    $unsafe.evaluators[0].sourceFile = '../../candidate-authored-test.cs'
    $unsafePath = Join-Path $root 'unsafe.json'
    $unsafe | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $unsafePath -Encoding utf8
    $unsafeRejected = $false
    try { & (Join-Path $PSScriptRoot 'validate-engineering-benchmark.ps1') -EvaluatorRegistry $unsafePath -ValidateManifestOnly | Out-Null } catch { $unsafeRejected = $true }
    if (-not $unsafeRejected) { throw 'An unsafe evaluator source path was accepted.' }
    Write-Output 'Engineering benchmark evaluator registry rejection tests passed.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
