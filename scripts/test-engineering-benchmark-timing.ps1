[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-timing.ps1')
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-timing-test-' + [Guid]::NewGuid().ToString('N'))
$hostExe = (Get-Process -Id $PID).Path
function Reject([scriptblock]$Action, [string]$Name) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw "Expected rejection: $Name" }
}
function New-Fixture([string]$Name) {
    $trial = Join-Path $root $Name
    New-Item -ItemType Directory -Path (Join-Path $trial 'checkout') -Force | Out-Null
    @{runId=[Guid]::NewGuid().ToString();preparedAtUtc='2000-01-01T00:00:00Z'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $trial 'session.json')
    Set-Content -LiteralPath (Join-Path $trial 'prompt.md') -Value 'test prompt'
    return $trial
}
try {
    $trial = New-Fixture 'success'
    $fixture = Join-Path $root 'host fixture.ps1'
    @'
param([string]$Literal)
$prompt = [Console]::In.ReadToEnd()
if ($prompt.Trim() -ne 'test prompt' -or (Split-Path -Leaf (Get-Location)) -ne 'checkout') { exit 9 }
[Console]::Out.WriteLine($Literal)
for ($i=0; $i -lt 10000; $i++) { [Console]::Out.WriteLine('output payload'); [Console]::Error.WriteLine('diagnostic payload') }
Start-Sleep -Milliseconds 150
'@ | Set-Content -LiteralPath $fixture
    $outer = [Diagnostics.Stopwatch]::StartNew()
    & (Join-Path $PSScriptRoot 'invoke-engineering-benchmark-host.ps1') -TrialRoot $trial -Executable $hostExe -HostArguments @('-NoProfile','-File',$fixture,'literal ; & argument') -TimeoutSeconds 30 | Out-Null
    $outer.Stop()
    $measurement = Read-BenchmarkHostTiming $trial
    if ($measurement.hostExitCode -ne 0 -or $measurement.hostElapsedMs -lt 150 -or $measurement.hostElapsedMs -gt $outer.ElapsedMilliseconds -or $null -ne $measurement.activeWorkMs) { throw 'Process timing was misclassified or measured outside capture.' }
    if ((Get-Content -LiteralPath (Join-Path $trial 'agent.log') -First 1) -cne 'literal ; & argument') { throw 'Argument boundaries were lost.' }
    if ([IO.File]::ReadAllLines((Join-Path $trial 'agent.log')).Count -ne 10001 -or [IO.File]::ReadAllLines((Join-Path $trial 'host.stderr.log')).Count -ne 10000) { throw 'Host output was truncated or lost.' }
    $result = [pscustomobject]@{timeMeasurement=$measurement}
    Assert-BenchmarkHostTiming $result $trial
    $summary = Get-BenchmarkHostTimeSummary @($result, $result)
    if ($summary.hostElapsedMs -ne 2 * $measurement.hostElapsedMs -or $summary.measuredTrials -ne 2 -or $null -ne $summary.activeWorkMs) { throw 'Incorrect complete timing aggregate.' }
    $partial = Get-BenchmarkHostTimeSummary @($result, [pscustomobject]@{})
    if ($null -ne $partial.hostElapsedMs -or $partial.measuredTrials -ne 1) { throw 'Missing timing counted as measured zero.' }
    if ($null -ne (Get-BenchmarkHostTimeSummary @()).hostElapsedMs) { throw 'Empty timing aggregate was measured.' }
    Reject { & (Join-Path $PSScriptRoot 'invoke-engineering-benchmark-host.ps1') -TrialRoot $trial -Executable $hostExe } 'capture overwrite'
    $result.timeMeasurement.hostElapsedMs++
    Reject { Assert-BenchmarkHostTiming $result $trial } 'result duration tampering'
    $result.timeMeasurement = Read-BenchmarkHostTiming $trial
    $result.timeMeasurement.activeWorkMs = 1
    Reject { Assert-BenchmarkHostTiming $result $trial } 'invented active work'
    $result.timeMeasurement = Read-BenchmarkHostTiming $trial
    Reject { Assert-BenchmarkHostTiming ([pscustomobject]@{}) $trial } 'omitted capture'
    $timingPath = Join-Path $trial 'host-timing.json'
    $original = [IO.File]::ReadAllText($timingPath)
    $altered = $original | ConvertFrom-Json
    $altered.elapsedTicks++
    $altered | ConvertTo-Json | Set-Content -LiteralPath $timingPath
    Reject { Assert-BenchmarkHostTiming $result $trial } 'timing source tampering'
    $altered.stopwatchFrequency = 0
    $altered | ConvertTo-Json | Set-Content -LiteralPath $timingPath
    Reject { Read-BenchmarkHostTiming $trial } 'zero frequency'
    foreach ($bad in @(-1,1.5,'10')) {
        $record = $original | ConvertFrom-Json
        $record.elapsedTicks = $bad
        $record | ConvertTo-Json | Set-Content -LiteralPath $timingPath
        Reject { Read-BenchmarkHostTiming $trial } 'invalid counter'
    }
    [IO.File]::WriteAllText($timingPath, $original)
    foreach ($name in @('session.json','prompt.md','agent.log','host.stderr.log')) {
        $path = Join-Path $trial $name
        $originalBytes = [IO.File]::ReadAllBytes($path)
        Add-Content -LiteralPath $path -Value 'tamper'
        Reject { Read-BenchmarkHostTiming $trial } "modified $name"
        [IO.File]::WriteAllBytes($path, $originalBytes)
    }
    $failed = New-Fixture 'failed'
    & (Join-Path $PSScriptRoot 'invoke-engineering-benchmark-host.ps1') -TrialRoot $failed -Executable $hostExe -HostArguments @('-NoProfile','-Command','[Console]::In.ReadToEnd() | Out-Null; exit 7') | Out-Null
    if ((Read-BenchmarkHostTiming $failed).hostExitCode -ne 7) { throw 'Nonzero host outcome was lost.' }
    $timeout = New-Fixture 'timeout'
    Reject { & (Join-Path $PSScriptRoot 'invoke-engineering-benchmark-host.ps1') -TrialRoot $timeout -Executable $hostExe -HostArguments @('-NoProfile','-Command','Start-Sleep -Seconds 10') -TimeoutSeconds 1 } 'timeout'
    Reject { Read-BenchmarkHostTiming $timeout } 'incomplete capture'
    $legacy = New-Fixture 'legacy'
    if ($null -ne (Read-BenchmarkHostTiming $legacy)) { throw 'Legacy timing must stay unavailable.' }
    Assert-BenchmarkHostTiming ([pscustomobject]@{}) $legacy
    Write-Output 'Host process timing, capture integrity, failure, timeout, and legacy checks passed.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-timing-test-', [StringComparison]::Ordinal)) { throw 'Unsafe timing fixture cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
