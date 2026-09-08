[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-repeatable-benchmark-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $manifest = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/engineering-tasks.json') -Raw | ConvertFrom-Json
    $manifest.fixtureCommit = (& git -C $repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve the fixture commit.' }
    $manifest.tasks = @($manifest.tasks | Select-Object -First 1)
    $manifest.minimumTasks = 1
    $manifest.minimumMatchedPairs = 2
    $manifest.repetitions = 2
    $manifest.requiredCategories = @($manifest.tasks[0].category)
    $manifestPath = Join-Path $root 'manifest.json'
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $suite = Join-Path $root 'suite'
    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-suite.ps1') -Model fixture-v1 -TokenBudget 1000 -PermissionProfile preauthorized-write-build-v1 -Manifest $manifestPath -OutputRoot $suite | Out-Null
    $sessions = @(Get-ChildItem -LiteralPath $suite -Filter session.json -Recurse | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
    if ($sessions.Count -ne 4) { throw 'Repeatable suite did not create two matched pairs.' }
    foreach ($trial in 1..2) {
        $pair = @($sessions | Where-Object { $_.trial -eq $trial })
        if ($pair.Count -ne 2 -or ((@($pair.arm | Sort-Object) -join ',') -ne 'arifce,baseline')) { throw "Trial $trial does not contain both arms." }
        if (@($pair.fixtureTree | Sort-Object -Unique).Count -ne 1 -or @($pair.isolatedCommit | Sort-Object -Unique).Count -ne 1) { throw "Trial $trial arms do not share an isolated fixture." }
        if (@($pair.permissionProfile | Sort-Object -Unique).Count -ne 1 -or $pair[0].permissionProfile -ne 'preauthorized-write-build-v1') { throw "Trial $trial permission profile is not matched." }
    }
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'new-engineering-benchmark-suite.ps1') -Model fixture-v1 -TokenBudget 1000 -PermissionProfile preauthorized-write-build-v1 -Manifest $manifestPath -OutputRoot $suite | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Repeatable suite overwrote an existing trial.' }
    Write-Output 'Repeated matched benchmark preparation and permission-profile smoke passed.'
}
finally {
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $root = [IO.Path]::GetFullPath($root)
    if (-not $root.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($root).StartsWith('arifce-repeatable-benchmark-', [StringComparison]::Ordinal)) { throw 'Unsafe fixture cleanup path.' }
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
