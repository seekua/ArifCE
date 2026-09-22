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
        @{ type='item.completed'; item=@{ type='command_execution'; command='./BENCHMARK_VERIFY_CHECK.ps1 -CliPath ./src/ArifCE.Cli/bin/Release/net10.0/arifce.dll -ClaimId CLAIM-0001 -TestProject ArifCE.slnx -PathCsv src,tests'; aggregated_output='CLAIM-0001: VERIFIED (EVIDENCE-0001)'; exit_code=0 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='dotnet ./src/ArifCE.Cli/bin/Release/net10.0/arifce.dll verify CLAIM-0001 --command "dotnet test ArifCE.slnx --no-restore"'; aggregated_output=('v' * 80); exit_code=0 } },
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
    if ($metrics.arifceCommandCount -ne 2 -or $metrics.arifceOverheadTokens -le 20) { throw 'The actual arifce.dll invocation and serialized verification wrapper were not attributed as ArifCE overhead.' }

    $directBuildLog = Join-Path $root 'direct-build.jsonl'
    $directBuildEvent = @{ type='item.completed'; item=@{ type='command_execution'; command='pwsh -Command "dotnet test ArifCE.slnx --no-restore"'; aggregated_output='full direct output'; exit_code=0 } }
    [IO.File]::WriteAllText($directBuildLog, ($directBuildEvent | ConvertTo-Json -Compress -Depth 5))
    $directBuildMetrics = Read-BenchmarkContextEfficiency $directBuildLog ([pscustomobject]@{ inputTokens=100 })
    if ($directBuildMetrics.unboundedBuildTestCount -ne 1 -or 'UNBOUNDED_BUILD_TEST_OUTPUT' -notin $directBuildMetrics.policyViolations) { throw 'A real direct dotnet test escaped the bounded-output policy.' }

    $repeatFailureLog = Join-Path $root 'repeat-failure.jsonl'
    $repeatFailures = @(
        @{ type='item.completed'; item=@{ type='command_execution'; command='pwsh -Command ./BENCHMARK_RUN_CHECK.ps1 -Action build'; aggregated_output='error CS1001: repeated compiler failure'; exit_code=1 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='pwsh   -Command ./BENCHMARK_RUN_CHECK.ps1 -Action build'; aggregated_output='error CS1001: repeated compiler failure'; exit_code=1 } }
    )
    [IO.File]::WriteAllLines($repeatFailureLog, @($repeatFailures | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 }))
    $repeatMetrics = Read-BenchmarkContextEfficiency $repeatFailureLog ([pscustomobject]@{ inputTokens=100 })
    if ('REPEATED_FAILURE_WITHOUT_REPLAN' -notin $repeatMetrics.policyViolations) { throw 'An exact repeated failed command without replanning was not rejected.' }

    $progressiveFailureLog = Join-Path $root 'progressive-failure.jsonl'
    $progressiveFailures = @(
        @{ type='item.completed'; item=@{ type='command_execution'; command='pwsh -Command ./BENCHMARK_RUN_CHECK.ps1 -Action build'; aggregated_output='error CS1001: first compiler failure'; exit_code=1 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command='pwsh -Command ./BENCHMARK_RUN_CHECK.ps1 -Action build'; aggregated_output='error CS2002: different compiler failure'; exit_code=1 } }
    )
    [IO.File]::WriteAllLines($progressiveFailureLog, @($progressiveFailures | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 }))
    $progressiveMetrics = Read-BenchmarkContextEfficiency $progressiveFailureLog ([pscustomobject]@{ inputTokens=100 })
    if ('REPEATED_FAILURE_WITHOUT_REPLAN' -in $progressiveMetrics.policyViolations) { throw 'A repeated command exposing a different failure was misclassified as a retry loop.' }

    $sharedPrefixLog = Join-Path $root 'shared-prefix-failures.jsonl'
    $wrapper = 'C:\a-very-long-host-runtime-path-that-used-to-consume-the-truncated-signature\pwsh.exe -Command '
    $sharedPrefixFailures = @(
        @{ type='item.completed'; item=@{ type='command_execution'; command=($wrapper + './BENCHMARK_RUN_CHECK.ps1 -Action build'); aggregated_output='network failure'; exit_code=1 } },
        @{ type='item.completed'; item=@{ type='command_execution'; command=($wrapper + './BENCHMARK_RUN_CHECK.ps1 -Action test'); aggregated_output='compiler failure'; exit_code=1 } }
    )
    [IO.File]::WriteAllLines($sharedPrefixLog, @($sharedPrefixFailures | ForEach-Object { $_ | ConvertTo-Json -Compress -Depth 5 }))
    $sharedPrefixMetrics = Read-BenchmarkContextEfficiency $sharedPrefixLog ([pscustomobject]@{ inputTokens=100 })
    if ('REPEATED_FAILURE_WITHOUT_REPLAN' -in $sharedPrefixMetrics.policyViolations) { throw 'Different failed commands sharing a host-wrapper prefix were misclassified as a retry loop.' }
    Write-Output 'Benchmark context-efficiency measurement and policy smoke test passed.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
