[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'codex-benchmark-host.ps1')

function Assert-Sequence([string[]]$Actual, [string[]]$Expected, [string]$Name) {
    if (($Actual | ConvertTo-Json -Compress) -cne ($Expected | ConvertTo-Json -Compress)) {
        throw "$Name arguments differ. Actual: $($Actual -join ' | ')"
    }
}

$common = @(
    'exec', '--json', '--ephemeral', '--ignore-user-config', '--ignore-rules',
    '--model', 'gpt-5.6-terra',
    '-c', 'model_reasoning_effort="medium"'
)
$windows = @(Get-CodexBenchmarkHostArguments -Model gpt-5.6-terra -Reasoning medium -PermissionProfile preauthorized-write-build-v1 -WindowsHost $true)
$nonWindows = @(Get-CodexBenchmarkHostArguments -Model gpt-5.6-terra -Reasoning medium -PermissionProfile preauthorized-write-build-v1 -WindowsHost $false)

Assert-Sequence $windows @($common + @('-c','windows.sandbox="unelevated"','--approve-for-me','-')) 'Windows'
Assert-Sequence $nonWindows @($common + @('--approve-for-me','-')) 'Non-Windows'

$rejected = $false
try { Get-CodexBenchmarkHostArguments -Model gpt-5.6-terra -Reasoning medium -PermissionProfile unknown-profile | Out-Null }
catch { $rejected = $true }
if (-not $rejected) { throw 'Unknown permission profile was accepted.' }
if ('--dangerously-bypass-approvals-and-sandbox' -in $windows -or '--sandbox' -in $windows) { throw 'Unsafe or incompatible sandbox flags entered the benchmark host profile.' }

Write-Output 'Codex benchmark host arguments are deterministic and Windows sandbox selection is explicit.'
