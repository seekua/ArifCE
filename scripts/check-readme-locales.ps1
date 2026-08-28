$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$required = @('ArifCE.svg','mermaid','dotnet tool install','arifce init','ROADMAP.md','SECURITY.md','CONTRIBUTING.md','Apache')
$files = Get-ChildItem (Join-Path $root 'README.*.md')
$failed = @()
foreach ($file in $files) {
  $text = Get-Content $file.FullName -Raw
  foreach ($token in $required) { if ($text -notmatch [regex]::Escape($token)) { $failed += "$($file.Name): missing $token" } }
}
if ($failed.Count -gt 0) {
  $failed | ForEach-Object { Write-Output "ERROR: $_" }
  Write-Output "README locale parity failed with $($failed.Count) missing markers."
  exit 1
}
Write-Output "Validated $($files.Count) localized README files against canonical markers."
