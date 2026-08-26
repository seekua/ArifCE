param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$smokeRoot = Join-Path $temporaryBase ('arifce-package-smoke-' + [Guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $smokeRoot 'packages'
$toolDirectory = Join-Path $smokeRoot 'tools'
$repositoryDirectory = Join-Path $smokeRoot 'repository'

try {
    New-Item -ItemType Directory -Force -Path $packageDirectory, $toolDirectory, $repositoryDirectory | Out-Null
    dotnet pack (Join-Path $PSScriptRoot '..\src\ArifCE.Cli\ArifCE.Cli.csproj') -c $Configuration --no-restore -o $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

    dotnet tool install --tool-path $toolDirectory --add-source $packageDirectory --ignore-failed-sources ArifCE.Cli --version 0.1.0
    if ($LASTEXITCODE -ne 0) { throw 'Tool installation failed.' }

    git -C $repositoryDirectory init | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Temporary Git initialization failed.' }

    $executable = Join-Path $toolDirectory ($(if ($IsWindows) { 'arifce.exe' } else { 'arifce' }))
    Push-Location $repositoryDirectory
    try {
        & $executable init
        if ($LASTEXITCODE -ne 0) { throw 'Initial arifce init failed.' }
        & $executable init
        if ($LASTEXITCODE -ne 0) { throw 'Idempotent arifce init failed.' }
        & $executable status
        if ($LASTEXITCODE -ne 0) { throw 'arifce status failed.' }
        Remove-Item -Force -LiteralPath (Join-Path $repositoryDirectory '.arifce/index/arifce.db')
        & $executable rebuild
        if ($LASTEXITCODE -ne 0) { throw 'arifce rebuild failed.' }
        & $executable context 'continue project work' --budget 400
        if ($LASTEXITCODE -ne 0) { throw 'arifce context failed.' }
        & $executable doctor
        if ($LASTEXITCODE -ne 0) { throw 'arifce doctor failed.' }
    }
    finally {
        Pop-Location
    }

    Write-Output 'Package smoke test passed.'
}
finally {
    $resolvedSmokeRoot = [System.IO.Path]::GetFullPath($smokeRoot)
    if ($resolvedSmokeRoot.StartsWith((Join-Path $temporaryBase 'arifce-package-smoke-'), [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedSmokeRoot)) {
        Remove-Item -Recurse -Force -LiteralPath $resolvedSmokeRoot
    }
}
