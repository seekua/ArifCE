[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TrialRoot,
    [string]$EvaluatorRegistry = 'benchmarks/evaluators.json',
    [string]$SourceRepository
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-safety-source.ps1')
$repo = if ([string]::IsNullOrWhiteSpace($SourceRepository)) { Split-Path -Parent $PSScriptRoot } else { [IO.Path]::GetFullPath($SourceRepository) }
$trial = [IO.Path]::GetFullPath($TrialRoot)
$resultPath = Join-Path $trial 'result.json'
$checkout = Join-Path $trial 'checkout'
$evaluatorRoot = Join-Path $trial 'independent-evaluator'
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'Complete and verify the candidate trial before independent evaluation.' }
& (Join-Path $PSScriptRoot 'complete-engineering-benchmark-trial.ps1') -TrialRoot $trial -VerifyOnly | Out-Null
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($null -ne $result.PSObject.Properties['independentEvaluation']) { throw 'Independent evaluation will not be overwritten.' }
$registryPath = if ([IO.Path]::IsPathRooted($EvaluatorRegistry)) { $EvaluatorRegistry } else { Join-Path $repo $EvaluatorRegistry }
$registry = Get-Content -LiteralPath $registryPath -Raw | ConvertFrom-Json
$entry = @($registry.evaluators | Where-Object taskId -eq $result.taskId)
if ($entry.Count -ne 1) { throw "Expected exactly one independent evaluator for $($result.taskId)." }
$entry = $entry[0]
$source = @(& git -C $repo show "$($entry.sourceCommit):$($entry.sourceFile)" 2>&1)
if ($LASTEXITCODE -ne 0) { throw "Unable to read pinned evaluator source: $($source -join [Environment]::NewLine)" }
$sourceText = $source -join "`n"
function Extract-Test([string]$Text, [string]$Method) {
    $signature = [regex]::Match($Text, "(?m)^    public (?:async )?(?:Task|void) $([regex]::Escape($Method))\(")
    if (-not $signature.Success) { throw "Pinned evaluator method was not found: $Method" }
    $fact = $Text.LastIndexOf('    [Fact]', $signature.Index, [StringComparison]::Ordinal)
    if ($fact -lt 0) { throw "Pinned evaluator method has no [Fact] marker: $Method" }
    $nextFact = $Text.IndexOf("`n    [Fact]", $signature.Index, [StringComparison]::Ordinal)
    $nextPrivate = $Text.IndexOf("`n    private ", $signature.Index, [StringComparison]::Ordinal)
    $ends = @($nextFact, $nextPrivate) | Where-Object { $_ -gt $signature.Index }
    $end = if ($ends.Count -eq 0) { $Text.LastIndexOf("`n}", [StringComparison]::Ordinal) } else { ($ends | Measure-Object -Minimum).Minimum }
    return $Text.Substring($fact, $end - $fact).TrimEnd()
}
$tests = @($entry.methods | ForEach-Object { Extract-Test $sourceText $_ }) -join "`n`n"
$classBody = switch ($entry.fixture) {
    'safety' { ConvertTo-BenchmarkSafetySource $sourceText }
    'behavior' { @"
using System.Diagnostics;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;
namespace ArifCE.IndependentEvaluator;
public sealed class IndependentTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-independent", Guid.NewGuid().ToString("N"));
    private readonly CanonicalStore canonical = new();
    private readonly JournalStore journal = new();
    private readonly IndexStore index = new();
    private readonly GitInspector git = new();
    public IndependentTests() { Directory.CreateDirectory(root); RunGit("init"); }
    private ProjectService Service => new(canonical, journal, index, git);
$tests
    private void RunGit(string arguments) { using var process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true }); process!.WaitForExit(); Assert.Equal(0, process.ExitCode); }
    public void Dispose() { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
"@ }
    'llm' { @"
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;
namespace ArifCE.IndependentEvaluator;
public sealed class IndependentTests
{
$tests
    private sealed class StubProvider(string id, bool fail)
    {
        public LlmProviderProfile Profile { get; } = new(id, LlmProviderKind.OpenAI, "model", null, null);
        public ILlmProvider Provider { get; } = new Impl(id, fail);
        private sealed class Impl(string id, bool fail) : ILlmProvider
        {
            public string ProviderId => id; public LlmProviderKind Kind => LlmProviderKind.OpenAI;
            public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) => fail ? throw new HttpRequestException("offline") : Task.FromResult(new LlmResponse(id, "model", "done", new LlmUsage(3, 2), TimeSpan.Zero));
            public Task<LlmConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(new LlmConnectionResult(id, !fail, fail ? "offline" : "ok", TimeSpan.Zero));
        }
    }
}
"@ }
    'mcp' { @"
using System.Diagnostics;
using Xunit;
namespace ArifCE.IndependentEvaluator;
public sealed class IndependentTests
{
$tests
    private static Process StartServer(string? root = null)
    {
        var server = Path.Combine(AppContext.BaseDirectory, "ArifCE.Mcp.dll");
        var start = new ProcessStartInfo("dotnet", $"\"{server}\"") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        if (root is not null) start.Environment["ARIFCE_PROJECT_ROOT"] = root;
        return Process.Start(start)!;
    }
}
"@ }
    default { throw "Unsupported evaluator fixture: $($entry.fixture)" }
}
New-Item -ItemType Directory -Path $evaluatorRoot | Out-Null
$projectReferences = @('../checkout/src/ArifCE.Infrastructure/ArifCE.Infrastructure.csproj')
if ($entry.fixture -eq 'mcp') { $projectReferences += '../checkout/src/ArifCE.Mcp/ArifCE.Mcp.csproj' }
$referenceXml = $projectReferences | ForEach-Object { "    <ProjectReference Include=`"$_`" />" }
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><ImplicitUsings>enable</ImplicitUsings><IsTestProject>true</IsTestProject><NoWarn>`$(NoWarn);xUnit1051</NoWarn></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="xunit.v3" Version="3.2.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
$($referenceXml -join "`n")
  </ItemGroup>
</Project>
"@
$sourcePath = Join-Path $evaluatorRoot 'IndependentTests.cs'
$projectPath = Join-Path $evaluatorRoot 'IndependentEvaluator.csproj'
$outputPath = Join-Path $evaluatorRoot 'evaluator.log'
$trxPath = Join-Path $evaluatorRoot 'results/evaluator.trx'
Set-Content -LiteralPath $sourcePath -Value $classBody -Encoding utf8
Set-Content -LiteralPath $projectPath -Value $project -Encoding utf8
Push-Location $evaluatorRoot
$testFilter = ($entry.methods | ForEach-Object { "FullyQualifiedName=ArifCE.IndependentEvaluator.IndependentTests.$_" }) -join '|'
try { & dotnet test $projectPath --configuration Release --disable-build-servers --maxcpucount:1 --filter $testFilter --logger 'trx;LogFileName=evaluator.trx' --results-directory (Join-Path $evaluatorRoot 'results') *> $outputPath; $exitCode = $LASTEXITCODE } finally { Pop-Location }
$assessment = Read-BenchmarkAssessment $trxPath $exitCode @($entry.methods)
$evaluation = [ordered]@{
    registrySha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $registryPath).Hash.ToLowerInvariant()
    sourceCommit = $entry.sourceCommit
    sourceFile = $entry.sourceFile
    methods = @($entry.methods)
    injectedSourceSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourcePath).Hash.ToLowerInvariant()
    projectSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $projectPath).Hash.ToLowerInvariant()
    outputSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $outputPath).Hash.ToLowerInvariant()
    exitCode = $exitCode
    taskPassed = $assessment.taskPassed
    assessment = $assessment
    testResultsSha256 = if (Test-Path -LiteralPath $trxPath) { (Get-FileHash -Algorithm SHA256 -LiteralPath $trxPath).Hash.ToLowerInvariant() } else { $null }
}
$result | Add-Member -NotePropertyName independentEvaluation -NotePropertyValue $evaluation
$result | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $resultPath -Encoding utf8
Write-Output "Independent evaluator $($entry.taskId): $($assessment.status)"
