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
foreach ($rid in $Runtime) {
    $output = Join-Path $OutputRoot $rid
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    dotnet publish $project --configuration $Configuration --runtime $rid --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output $output
    $binary = if ($rid -like 'win-*') { Join-Path $output 'arifce.exe' } else { Join-Path $output 'arifce' }
    if (-not (Test-Path -LiteralPath $binary)) { throw "Expected published binary was not produced: $binary" }
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $binary
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $binary) | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS') -Encoding ascii
    Write-Host "Published $rid -> $binary"
}
