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
    Write-Output 'Benchmark run check enforces deterministic NuGet audit policy and compact success output.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
