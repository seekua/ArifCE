[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-telemetry.ps1')
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$root = Join-Path $tempParent ('arifce-telemetry-test-' + [Guid]::NewGuid().ToString('N'))
function Expect-Rejection([scriptblock]$Action, [string]$Name) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Accepted invalid telemetry: $Name" }
}
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $log = Join-Path $root 'fixture.jsonl'
    # Synthetic protocol fixtures only; these are not measured benchmark runs.
    $header = '{"type":"thread.started","thread_id":"fixture-thread"}' + "`n" + '{"type":"turn.started"}' + "`n"
    $terminal = '{"type":"turn.completed","usage":{"input_tokens":100,"cached_input_tokens":60,"output_tokens":20}}'
    [IO.File]::WriteAllText($log, $header + $terminal)
    $usage = Read-BenchmarkTokenUsage $log
    if ($usage.totalTokens -ne 120 -or $usage.cachedInputTokens -ne 60) { throw 'Cached input was double-counted or total usage is wrong.' }
    $measured = [pscustomobject]@{ tokenSource = 'agent-host'; tokensConsumed = 120L; tokenMeasurement = $usage }
    Assert-BenchmarkTokenUsage $measured $log
    $summary = Get-BenchmarkTokenSummary @($measured, $measured)
    if ($summary.totalTokens -ne 240 -or $summary.availableTrials -ne 2) { throw 'Complete measured totals are incorrect.' }
    foreach ($missingValue in @($null, 0)) {
        $missing = [pscustomobject]@{ tokenSource = 'unavailable'; tokensConsumed = $missingValue }
        Assert-BenchmarkTokenUsage $missing $log
        $summary = Get-BenchmarkTokenSummary @($missing, $measured)
        if ($null -ne $summary.totalTokens -or $summary.unavailableTrials -ne 1) { throw 'Missing telemetry was reported as zero consumption.' }
    }
    if ($null -ne (Get-BenchmarkTokenSummary @()).totalTokens) { throw 'An empty arm is not a measured zero.' }
    $measured.tokensConsumed = 121L
    Expect-Rejection { Assert-BenchmarkTokenUsage $measured $log } 'tampered total'
    $measured.tokensConsumed = 120L
    $usage.inputTokens = 101L
    Expect-Rejection { Assert-BenchmarkTokenUsage $measured $log } 'tampered counters'
    $usage.inputTokens = 100L
    Expect-Rejection { Assert-BenchmarkTokenUsage ([pscustomobject]@{tokenSource='provider';tokensConsumed=120L}) $log } 'unbound manual measurement'
    Expect-Rejection { Assert-BenchmarkTokenUsage ([pscustomobject]@{tokenSource='unavailable';tokensConsumed=120L}) $log } 'nonzero unavailable measurement'

    foreach ($invalid in @(
        $header,
        ($header + $terminal + "`n" + $terminal),
        ($header + $terminal + "`n" + $header + $terminal),
        ($header + '{"type":"turn.failed"}'),
        ($header + '{"type":"error"}' + "`n" + $terminal),
        ($header + '{not-json'),
        ($header + '{"type":"turn.completed","usage":{"input_tokens":-1,"cached_input_tokens":0,"output_tokens":20}}'),
        ($header + '{"type":"turn.completed","usage":{"input_tokens":1.5,"cached_input_tokens":0,"output_tokens":20}}'),
        ($header + '{"type":"turn.completed","usage":{"input_tokens":"100","cached_input_tokens":60,"output_tokens":20}}'),
        ($header + '{"type":"turn.completed","usage":{"input_tokens":100,"cached_input_tokens":101,"output_tokens":20}}'),
        ($header + '{"type":"turn.completed","usage":{"input_tokens":9223372036854775807,"cached_input_tokens":0,"output_tokens":1}}'),
        ($header + '{"type":"turn.completed","usage":{"output_tokens":20}}'),
        $terminal
    )) {
        [IO.File]::WriteAllText($log, $invalid)
        Expect-Rejection { Read-BenchmarkTokenUsage $log } 'malformed, incomplete, duplicate, or inconsistent events'
    }
    [IO.File]::WriteAllText($log, $header + '{"type":"turn.completed","usage":{"input_tokens":0,"cached_input_tokens":0,"output_tokens":0}}')
    $zero = Read-BenchmarkTokenUsage $log
    $summary = Get-BenchmarkTokenSummary @([pscustomobject]@{tokenSource='agent-host';tokensConsumed=0L;tokenMeasurement=$zero})
    if ($null -eq $summary.totalTokens -or $summary.totalTokens -ne 0) { throw 'A reported zero must remain distinct from unavailable usage.' }
    Write-Output 'Benchmark telemetry parsing, tamper rejection, and missing-value checks passed.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($tempParent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-telemetry-test-', [StringComparison]::Ordinal)) { throw 'Unsafe telemetry fixture cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
