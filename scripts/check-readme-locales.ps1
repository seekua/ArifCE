$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$canonical = Get-Content (Join-Path $root 'README.md') -Raw
$translationStatus = Get-Content (Join-Path $root 'docs/TRANSLATION-STATUS.md') -Raw
$required = @('ArifCE.svg','mermaid','dotnet tool install','arifce init','ROADMAP.md','SECURITY.md','CONTRIBUTING.md','Apache')
$required += [regex]::Matches($canonical, '(?m)^#{1,6}\s+.+$') | ForEach-Object { $_.Value.Trim() }
$files = Get-ChildItem (Join-Path $root 'README.*.md')
$failed = @()
foreach ($file in $files) {
  $text = Get-Content $file.FullName -Raw
  if ($text.Length -lt ($canonical.Length * 0.85)) {
    $failed += "$($file.Name): content is shorter than the canonical README (less than 85 percent of canonical length)"
  }
  foreach ($token in $required) { if ($text -notmatch [regex]::Escape($token)) { $failed += "$($file.Name): missing $token" } }
}
$listed = [regex]::Matches($translationStatus, '`(README\.[^`]+\.md)`') | ForEach-Object { $_.Groups[1].Value }
foreach ($file in $files) {
  if ($listed -notcontains $file.Name) { $failed += "$($file.Name): missing from docs/TRANSLATION-STATUS.md" }
}
if ($failed.Count -gt 0) {
  $failed | ForEach-Object { Write-Output "ERROR: $_" }
  Write-Output "README locale parity failed with $($failed.Count) missing markers."
  exit 1
}
Write-Output "Validated $($files.Count) localized README files against canonical markers."
