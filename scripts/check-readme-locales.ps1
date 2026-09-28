$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$canonical = (Get-Content (Join-Path $root 'README.md') -Raw) -replace "`r`n", "`n"
$translationStatus = (Get-Content (Join-Path $root 'docs/TRANSLATION-STATUS.md') -Raw) -replace "`r`n", "`n"
$required = @('ArifCE.svg','mermaid','arifce init','ROADMAP.md','SECURITY.md','CONTRIBUTING.md','Apache')
$languageSelector = ($canonical -split "`r?`n" | Where-Object { $_ -match '^\[English\]\(README\.md\)' } | Select-Object -First 1).Trim()
$localizedLanguageSelector = $languageSelector.Replace('(README.md)', '(../../README.md)').Replace('(docs/locales/', '(')
$canonicalWithoutCode = [regex]::Replace($canonical, '(?ms)^```[^\n]*\n.*?^```[ \t]*\n?', '')
$canonicalHeadingCount = [regex]::Matches($canonicalWithoutCode, '(?m)^#{1,6}\s+.+$').Count
$canonicalHeadingLevels = @([regex]::Matches($canonicalWithoutCode, '(?m)^(#{1,6})\s+.+$') | ForEach-Object { $_.Groups[1].Value.Length })
$canonicalFenceCount = [regex]::Matches($canonical, '(?m)^```').Count
$canonicalMermaidCount = [regex]::Matches($canonical, '(?m)^```mermaid').Count
$canonicalMermaid = [regex]::Match($canonical, '(?ms)^```mermaid\s*\n(.*?)^```[ \t]*$').Groups[1].Value.Trim()
$canonicalBadgeCount = [regex]::Matches($canonical, 'https://img.shields.io').Count
$canonicalWithoutMermaid = [regex]::Replace($canonical, '(?ms)^```mermaid\s*\r?\n.*?^```[ \t]*\r?\n?', '')
$canonicalCodeBlocks = @([regex]::Matches($canonicalWithoutMermaid, '(?ms)^```[^\r\n]*\r?\n.*?^```[ \t]*$') | ForEach-Object { $_.Value -replace "`r`n", "`n" })
$canonicalTargets = @([regex]::Matches($canonical, '\]\(([^)]+)\)') | ForEach-Object { $_.Groups[1].Value })

function Convert-CanonicalTarget([string]$Target) {
  if ($Target -match '^(https?://|#|mailto:)') { return $Target }
  if ($Target -eq 'README.md') { return '../../README.md' }
  if ($Target -match '^docs/locales/(.+)$') { return $Matches[1] }
  if ($Target -match '^docs/(.+)$') { return "../$($Matches[1])" }
  return "../../$Target"
}

function Get-MarkdownStructureSignature([string]$Text) {
  $withoutCode = [regex]::Replace($Text, '(?ms)^```[^\n]*\n.*?^```[ \t]*\n?', '[CODE]')
  return @([regex]::Split($withoutCode.Trim(), '\n{2,}') | ForEach-Object {
    $block = $_.Trim()
    if ($block -eq '[CODE]') { return 'CODE' }
    if ($block -match '^#{1,6} ') { return 'HEADING' }
    if ($block -match '^- ') { return "LIST:$([regex]::Matches($block, '(?m)^- ').Count)" }
    if ($block -match '^> ') { return 'QUOTE' }
    if ($block -match '^<p ') { return 'HTML' }
    return 'PARAGRAPH'
  }) -join ','
}

