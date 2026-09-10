function Estimate-BenchmarkTokens([string]$Text) {
    if ([string]::IsNullOrEmpty($Text)) { return 0L }
    return [long][Math]::Ceiling($Text.Length / 4.0)
}

function Read-BenchmarkContextEfficiency([string]$LogPath, $TokenMeasurement) {
    $commands = [System.Collections.Generic.List[object]]::new()
    $messages = [System.Collections.Generic.List[string]]::new()
    $reader = [IO.File]::OpenText([IO.Path]::GetFullPath($LogPath))
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            if ([string]::IsNullOrWhiteSpace($line)) { continue }
            try { $event = $line | ConvertFrom-Json -ErrorAction Stop } catch { continue }
            if ($event.type -cne 'item.completed' -or $null -eq $event.item) { continue }
            if ($event.item.type -ceq 'agent_message') { $messages.Add([string]$event.item.text); continue }
            if ($event.item.type -notin @('command_execution','file_change')) { continue }
            $command = [string]$event.item.command
            $output = [string]$event.item.aggregated_output
            $commands.Add([pscustomobject]@{
                type = [string]$event.item.type
                command = $command
                output = $output
                exitCode = $event.item.exit_code
                outputTokens = Estimate-BenchmarkTokens $output
            })
        }
    }
    finally { $reader.Dispose() }

    $readPattern = '(?i)(Get-Content|\btype\s+|\bsed\s+-n|\bhead\s+|\btail\s+)'
    $searchPattern = '(?i)(^|[\s"''])rg(?:\.exe)?\s'
    $buildPattern = '(?i)(dotnet\s+(restore|build|test)|BENCHMARK_RUN_CHECK\.ps1)'
    $arifcePattern = '(?i)(ArifCE\.Cli(?:\.dll|\.csproj)|\barifce(?:\.exe)?\s+(status|context|search|task|claim|verify|handoff))'
    $shellEditPattern = '(?i)(Set-Content|Add-Content|WriteAllText|WriteAllLines|FromBase64String|\s-replace\s|python\s+-c)'
    $reads = @($commands | Where-Object { $_.type -eq 'command_execution' -and $_.command -match $readPattern })
    $searches = @($commands | Where-Object { $_.type -eq 'command_execution' -and $_.command -match $searchPattern })
    $builds = @($commands | Where-Object { $_.type -eq 'command_execution' -and $_.command -match $buildPattern })
    $arifceCommands = @($commands | Where-Object { $_.type -eq 'command_execution' -and $_.command -match $arifcePattern })
    $shellEdits = @($commands | Where-Object { $_.type -eq 'command_execution' -and $_.command -match $shellEditPattern })

    function Get-DuplicateStats([object[]]$Items) {
        $seen = @{}
        [long]$duplicateTokens = 0
        $duplicates = 0
        [long]$uniqueTokens = 0
        foreach ($item in $Items) {
            $key = $item.command.Trim() + "`n" + $item.output
            if ($seen.ContainsKey($key)) { $duplicates++; $duplicateTokens += $item.outputTokens }
            else { $seen[$key] = $true; $uniqueTokens += $item.outputTokens }
        }
        [pscustomobject]@{ count = $duplicates; tokens = $duplicateTokens; uniqueTokens = $uniqueTokens }
    }
    $readDuplicates = Get-DuplicateStats $reads
    $searchDuplicates = Get-DuplicateStats $searches
    $largeShellEdits = @($shellEdits | Where-Object { $_.command.Length -gt 2000 })
    $directBuildChecks = @($commands | Where-Object { $_.command -match '(?i)dotnet\s+(restore|build|test)' -and $_.command -notmatch '(?i)BENCHMARK_RUN_CHECK\.ps1' -and $_.command -notmatch '(?i)ArifCE\.Cli\.dll' })
    $failedCommands = @($commands | Where-Object { $null -ne $_.exitCode -and [int]$_.exitCode -ne 0 })
    $replanMessages = @($messages | Where-Object { $_ -match '(?i)re-?plan|new approach|adjust(?:ing)? the approach' })
    $similarFailureGroups = @($failedCommands | Group-Object { ($_.command -replace '\s+',' ').Substring(0, [Math]::Min(120, ($_.command -replace '\s+',' ').Length)) } | Where-Object Count -ge 2)

    [long]$uniqueUsefulContext = $readDuplicates.uniqueTokens + $searchDuplicates.uniqueTokens
    [long]$processedInput = if ($null -eq $TokenMeasurement) { 0 } else { [long]$TokenMeasurement.inputTokens }
    $usefulRatio = if ($processedInput -gt 0) { [Math]::Round($uniqueUsefulContext / [double]$processedInput, 6) } else { $null }
    $amplification = if ($uniqueUsefulContext -gt 0) { [Math]::Round($processedInput / [double]$uniqueUsefulContext, 3) } else { $null }
    $policyViolations = [System.Collections.Generic.List[string]]::new()
    if ($readDuplicates.count -gt 0) { $policyViolations.Add('UNCHANGED_FULL_READ_REPEATED') }
    if ($searchDuplicates.count -gt 0) { $policyViolations.Add('UNCHANGED_SEARCH_REPEATED') }
    if ($largeShellEdits.Count -gt 0) { $policyViolations.Add('LARGE_SOURCE_EDIT_EMBEDDED_IN_SHELL') }
    if ($directBuildChecks.Count -gt 0) { $policyViolations.Add('UNBOUNDED_BUILD_TEST_OUTPUT') }
    if ($similarFailureGroups.Count -gt 0 -and $replanMessages.Count -eq 0) { $policyViolations.Add('REPEATED_FAILURE_WITHOUT_REPLAN') }

    return [pscustomobject][ordered]@{
        schemaVersion = 1
        method = 'jsonl-command-output-estimate-v1'
        modelToolRounds = $commands.Count
        fileReadTokens = [long](($reads | Measure-Object outputTokens -Sum).Sum ?? 0)
        repeatedFileReadCount = $readDuplicates.count
        repeatedFileContextTokens = $readDuplicates.tokens
        searchTokens = [long](($searches | Measure-Object outputTokens -Sum).Sum ?? 0)
        repeatedSearchCount = $searchDuplicates.count
        repeatedSearchTokens = $searchDuplicates.tokens
        editRetryTokens = [long](($failedCommands | Measure-Object outputTokens -Sum).Sum ?? 0)
        largeShellEditCount = $largeShellEdits.Count
        unboundedBuildTestCount = $directBuildChecks.Count
        buildTestTokens = [long](($builds | Measure-Object outputTokens -Sum).Sum ?? 0)
        arifceOverheadTokens = [long](($arifceCommands | Measure-Object outputTokens -Sum).Sum ?? 0)
        arifceCommandCount = $arifceCommands.Count
        uniqueUsefulContextTokensEstimate = $uniqueUsefulContext
        usefulContextRatioEstimate = $usefulRatio
        contextAmplificationFactorEstimate = $amplification
        failedToolActions = $failedCommands.Count
        replanSignals = $replanMessages.Count
        policyPassed = ($policyViolations.Count -eq 0)
        policyViolations = @($policyViolations)
        limitations = 'Token estimates use four characters per token. Useful context means first unique read/search output, not semantic necessity. Cached replay attribution is unavailable from host events.'
    }
}
