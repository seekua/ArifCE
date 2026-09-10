[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TrialRoot,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-contract.ps1')
$trial = [IO.Path]::GetFullPath($TrialRoot)
$sessionPath = Join-Path $trial 'session.json'
$checkout = Join-Path $trial 'checkout'
$contractPath = Join-Path $checkout 'BENCHMARK_API_CONTRACT.cs.txt'
$gateRoot = Join-Path $trial 'api-compatibility-gate'
$resultPath = Join-Path $gateRoot 'result.json'

function Hash([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "API gate artifact is missing: $Path" }
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace([string]$session.apiContractSha256)) { throw 'Trial has no public API contract.' }
if ((Get-BenchmarkContractHash ([IO.File]::ReadAllText($contractPath))) -cne [string]$session.apiContractSha256) { throw 'Public API contract hash mismatch.' }

if ($VerifyOnly) {
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'API compatibility gate result is missing.' }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    foreach ($pair in @(
        @($contractPath, $result.contractSha256),
        @((Join-Path $gateRoot 'BenchmarkApiContract.cs'), $result.sourceSha256),
        @((Join-Path $gateRoot 'ApiCompatibilityGate.csproj'), $result.projectSha256),
        @((Join-Path $gateRoot 'gate.log'), $result.outputSha256)
    )) { if ((Hash $pair[0]) -cne [string]$pair[1]) { throw "API gate provenance mismatch: $($pair[0])" } }
    if ([bool]$result.passed -ne ([int]$result.exitCode -eq 0)) { throw 'API gate outcome is inconsistent.' }
    return $result
}

if (Test-Path -LiteralPath $gateRoot) { throw 'API compatibility gate will not overwrite an existing capture.' }
New-Item -ItemType Directory -Path $gateRoot | Out-Null
Copy-Item -LiteralPath $contractPath -Destination (Join-Path $gateRoot 'BenchmarkApiContract.cs')
$projectReference = [IO.Path]::GetRelativePath($gateRoot, (Join-Path $checkout 'src/ArifCE.Infrastructure/ArifCE.Infrastructure.csproj')).Replace('\', '/')
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup><ProjectReference Include="$projectReference" /></ItemGroup>
</Project>
"@
$projectPath = Join-Path $gateRoot 'ApiCompatibilityGate.csproj'
$logPath = Join-Path $gateRoot 'gate.log'
Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8
$started = [DateTimeOffset]::UtcNow
Push-Location $gateRoot
try {
    & dotnet build $projectPath --configuration Release --disable-build-servers --maxcpucount:1 --nologo --verbosity:minimal *> $logPath
    $exitCode = $LASTEXITCODE
}
finally { Pop-Location }
$completed = [DateTimeOffset]::UtcNow
$record = [ordered]@{
    schemaVersion = 1
    kind = 'public-api-compile-gate'
    taskId = $session.taskId
    contractSha256 = Hash $contractPath
    sourceSha256 = Hash (Join-Path $gateRoot 'BenchmarkApiContract.cs')
    projectSha256 = Hash $projectPath
    outputSha256 = Hash $logPath
    exitCode = $exitCode
    passed = ($exitCode -eq 0)
    durationMs = [Math]::Max(0, [long]($completed - $started).TotalMilliseconds)
}
$record | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $resultPath -Encoding utf8
& $PSCommandPath -TrialRoot $trial -VerifyOnly | Out-Null
$record