$expectedLocalizedTargets = @($canonicalTargets | ForEach-Object { Convert-CanonicalTarget $_ } | Sort-Object)
$canonicalStructure = Get-MarkdownStructureSignature $canonical
$files = Get-ChildItem (Join-Path $root 'docs/locales/README.*.md')
$failed = @()
foreach ($file in $files) {
  $text = (Get-Content $file.FullName -Raw) -replace "`r`n", "`n"
  # Translation can be substantially more compact than English (especially
  # Japanese, Chinese, and Arabic). Structural markers and heading parity are
  # authoritative; use a conservative 45% floor for accidental content loss.
  if ($text.Length -lt ($canonical.Length * 0.45)) {
    $failed += "$($file.Name): content is shorter than the canonical README (less than 45 percent of canonical length)"
  }
  $textWithoutCode = [regex]::Replace($text, '(?ms)^```[^\n]*\n.*?^```[ \t]*\n?', '')
  if ([regex]::Matches($textWithoutCode, '(?m)^#{1,6}\s+.+$').Count -lt $canonicalHeadingCount) {
    $failed += "$($file.Name): fewer Markdown headings than the canonical README"
  }
  $headingLevels = @([regex]::Matches($textWithoutCode, '(?m)^(#{1,6})\s+.+$') | ForEach-Object { $_.Groups[1].Value.Length })
  if (($headingLevels -join ',') -cne ($canonicalHeadingLevels -join ',')) {
    $failed += "$($file.Name): heading levels/order differ from the canonical README"
  }
  if ([regex]::Matches($text, '(?m)^```').Count -ne $canonicalFenceCount) {
    $failed += "$($file.Name): code-fence count differs from the canonical README"
  }
  if ([regex]::Matches($text, '(?m)^```mermaid').Count -ne $canonicalMermaidCount) {
    $failed += "$($file.Name): Mermaid diagram count differs from the canonical README"
  }
  $localeMermaid = [regex]::Match($text, '(?ms)^```mermaid\s*\n(.*?)^```[ \t]*$').Groups[1].Value.Trim()
  if ($localeMermaid -ceq $canonicalMermaid) {
    $failed += "$($file.Name): Mermaid labels are still the canonical English labels"
  }
  $localeWithoutMermaid = [regex]::Replace($text, '(?ms)^```mermaid\s*\r?\n.*?^```[ \t]*\r?\n?', '')
  $localeCodeBlocks = @([regex]::Matches($localeWithoutMermaid, '(?ms)^```[^\r\n]*\r?\n.*?^```[ \t]*$') | ForEach-Object { $_.Value -replace "`r`n", "`n" })
  if ($localeCodeBlocks.Count -ne $canonicalCodeBlocks.Count) {
    $failed += "$($file.Name): non-diagram code example count differs from the canonical README"
  } else {
    for ($i = 0; $i -lt $canonicalCodeBlocks.Count; $i++) {
      if ($localeCodeBlocks[$i] -cne $canonicalCodeBlocks[$i]) {
        $failed += "$($file.Name): code example $($i + 1) differs from the canonical README"
        break
      }
    }
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
  $logoIndex = $text.IndexOf('../../assets/ArifCE.svg')
  $selectorIndex = $text.IndexOf($localizedLanguageSelector)
  $boldMatches = [regex]::Matches($text, '(?m)^\*\*[^*]+\*\*\s*$')
  if ($boldMatches.Count -lt 2 -or $logoIndex -lt 0 -or $selectorIndex -lt 0 -or
      $boldMatches[0].Index -lt $logoIndex -or $boldMatches[0].Index -gt $selectorIndex -or
      $boldMatches[1].Index -lt $selectorIndex) {
    $failed += "$($file.Name): language prompt/selector/slogan order differs from canonical"
  }
  $actualTargets = @([regex]::Matches($text, '\]\(([^)]+)\)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object)
  if (($actualTargets -join "`n") -cne ($expectedLocalizedTargets -join "`n")) {
    $failed += "$($file.Name): Markdown link targets/counts differ from canonical README"
  }
  if ((Get-MarkdownStructureSignature $text) -cne $canonicalStructure) {
    $failed += "$($file.Name): paragraph/list/code-block structure differs from canonical README"
  }
  if ($text -match '(?im)^## Installation and quick start\s*$|^### 60-second quick start\s*$|^### From source\s*$|dotnet tool install|v0\.2\.0') {
    $failed += "$($file.Name): contains stale or untranslated installation/source text"
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
