function Get-BenchmarkAcceptanceContract($Definition, $Task) {
    if ($Definition.schemaVersion -eq 1) { return '' }
    if ($Definition.schemaVersion -ne 2) { throw 'Unsupported benchmark manifest schema.' }
    foreach ($name in @('acceptanceContract','evaluationLimitations')) {
        $values = $Task.$name
        if ($values -isnot [array] -or $values.Count -eq 0) { throw "Task $($Task.id) requires a nonempty $name array." }
        foreach ($value in $values) {
            if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) { throw "Task $($Task.id) contains an invalid $name item." }
        }
    }
    return "## Public evaluator contract`n`n" + (($Task.acceptanceContract | ForEach-Object { '- ' + $_ }) -join "`n") + "`n`n## Evaluation limits`n`n" + (($Task.evaluationLimitations | ForEach-Object { '- ' + $_ }) -join "`n")
}

function Get-BenchmarkContractHash([string]$Contract) {
    if ([string]::IsNullOrEmpty($Contract)) { return $null }
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Contract))).ToLowerInvariant()
}
