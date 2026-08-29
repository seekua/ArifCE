param([switch]$RequireReviewed)
$root = Split-Path -Parent $PSScriptRoot
$status = Get-Content (Join-Path $root 'docs/TRANSLATION-STATUS.md') -Raw
$files = Get-ChildItem (Join-Path $root 'README.*.md')
$pending = @()
foreach ($file in $files) {
  $row = ($status -split "`r?`n" | Where-Object { $_ -match [regex]::Escape($file.Name) } | Select-Object -First 1)
  if (-not $row) { $pending += "$($file.Name): missing status row"; continue }
  $review = (($row -split '\|') | Select-Object -Last 1).Trim()
  if ($review -notmatch '(?i)reviewed') { $pending += "$($file.Name): $review" }
}
if ($pending.Count -gt 0) {
  Write-Output "Human translation review pending for $($pending.Count) of $($files.Count) localized README files."
  $pending | ForEach-Object { Write-Output "PENDING: $_" }
  if ($RequireReviewed) { exit 1 }
} else { Write-Output "All localized README files are marked Reviewed." }
