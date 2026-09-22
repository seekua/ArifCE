[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-run-check-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $root | Out-Null
    $project = Join-Path $root 'AuditProperty.csproj'
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <Target Name="RequireDisabledAudit" BeforeTargets="Build">
    <Error Condition="'$(NuGetAudit)' != 'false'" Text="NuGetAudit must be false for deterministic benchmark checks." />
    <Error Condition="'$(MSBUILDDISABLENODEREUSE)' != '1'" Text="MSBuild node reuse must be disabled for deterministic benchmark checks." />
  </Target>
</Project>
'@ | Set-Content -LiteralPath $project -Encoding utf8
    $shell = (Get-Process -Id $PID).Path
    $check = Join-Path $PSScriptRoot 'benchmark-run-check.ps1'
    $escapedCheck = $check.Replace("'", "''")
    $escapedProject = $project.Replace("'", "''")
    $command = "& '$escapedCheck' -Action build -Project '$escapedProject' -AdditionalArguments @('-p:NuGetAudit=true')"
    $encodedCommand = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $output = @(& $shell -NoProfile -EncodedCommand $encodedCommand 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "Benchmark run check did not enforce offline audit policy: $($output -join [Environment]::NewLine)" }
    $record = ($output | Select-Object -Last 1) | ConvertFrom-Json
    if ($record.exitCode -ne 0 -or $record.action -ne 'build') { throw 'Benchmark run check returned an invalid compact success record.' }

    $lockHelper = Join-Path $PSScriptRoot 'benchmark-operation-lock.ps1'
    $worker = Join-Path $root 'lock-worker.ps1'
    $sequence = Join-Path $root 'lock-sequence.txt'
    @'
param([string]$Helper, [string]$LockRoot, [string]$Sequence)
. $Helper
Invoke-WithBenchmarkOperationLock -Root $LockRoot -TimeoutSeconds 10 -Operation {
    [IO.File]::AppendAllText($Sequence, "start:$PID`n")
    Start-Sleep -Milliseconds 750
    [IO.File]::AppendAllText($Sequence, "end:$PID`n")
}
'@ | Set-Content -LiteralPath $worker -Encoding utf8
    function Start-LockWorker {
        $start = [Diagnostics.ProcessStartInfo]::new($shell)
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        foreach ($argument in @('-NoProfile','-File',$worker,$lockHelper,$root,$sequence)) { $start.ArgumentList.Add($argument) }
        return [Diagnostics.Process]::Start($start)
    }
    $first = Start-LockWorker
    $second = Start-LockWorker
    try {
        if (-not $first.WaitForExit(15000) -or -not $second.WaitForExit(15000)) { throw 'Benchmark operation lock workers timed out.' }
        if ($first.ExitCode -ne 0 -or $second.ExitCode -ne 0) { throw 'Benchmark operation lock worker failed.' }
    }
    finally {
        if (-not $first.HasExited) { $first.Kill($true) }
        if (-not $second.HasExited) { $second.Kill($true) }
        $first.Dispose(); $second.Dispose()
    }
    $events = @(Get-Content -LiteralPath $sequence)
    if ($events.Count -ne 4) { throw 'Benchmark operation lock did not record both workers.' }
    foreach ($offset in @(0, 2)) {
        $startPid = $events[$offset] -replace '^start:', ''
        $endPid = $events[$offset + 1] -replace '^end:', ''
        if ($startPid -ne $endPid) { throw 'Benchmark build/test operations overlapped instead of serializing.' }
    }
    Write-Output 'Benchmark run check enforces deterministic audit/node-reuse policy, compact output, and serialized build/test operations.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
