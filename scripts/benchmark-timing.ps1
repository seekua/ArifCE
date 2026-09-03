function Get-BenchmarkArtifactHash([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path -ErrorAction Stop).Hash.ToLowerInvariant()
}

function Read-BenchmarkHostTiming([string]$TrialRoot) {
    $path = Join-Path $TrialRoot 'host-timing.json'
    if (-not (Test-Path -LiteralPath $path)) {
        if (Test-Path -LiteralPath (Join-Path $TrialRoot 'host-capture.started')) { throw 'Interrupted host capture has no complete timing record.' }
        return $null
    }
    $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $session = Get-Content -LiteralPath (Join-Path $TrialRoot 'session.json') -Raw | ConvertFrom-Json
    if ($record.schemaVersion -cne 1 -or $record.kind -cne 'host-process-elapsed' -or $record.runId -cne $session.runId) { throw 'Invalid host timing identity.' }
    foreach ($name in @('elapsedTicks','stopwatchFrequency','exitCode')) {
        if ($record.$name -isnot [long] -and $record.$name -isnot [int]) { throw "Invalid integer timing field: $name" }
    }
    if ($record.elapsedTicks -lt 0 -or $record.stopwatchFrequency -le 0 -or $record.exitCode -lt [int]::MinValue -or $record.exitCode -gt [int]::MaxValue) { throw 'Invalid host timing counters.' }
    foreach ($pair in @(@('session.json','sessionSha256'),@('prompt.md','promptSha256'),@('agent.log','agentLogSha256'),@('host.stderr.log','stderrSha256'))) {
        if ((Get-BenchmarkArtifactHash (Join-Path $TrialRoot $pair[0])) -cne $record.($pair[1])) { throw "Host capture hash mismatch: $($pair[0])" }
    }
    $milliseconds = [Math]::Floor([decimal]$record.elapsedTicks * 1000 / [decimal]$record.stopwatchFrequency)
    if ($milliseconds -gt [long]::MaxValue) { throw 'Host elapsed milliseconds overflow.' }
    return [pscustomobject][ordered]@{
        kind = 'host-process-elapsed'
        hostElapsedMs = [long]$milliseconds
        activeWorkMs = $null
        hostExitCode = $record.exitCode
        captureSha256 = Get-BenchmarkArtifactHash $path
    }
}

function Assert-BenchmarkHostTiming($Result, [string]$TrialRoot) {
    $expected = Read-BenchmarkHostTiming $TrialRoot
    if ($null -eq $expected) {
        if ($null -ne $Result.timeMeasurement) { throw 'Host timing record is missing.' }
        return
    }
    if ($null -eq $Result.timeMeasurement) { throw 'Captured host timing cannot be omitted from the result.' }
    foreach ($property in $expected.PSObject.Properties) {
        if (($Result.timeMeasurement.($property.Name) | ConvertTo-Json -Compress) -cne ($property.Value | ConvertTo-Json -Compress)) { throw "Host timing mismatch: $($property.Name)" }
    }
}

function Get-BenchmarkHostTimeSummary([object[]]$Rows) {
    # Caller verifies each capture first; do not mix partial totals with complete arms.
    $measured = @($Rows | Where-Object { $null -ne $_.timeMeasurement })
    $total = $null
    if ($Rows.Count -gt 0 -and $Rows.Count -eq $measured.Count) {
        [decimal]$sum = 0
        foreach ($row in $measured) {
            $value = $row.timeMeasurement.hostElapsedMs
            if (($value -isnot [long] -and $value -isnot [int]) -or $value -lt 0) { throw 'Invalid host elapsed time.' }
            $sum += $value
        }
        if ($sum -gt [long]::MaxValue) { throw 'Aggregate host elapsed time overflows.' }
        $total = [long]$sum
    }
    return [pscustomobject]@{ hostElapsedMs = $total; measuredTrials = $measured.Count; activeWorkMs = $null }
}
