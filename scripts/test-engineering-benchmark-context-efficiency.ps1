[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-context-efficiency.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-context-efficiency-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $log = Join-Path $root 'agent.jsonl'
    $events = @(
        @{ type='item.completed'; item=@{ type='command_execution'; command='Get-Content src/A.cs'; aggregated_output=('a' * 400); exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='Get-Content src/A.cs'; aggregated_output=('a' * 400); exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='rg -n Symbol src'; aggregated_output=('b' * 200); exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='rg -n Symbol src'; aggregated_output=('b' * 200); exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='./BENCHMARK_RUN_CHECK.ps1 -Action test'; aggregated_output='{"exitCode":0}'; exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command=('Set-Content src/A.cs ' + ('x' * 2100)); aggregated_output='failed'; exit_code=1 } },
        @{ type='item.completed'; item=@{ type='agent_message'; text='I will re-plan after the failed edit.' } }
    )
    [IO.File]::WriteAllLines($log, @($events | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 }))
    $metrics = Read-BenchmarkContextEfficiency $log ([pscustomobject]@{ inputTokens=1000 })
    if ($metrics.fileReadTokens -ne 200 -or $metrics.repeatedFileReadCount -ne 1 -or $metrics.repeatedFileContextTokens -ne 100) { throw 'Repeated file reads were not measured.' }
    if ($metrics.searchTokens -ne 100 -or $metrics.repeatedSearchCount -ne 1 -or $metrics.repeatedSearchTokens -ne 50) { throw 'Repeated searches were not measured.' }
    if ($metrics.largeShellEditCount -ne 1 -or $metrics.policyPassed -or 'LARGE_SOURCE_EDIT_EMBEDDED_IN_SHELL' -notin $metrics.policyViolations) { throw 'Large shell edits were not rejected by policy.' }
    if ($metrics.uniqueUsefulContextTokensEstimate -ne 150 -or $metrics.usefulContextRatioEstimate -ne 0.15 -or $metrics.contextAmplificationFactorEstimate -ne 6.667) { throw 'Useful-context estimates are inconsistent.' }
    if ($metrics.replanSignals -ne 1 -or $metrics.unboundedBuildTestCount -ne 0) { throw 'Replanning or bounded build detection is wrong.' }
    Write-Output 'Benchmark context-efficiency measurement and policy smoke test passed.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
