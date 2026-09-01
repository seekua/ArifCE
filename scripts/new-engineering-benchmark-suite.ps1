[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Model,
    [Parameter(Mandatory = $true)][ValidateRange(1, [int]::MaxValue)][int]$TokenBudget,
    [string]$Manifest = 'benchmarks/engineering-tasks.json',
    [string]$OutputRoot = 'artifacts/engineering-benchmark'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$manifestPath = if ([IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repo $Manifest }
$definition = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
foreach ($task in $definition.tasks) {
    foreach ($arm in @('baseline', 'arifce')) {
        & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId $task.id -Arm $arm -Model $Model -TokenBudget $TokenBudget -Manifest $manifestPath -OutputRoot $OutputRoot
    }
}
Write-Output "Prepared $($definition.tasks.Count * 2) isolated trials. No agent was invoked."
