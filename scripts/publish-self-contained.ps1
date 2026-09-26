[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')]
    [string[]]$Runtime = @('win-x64'),
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'src/ArifCE.Cli/ArifCE.Cli.csproj'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $repo 'artifacts/self-contained' }

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$outputRootFull = [System.IO.Path]::GetFullPath($OutputRoot)
foreach ($rid in $Runtime) {
    $output = [System.IO.Path]::GetFullPath((Join-Path $outputRootFull $rid))
    if ([System.IO.Path]::GetDirectoryName($output) -ne $outputRootFull) { throw "Refusing to publish outside the configured output root: $output" }
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    dotnet publish $project --configuration $Configuration --runtime $rid --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output $output
    $binary = if ($rid -like 'win-*') {
        Get-Item -LiteralPath (Join-Path $output 'arifce.exe') -ErrorAction SilentlyContinue
    } else {
        Get-Item -LiteralPath (Join-Path $output 'arifce') -ErrorAction SilentlyContinue
    }
    if ($null -eq $binary) { throw "No self-contained executable was produced in $output" }
    $binary = $binary.FullName
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $binary
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $binary) | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS') -Encoding ascii
    Write-Host "Published $rid -> $binary"
}
