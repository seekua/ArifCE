param(
  [Parameter(Mandatory=$true)][string]$Baseline,
  [Parameter(Mandatory=$true)][string]$Arifce,
  [string]$Output = "docs/evidence/ab-run.json"
)

# The harness only normalizes caller-provided raw results; it never invents quality outcomes.
$baselineRows = Get-Content -LiteralPath $Baseline -Raw | ConvertFrom-Json
$arifceRows = Get-Content -LiteralPath $Arifce -Raw | ConvertFrom-Json
if ($baselineRows.Count -ne 20 -or $arifceRows.Count -ne 20) { throw "Exactly 20 matched tasks are required in each arm." }
$ids = @($baselineRows | ForEach-Object taskId | Sort-Object)
$otherIds = @($arifceRows | ForEach-Object taskId | Sort-Object)
if (($ids -join "`n") -ne (($ids | Sort-Object -Unique) -join "`n") -or ($ids -join "`n") -ne ($otherIds -join "`n")) { throw "Task identifiers must match one-to-one." }
$result = [ordered]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); taskCount = 20; baseline = $baselineRows; arifce = $arifceRows; interpretation = 'Raw matched-task metadata only; no product effectiveness claim.' }
$dir = Split-Path -Parent $Output; if ($dir) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Output -Encoding utf8
Write-Output "Wrote $Output (20 matched tasks)."
