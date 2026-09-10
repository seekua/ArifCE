[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('restore','build','test')][string]$Action,
    [string]$Project = 'ArifCE.slnx',
    [string[]]$AdditionalArguments = @()
)

$ErrorActionPreference = 'Stop'
$log = Join-Path ([IO.Path]::GetTempPath()) ('arifce-check-' + [Guid]::NewGuid().ToString('N') + '.log')
$started = [Diagnostics.Stopwatch]::StartNew()
try {
    $arguments = @($Action, $Project, '--disable-build-servers', '--maxcpucount:1', '--nologo', '--verbosity:minimal') + $AdditionalArguments
    & dotnet @arguments *> $log
    $exitCode = $LASTEXITCODE
    $started.Stop()
    $lines = @(Get-Content -LiteralPath $log)
    $warnings = @($lines | Select-String -Pattern '\bwarning\b' -CaseSensitive:$false).Count
    if ($exitCode -eq 0) {
        $testSummary = $lines | Select-String -Pattern 'Passed!|Failed!|Test summary|passed:|failed:' -CaseSensitive:$false | Select-Object -Last 1
        [pscustomobject][ordered]@{
            action = $Action
            exitCode = 0
            durationMs = $started.ElapsedMilliseconds
            warnings = $warnings
            summary = if ($null -eq $testSummary) { "$Action completed successfully." } else { $testSummary.Line.Trim() }
        } | ConvertTo-Json -Compress
        exit 0
    }
    [Console]::Error.WriteLine("$Action failed (exit $exitCode, $($started.ElapsedMilliseconds) ms, $warnings warning line(s)).")
    $matches = @($lines | Select-String -Pattern '\berror\b|\bfailed\b|exception|stack trace' -CaseSensitive:$false -Context 2,2 | Select-Object -First 12)
    if ($matches.Count -eq 0) { $lines | Select-Object -Last 30 | ForEach-Object { [Console]::Error.WriteLine($_) } }
    else { $matches | ForEach-Object { [Console]::Error.WriteLine($_.ToString()) } }
    [Console]::Error.WriteLine("Full log retained outside the checkout: $log")
    exit $exitCode
}
finally {
    if ($LASTEXITCODE -eq 0 -and (Test-Path -LiteralPath $log)) { Remove-Item -LiteralPath $log -Force }
}
