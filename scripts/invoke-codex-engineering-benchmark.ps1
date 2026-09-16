[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TrialRoot,
    [Parameter(Mandatory)][string]$Model,
    [Parameter(Mandatory)][ValidateSet('low','medium','high','xhigh','max','ultra')][string]$Reasoning,
    [ValidatePattern('^[a-z0-9][a-z0-9._-]{2,127}$')][string]$PermissionProfile = 'preauthorized-write-build-v1',
    [string]$CodexExecutable = (Get-Command codex -ErrorAction Stop).Source,
    [ValidateRange(1, 86400)][int]$TimeoutSeconds = 1800
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'codex-benchmark-host.ps1')

$arguments = Get-CodexBenchmarkHostArguments `
    -Model $Model `
    -Reasoning $Reasoning `
    -PermissionProfile $PermissionProfile

& (Join-Path $PSScriptRoot 'invoke-engineering-benchmark-host.ps1') `
    -TrialRoot $TrialRoot `
    -Executable $CodexExecutable `
    -HostArguments $arguments `
    -TimeoutSeconds $TimeoutSeconds
