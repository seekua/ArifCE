$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$canonical = Get-Content (Join-Path $root 'README.md') -Raw
$translationStatus = Get-Content (Join-Path $root 'docs/TRANSLATION-STATUS.md') -Raw
$required = @('ArifCE.svg','mermaid','dotnet tool install','arifce init','ROADMAP.md','SECURITY.md','CONTRIBUTING.md','Apache')
$languageSelector = ($canonical -split "`r?`n" | Where-Object { $_ -match '^\[English\]\(README\.md\)' } | Select-Object -First 1).Trim()
$localizedLanguageSelector = $languageSelector.Replace('(README.md)', '(../../README.md)').Replace('(locales/', '(')
$canonicalHeadingCount = [regex]::Matches($canonical, '(?m)^#{1,6}\s+.+$').Count
$canonicalFenceCount = [regex]::Matches($canonical, '(?m)^```').Count
$canonicalMermaidCount = [regex]::Matches($canonical, '(?m)^```mermaid').Count
$canonicalBadgeCount = [regex]::Matches($canonical, 'https://img.shields.io').Count
$files = Get-ChildItem (Join-Path $root 'docs/locales/README.*.md')
$failed = @()
foreach ($file in $files) {
  $text = Get-Content $file.FullName -Raw
  # Translation can be substantially more compact than English (especially
  # Japanese, Chinese, and Arabic). Structural markers and heading parity are
  # authoritative; use a conservative 45% floor for accidental content loss.
  if ($text.Length -lt ($canonical.Length * 0.45)) {
    $failed += "$($file.Name): content is shorter than the canonical README (less than 45 percent of canonical length)"
  }
  if ([regex]::Matches($text, '(?m)^#{1,6}\s+.+$').Count -lt $canonicalHeadingCount) {
    $failed += "$($file.Name): fewer Markdown headings than the canonical README"
  }
  if ([regex]::Matches($text, '(?m)^```').Count -ne $canonicalFenceCount) {
    $failed += "$($file.Name): code-fence count differs from the canonical README"
  }
  if ([regex]::Matches($text, '(?m)^```mermaid').Count -ne $canonicalMermaidCount) {
    $failed += "$($file.Name): Mermaid diagram count differs from the canonical README"
  }
  if ([regex]::Matches($text, 'https://img.shields.io').Count -ne $canonicalBadgeCount) {
    $failed += "$($file.Name): badge count differs from the canonical README"
  }
  if ($text -notmatch '(?m)^\*\*[^*]+\*\*\s*$') {
    $failed += "$($file.Name): missing translated slogan"
  }
  if ($text -notmatch '(?m)^>\s+\S+') {
    $failed += "$($file.Name): missing translated context quote"
  }
  if ($text -notmatch [regex]::Escape($localizedLanguageSelector)) {
    $failed += "$($file.Name): language selector does not match canonical links"
  }
  if ([regex]::Matches($text, '\.\./\.\./assets/ArifCE\.svg').Count -ne 1) {
    $failed += "$($file.Name): expected exactly one ArifCE logo"
  }
  if ([regex]::Matches($text, '(?m)^\[English\]\(\.\./\.\./README\.md\)').Count -ne 1) {
    $failed += "$($file.Name): expected exactly one language selector"
  }
  foreach ($match in [regex]::Matches($text, '\]\(([^)]+)\)')) {
    $target = $match.Groups[1].Value
    if ($target -notmatch '^(https?://|#|mailto:)') {
      $targetPath = Join-Path $file.DirectoryName ($target -replace '#.*$','')
      if (-not (Test-Path -LiteralPath $targetPath)) {
        $failed += "$($file.Name): broken local link $target"
      }
    }
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
