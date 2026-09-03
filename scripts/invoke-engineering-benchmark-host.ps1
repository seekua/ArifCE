[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TrialRoot,
    [Parameter(Mandatory)][string]$Executable,
    [string[]]$HostArguments = @(),
    [ValidateRange(1, 86400)][int]$TimeoutSeconds = 1800
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-timing.ps1')
$trial = [IO.Path]::GetFullPath($TrialRoot)
$sessionPath = Join-Path $trial 'session.json'
$promptPath = Join-Path $trial 'prompt.md'
$checkout = Join-Path $trial 'checkout'
$session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
if (-not (Test-Path -LiteralPath $checkout -PathType Container)) { throw 'Prepared checkout is missing.' }
$sessionHash = Get-BenchmarkArtifactHash $sessionPath
$promptHash = Get-BenchmarkArtifactHash $promptPath
$prompt = [IO.File]::ReadAllText($promptPath)
foreach ($name in @('result.json','agent.log','host.stderr.log','host-timing.json')) {
    if (Test-Path -LiteralPath (Join-Path $trial $name)) { throw "Refusing to overwrite host capture: $name" }
}
# Persistent reservation: interrupted captures cannot be silently retried as the same trial.
$reservation = [IO.File]::Open((Join-Path $trial 'host-capture.started'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
$reservation.Dispose()
$process = [Diagnostics.Process]::new()
$process.StartInfo = [Diagnostics.ProcessStartInfo]::new()
$process.StartInfo.FileName = $Executable
$process.StartInfo.WorkingDirectory = $checkout
$process.StartInfo.UseShellExecute = $false
$process.StartInfo.CreateNoWindow = $true
$process.StartInfo.RedirectStandardInput = $true
$process.StartInfo.RedirectStandardOutput = $true
$process.StartInfo.RedirectStandardError = $true
foreach ($argument in $HostArguments) { $process.StartInfo.ArgumentList.Add($argument) }
$stdout = $null; $stderr = $null; $outCopy = $null; $errCopy = $null; $launched = $false
try {
    $stdout = [IO.File]::Open((Join-Path $trial 'agent.log'), [IO.FileMode]::CreateNew)
    $stderr = [IO.File]::Open((Join-Path $trial 'host.stderr.log'), [IO.FileMode]::CreateNew)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    if (-not $process.Start()) { throw 'Host process did not start.' }
    $launched = $true
    $outCopy = $process.StandardOutput.BaseStream.CopyToAsync($stdout)
    $errCopy = $process.StandardError.BaseStream.CopyToAsync($stderr)
    $inputWrite = $process.StandardInput.WriteAsync($prompt)
    if (-not $inputWrite.Wait($TimeoutSeconds * 1000)) { throw 'Host prompt delivery timed out.' }
    $process.StandardInput.Close()
    $remaining = [Math]::Max(0, $TimeoutSeconds * 1000 - $watch.ElapsedMilliseconds)
    if (-not $process.WaitForExit([int]$remaining)) { throw 'Host execution timed out.' }
    $watch.Stop()
    # Bound pipe draining too: a detached child must not keep capture alive indefinitely.
    if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($outCopy, $errCopy), 5000)) { throw 'Host output streams did not close.' }
    $stdout.Dispose(); $stdout = $null
    $stderr.Dispose(); $stderr = $null
    if ((Get-BenchmarkArtifactHash $sessionPath) -cne $sessionHash -or (Get-BenchmarkArtifactHash $promptPath) -cne $promptHash) { throw 'Trial instructions changed during capture.' }
    $record = [ordered]@{
        schemaVersion = 1
        kind = 'host-process-elapsed'
        runId = $session.runId
        elapsedTicks = $watch.ElapsedTicks
        stopwatchFrequency = [Diagnostics.Stopwatch]::Frequency
        exitCode = $process.ExitCode
        sessionSha256 = $sessionHash
        promptSha256 = $promptHash
        agentLogSha256 = Get-BenchmarkArtifactHash (Join-Path $trial 'agent.log')
        stderrSha256 = Get-BenchmarkArtifactHash (Join-Path $trial 'host.stderr.log')
    }
    # No command arguments or environment variables are persisted: they may contain credentials.
    $record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $trial 'host-timing.json') -Encoding utf8
    Read-BenchmarkHostTiming $trial | Out-Null
    Write-Output "Captured host exit $($process.ExitCode). This is process elapsed time, not active model work."
}
finally {
    if ($launched -and -not $process.HasExited) { $process.Kill($true); $process.WaitForExit(5000) | Out-Null }
    if ($null -ne $stdout) { $stdout.Dispose() }
    if ($null -ne $stderr) { $stderr.Dispose() }
    $process.Dispose()
}
