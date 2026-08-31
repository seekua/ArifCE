$ErrorActionPreference = 'Stop'

$patterns = @(
    @{ Name = 'private-key'; Regex = '-----BEGIN [A-Z ]*PRIVATE KEY-----' },
    @{ Name = 'bearer-token'; Regex = '(?i)bearer\s+[a-z0-9._~+/=-]{8,}' },
    @{ Name = 'credential-assignment'; Regex = '(?i)(password|pwd|api[_-]?key|secret)\s*[=:]\s*[^\s;]{6,}' },
    @{ Name = 'jwt'; Regex = 'eyJ[a-zA-Z0-9_-]{8,}\.[a-zA-Z0-9_-]{8,}\.[a-zA-Z0-9_-]{8,}' }
)

$allowedFixtures = @(
    @{ Path = 'tests/ArifCE.Tests/BehaviorTests.cs'; Pattern = 'bearer-token'; Value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('QmVhcmVyIGFiYy5kZWYuZ2hp')) },
    @{ Path = 'tests/ArifCE.Tests/BehaviorTests.cs'; Pattern = 'credential-assignment'; Value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('cGFzc3dvcmQ9c2VjcmV0')) }
    @{ Path = 'tests/ArifCE.Tests/BehaviorTests.cs'; Pattern = 'credential-assignment'; Value = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('cGFzc3dvcmQ9aHVudGVyMg==')) }
)

$findings = [System.Collections.Generic.List[object]]::new()
$trackedFiles = git ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0) { throw 'Unable to enumerate tracked files.' }

foreach ($relativePath in $trackedFiles) {
    $absolutePath = Join-Path (Get-Location) $relativePath
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) { continue }
    $content = Get-Content -Raw -LiteralPath $absolutePath -ErrorAction SilentlyContinue
    if ($null -eq $content) { continue }
    foreach ($pattern in $patterns) {
        foreach ($match in [regex]::Matches($content, $pattern.Regex)) {
            $isAllowed = $allowedFixtures | Where-Object { $_.Path -eq $relativePath.Replace('\', '/') -and $_.Pattern -eq $pattern.Name -and $_.Value -eq $match.Value }
            if ($isAllowed) { continue }
            $line = 1 + ($content.Substring(0, $match.Index).Split("`n").Count - 1)
            $findings.Add([pscustomobject]@{ Path = $relativePath; Line = $line; Pattern = $pattern.Name })
        }
    }
}

if ($findings.Count -gt 0) {
    foreach ($finding in $findings) { Write-Error "Potential credential finding at $($finding.Path):$($finding.Line) [$($finding.Pattern)]" }
    throw "Secret scan failed with $($findings.Count) potential finding(s). Values were intentionally not printed."
}

Write-Output "Secret scan passed for $($trackedFiles.Count) tracked files."
