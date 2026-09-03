# Parse captured host events only. This module never invokes a model or estimates tokens.
function Read-BenchmarkTokenCount($Value, [string]$Name) {
    if (($Value -isnot [long] -and $Value -isnot [int]) -or $Value -lt 0) {
        throw "Token counter '$Name' must be a non-negative integer."
    }
    return [long]$Value
}

function Read-BenchmarkTokenUsage([string]$LogPath) {
    $threadId = $null
    $started = $false
    $completed = $false
    $measurement = $null
    $lineNumber = 0
    $reader = [IO.File]::OpenText([IO.Path]::GetFullPath($LogPath))
    try {
    while ($null -ne ($line = $reader.ReadLine())) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $event = $line | ConvertFrom-Json -ErrorAction Stop } catch { throw "Malformed host JSON at line $lineNumber." }
        if ($event -isnot [pscustomobject] -or $event.type -isnot [string]) { throw "Invalid host event at line $lineNumber." }
        switch -CaseSensitive ($event.type) {
            'thread.started' {
                if ($null -ne $threadId -or $started -or $event.thread_id -isnot [string] -or [string]::IsNullOrWhiteSpace($event.thread_id)) { throw 'Expected one host thread per trial log.' }
                $threadId = $event.thread_id
            }
            'turn.started' {
                if ($null -eq $threadId -or $started) { throw 'Expected one host turn per trial log.' }
                $started = $true
            }
            'turn.completed' {
                if (-not $started -or $completed) { throw 'Duplicate or unmatched host completion.' }
                $inputTokens = Read-BenchmarkTokenCount $event.usage.input_tokens 'input_tokens'
                $cachedTokens = Read-BenchmarkTokenCount $event.usage.cached_input_tokens 'cached_input_tokens'
                $outputTokens = Read-BenchmarkTokenCount $event.usage.output_tokens 'output_tokens'
                if ($cachedTokens -gt $inputTokens) { throw 'Cached input exceeds total input tokens.' }
                $total = [decimal]$inputTokens + [decimal]$outputTokens
                if ($total -gt [long]::MaxValue) { throw 'Total token count overflows Int64.' }
                $measurement = [pscustomobject][ordered]@{
                    format = 'codex-exec-jsonl'
                    version = 1
                    threadId = $threadId
                    inputTokens = $inputTokens
                    cachedInputTokens = $cachedTokens
                    outputTokens = $outputTokens
                    totalTokens = [long]$total
                }
                $completed = $true
            }
            'turn.failed' { throw 'Failed host turn has incomplete token telemetry; preserve it with unavailable usage.' }
            'error' { throw 'Host error prevents complete token telemetry; preserve it with unavailable usage.' }
        }
    }
    } finally { $reader.Dispose() }
    if (-not $completed) { throw 'No complete host usage event was captured.' }
    return $measurement
}

function Assert-BenchmarkTokenUsage($Result, [string]$LogPath) {
    if ($Result.tokenSource -ceq 'unavailable') {
        # Legacy schema-1 trials used zero as an unavailable sentinel.
        if (($null -ne $Result.tokensConsumed -and $Result.tokensConsumed -cne 0) -or $null -ne $Result.tokenMeasurement) { throw 'Unavailable usage cannot contain a token measurement.' }
        return
    }
    if ($Result.tokenSource -cne 'agent-host' -or $null -eq $Result.tokenMeasurement) { throw 'Token counts require supported captured host usage; manual totals are not provenance.' }
    $expected = Read-BenchmarkTokenUsage $LogPath
    foreach ($property in $expected.PSObject.Properties) {
        if (($Result.tokenMeasurement.($property.Name) | ConvertTo-Json -Compress) -cne ($property.Value | ConvertTo-Json -Compress)) { throw "Token measurement mismatch: $($property.Name)." }
    }
    if ((Read-BenchmarkTokenCount $Result.tokensConsumed 'tokensConsumed') -ne $expected.totalTokens) { throw 'Total tokens do not match captured host usage.' }
}

function Get-BenchmarkTokenSummary([object[]]$Rows) {
    $available = @($Rows | Where-Object { $_.tokenSource -ceq 'agent-host' -and $null -ne $_.tokenMeasurement })
    $total = $null
    if ($Rows.Count -gt 0 -and $available.Count -eq $Rows.Count) {
        [decimal]$sum = 0
        foreach ($row in $available) { $sum += Read-BenchmarkTokenCount $row.tokensConsumed 'tokensConsumed' }
        if ($sum -gt [long]::MaxValue) { throw 'Aggregate token count overflows Int64.' }
        $total = [long]$sum
    }
    return [pscustomobject]@{ totalTokens = $total; availableTrials = $available.Count; unavailableTrials = $Rows.Count - $available.Count }
}
