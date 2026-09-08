[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Model,
    [Parameter(Mandatory = $true)][ValidateRange(1, [int]::MaxValue)][int]$TokenBudget,
    [ValidatePattern('^[a-z0-9][a-z0-9._-]{2,127}$')][string]$PermissionProfile = 'local-unverified',
    [string]$Manifest = 'benchmarks/engineering-tasks.json',
    [string]$OutputRoot = 'artifacts/engineering-benchmark'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$manifestPath = if ([IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repo $Manifest }
$definition = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$repetitions = if ($definition.schemaVersion -eq 3) { [int]$definition.repetitions } else { 1 }
foreach ($task in $definition.tasks) {
    for ($trial = 1; $trial -le $repetitions; $trial++) {
        foreach ($arm in @('baseline', 'arifce')) {
            & (Join-Path $PSScriptRoot 'new-engineering-benchmark-trial.ps1') -TaskId $task.id -Arm $arm -Model $Model -TokenBudget $TokenBudget -Trial $trial -PermissionProfile $PermissionProfile -Manifest $manifestPath -OutputRoot $OutputRoot
        }
    }
}
Write-Output "Prepared $($definition.tasks.Count * 2 * $repetitions) isolated trials. No agent was invoked."
