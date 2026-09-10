[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).ProviderPath
# macOS exposes its temporary directory through /var while the physical path is
# /private/var. Canonicalize both ends before creating relative project
# references so MSBuild does not resolve them under /private/Users.
$tempParent = (Resolve-Path -LiteralPath ([IO.Path]::GetTempPath())).ProviderPath
$root = Join-Path $tempParent ('arifce-api-gate-test-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $manifest = Get-Content -LiteralPath (Join-Path $repo 'benchmarks/engineering-tasks.json') -Raw | ConvertFrom-Json
    foreach ($task in $manifest.tasks) {
        $taskRoot = Join-Path $root $task.id
        New-Item -ItemType Directory -Path $taskRoot | Out-Null
        $projectReference = [IO.Path]::GetRelativePath($taskRoot, (Join-Path $repo 'src/ArifCE.Infrastructure/ArifCE.Infrastructure.csproj')).Replace('\', '/')
        $resolvedProjectReference = [IO.Path]::GetFullPath((Join-Path $taskRoot $projectReference))
        if (-not (Test-Path -LiteralPath $resolvedProjectReference -PathType Leaf)) { throw "Generated project reference does not resolve to the reference implementation: $projectReference" }
        $project = "<Project Sdk=`"Microsoft.NET.Sdk`"><PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><ProjectReference Include=`"$projectReference`" /></ItemGroup></Project>"
        Copy-Item -LiteralPath (Join-Path $repo $task.apiContractFile) -Destination (Join-Path $taskRoot 'BenchmarkApiContract.cs')
        Set-Content -LiteralPath (Join-Path $taskRoot 'ApiCompatibilityGate.csproj') -Value $project -Encoding utf8
        & dotnet build (Join-Path $taskRoot 'ApiCompatibilityGate.csproj') --configuration Release --disable-build-servers --maxcpucount:1 --nologo --verbosity:quiet
        if ($LASTEXITCODE -ne 0) { throw "Public API contract did not compile against the reference implementation: $($task.id)" }
    }

    $manifest.fixtureCommit = (& git -C $repo rev-parse HEAD).Trim()
    $manifest.tasks = @($manifest.tasks | Where-Object id -eq 'deterministic-code-graph')
    $manifest.minimumTasks = 1
    $manifest.minimumMatchedPairs = 2
    $manifest.repetitions = 2
    $manifest.requiredCategories = @('FEATURE')
    $manifestPath = Join-Path $root 'manifest.json'
    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding utf8
    $trialOutput = Join-Path $root 'trial'
    & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId deterministic-code-graph -Arm baseline -Model fixture -TokenBudget 50000 -Manifest $manifestPath -OutputRoot $trialOutput | Out-Null
    $trial = Join-Path $trialOutput 'deterministic-code-graph/baseline/trial-01'
    $result = & (Join-Path $PSScriptRoot 'run-engineering-api-gate.ps1') -TrialRoot $trial
    if (-not $result.passed -or $result.kind -ne 'public-api-compile-gate') { throw 'Public API gate did not pass the reference implementation.' }
    & (Join-Path $PSScriptRoot 'run-engineering-api-gate.ps1') -TrialRoot $trial -VerifyOnly | Out-Null
    Add-Content -LiteralPath (Join-Path $trial 'checkout/BENCHMARK_API_CONTRACT.cs.txt') -Value '// tampered'
    $rejected = $false
    try { & (Join-Path $PSScriptRoot 'run-engineering-api-gate.ps1') -TrialRoot $trial -VerifyOnly | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Tampered public API contract was accepted.' }
    Write-Output "All $($manifest.tasks.Count + 9) public API contracts compiled; hash-bound gate and tamper rejection passed."
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($tempParent.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-api-gate-test-', [StringComparison]::Ordinal)) { throw 'Unsafe API gate fixture cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
