[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-assessment.ps1')
. (Join-Path $PSScriptRoot 'benchmark-contract.ps1')
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-assessment-test-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $trx = Join-Path $root 'result.trx'
    if ((Read-BenchmarkAssessment $trx 1 @('Example')).status -ne 'ERROR') { throw 'Compilation failure counted as test failure.' }
    foreach ($case in @(
        @{outcome='Passed';exit=0;expected='PASSED'},
        @{outcome='Failed';exit=1;expected='FAILED'},
        @{outcome='Passed';exit=1;expected='ERROR'},
        @{outcome='Failed';exit=0;expected='ERROR'},
        @{outcome='NotExecuted';exit=0;expected='ERROR'}
    )) {
        [IO.File]::WriteAllText($trx, '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult testName="ArifCE.IndependentEvaluator.IndependentTests.Example" outcome="' + $case.outcome + '" /></Results></TestRun>')
        $assessment = Read-BenchmarkAssessment $trx $case.exit @('Example')
        if ($assessment.status -ne $case.expected) { throw "Incorrect assessment: $($case.outcome)/$($case.exit)" }
        if ($case.expected -eq 'ERROR' -and $null -ne $assessment.taskPassed) { throw 'Evaluator error must remain unscored.' }
        if ((Read-BenchmarkAssessment $trx $case.exit @('Other')).status -ne 'ERROR') { throw 'Wrong test identity accepted.' }
        if ((Read-BenchmarkAssessment $trx $case.exit @('Example','Missing')).status -ne 'ERROR') { throw 'Incomplete evaluator accepted.' }
    }
    foreach ($invalid in @('<broken', '<!DOCTYPE TestRun [<!ENTITY external SYSTEM "file:///nonexistent">]><TestRun>&external;</TestRun>', '<TestRun><Results><UnitTestResult testName="ArifCE.IndependentEvaluator.IndependentTests.Example" outcome="Passed"/><UnitTestResult testName="ArifCE.IndependentEvaluator.IndependentTests.Example" outcome="Passed"/></Results></TestRun>')) {
        [IO.File]::WriteAllText($trx, $invalid)
        if ((Read-BenchmarkAssessment $trx 0 @('Example')).status -ne 'ERROR') { throw 'Malformed or duplicate test result accepted.' }
    }
    $definition = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'benchmarks/engineering-tasks.json') -Raw | ConvertFrom-Json
    foreach ($task in $definition.tasks) {
        $contract = Get-BenchmarkAcceptanceContract $definition $task
        if ($contract -notmatch 'Public evaluator contract' -or (Get-BenchmarkContractHash $contract) -notmatch '^[0-9a-f]{64}$') { throw 'Missing task contract.' }
    }
    $definition.tasks[0].acceptanceContract = @('')
    $rejected = $false
    try { Get-BenchmarkAcceptanceContract $definition $definition.tasks[0] | Out-Null } catch { $rejected = $true }
    if (-not $rejected) { throw 'Blank contract accepted.' }
    if ((Get-BenchmarkAcceptanceContract ([pscustomobject]@{schemaVersion=1}) ([pscustomobject]@{})) -ne '') { throw 'Legacy manifest compatibility failed.' }
    Write-Output 'Benchmark contract and executed-test assessment checks passed.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-assessment-test-', [StringComparison]::Ordinal)) { throw 'Unsafe fixture cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
