$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$files = Get-ChildItem $root -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj|\.git)\\' }
$violations = foreach ($file in $files) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $lineNumber++
        if ($line.Length -gt 200) { [pscustomobject]@{ File = $file.FullName.Substring($root.Length + 1); Line = $lineNumber; Length = $line.Length } }
    }
}
if ($violations) { $violations | Sort-Object Length -Descending | Format-Table -AutoSize; Write-Host "Long-line baseline: $($violations.Count) line(s) over 200 characters." } else { Write-Host 'Long-line baseline: no lines over 200 characters.' }
