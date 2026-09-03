[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$scanner = Join-Path $PSScriptRoot 'secret-scan.ps1'
$parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$root = Join-Path $parent ('arifce-secret-scan-test-' + [Guid]::NewGuid().ToString('N'))
try {
    $directory = Join-Path $root 'tests/ArifCE.Tests'
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $fixture = Join-Path $directory 'BenchmarkSafetyTests.cs'
    $synthetic = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('cGFzc3dvcmQ9aHVudGVyMg=='))
    Push-Location $root
    try {
        & git init --quiet
        if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize scanner fixture.' }
        [IO.File]::WriteAllText($fixture, $synthetic)
        & $scanner | Out-Null
        [IO.File]::WriteAllText($fixture, 'password=' + 'different-fixture-value')
        $rejected = $false
        try { & $scanner | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Fixture exception allowed a different credential value.' }
        [IO.File]::WriteAllText($fixture, $synthetic)
        [IO.File]::WriteAllText((Join-Path $root 'unapproved.cs'), $synthetic)
        $rejected = $false
        try { & $scanner | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Fixture exception escaped its exact file path.' }
    } finally { Pop-Location }
    Write-Output 'Exact synthetic fixture allowed; changed values and other paths rejected.'
}
finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or -not [IO.Path]::GetFileName($resolved).StartsWith('arifce-secret-scan-test-', [StringComparison]::Ordinal)) { throw 'Unsafe scanner fixture cleanup path.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
