[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-execution-plan-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $manifest = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/engineering-tasks.json') -Raw | ConvertFrom-Json
    $manifest.fixtureCommit = (& git -C $repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve the fixture commit.' }
    $manifest.tasks = @($manifest.tasks | Select-Object -First 2)
    $manifest.minimumTasks = 2
    $manifest.minimumMatchedPairs = 4
    $manifest.repetitions = 2
    $manifest.requiredCategories = @($manifest.tasks.category)
    $manifestPath = Join-Path $root 'manifest.json'
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $suite = Join-Path $root 'suite'
    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-suite.ps1') -Model fixture-v1 -TokenBudget 1000 -PermissionProfile preauthorized-write-build-v1 -Manifest $manifestPath -OutputRoot $suite | Out-Null
    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-execution-plan.ps1') -Manifest $manifestPath -SuiteRoot $suite -Seed 'fixed-study-seed' | Out-Null
    $plan = Get-Content -LiteralPath (Join-Path $suite 'execution-plan.json') -Raw | ConvertFrom-Json
    if ($plan.schemaVersion -ne 1 -or $plan.pairCount -ne 4 -or $plan.runCount -ne 8) { throw 'Execution plan counts are invalid.' }
    if ($plan.balance.baselineFirst -ne 2 -or $plan.balance.arifceFirst -ne 2) { throw 'First-arm assignment is not balanced.' }
    $identities = @($plan.runs | ForEach-Object { "$($_.taskId)|$($_.trial)|$($_.arm)" })
    if (($identities | Sort-Object -Unique).Count -ne 8) { throw 'Execution plan contains duplicate or missing run identities.' }
    foreach ($pairSequence in 1..4) {
        $pair = @($plan.runs | Where-Object pairSequence -eq $pairSequence | Sort-Object sequence)
        if ($pair.Count -ne 2 -or $pair[1].sequence -ne ($pair[0].sequence + 1) -or $pair[0].taskId -cne $pair[1].taskId -or $pair[0].trial -ne $pair[1].trial -or $pair[0].arm -ceq $pair[1].arm) { throw "Pair $pairSequence is not adjacent and matched." }
        foreach ($run in $pair) {
            if ([IO.Path]::IsPathRooted($run.relativeTrialRoot) -or $run.sessionSha256 -notmatch '^[0-9a-f]{64}$' -or $run.promptSha256 -notmatch '^[0-9a-f]{64}$') { throw 'Execution plan persisted unsafe or unbound trial metadata.' }
        }
    }
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'new-engineering-benchmark-execution-plan.ps1') -Manifest $manifestPath -SuiteRoot $suite -Seed 'fixed-study-seed' | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Execution plan overwrote an existing plan.' }
    Write-Output 'Balanced reproducible benchmark execution-plan smoke passed.'
}
finally {
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $root = [IO.Path]::GetFullPath($root)
    if (-not $root.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($root).StartsWith('arifce-execution-plan-', [StringComparison]::Ordinal)) { throw 'Unsafe fixture cleanup path.' }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
