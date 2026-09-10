function Get-BenchmarkAcceptanceContract($Definition, $Task) {
    if ($Definition.schemaVersion -eq 1) { return '' }
    if ($Definition.schemaVersion -notin @(2,3)) { throw 'Unsupported benchmark manifest schema.' }
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

function Get-BenchmarkApiContract($Definition, $Task, [string]$RepositoryRoot) {
    if ($Definition.schemaVersion -lt 3) { return $null }
    $relative = [string]$Task.apiContractFile
    if ([string]::IsNullOrWhiteSpace($relative) -or $relative -notmatch '^benchmarks/api-contracts/[a-z0-9-]+\.cs$') {
        throw "Task $($Task.id) requires a safe public apiContractFile."
    }
    $path = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot $relative))
    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Task $($Task.id) public API contract is missing or escapes the repository."
    }
    $source = [IO.File]::ReadAllText($path)
    if ([string]::IsNullOrWhiteSpace($source) -or $source -notmatch 'public static class BenchmarkApiContract') {
        throw "Task $($Task.id) public API contract is not a compile probe."
    }
    return $source
}
