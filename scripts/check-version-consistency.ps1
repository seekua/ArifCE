$ErrorActionPreference = 'Stop'
$project = Get-Content (Join-Path $PSScriptRoot '..\src\ArifCE.Cli\ArifCE.Cli.csproj') -Raw
$readme = Get-Content (Join-Path $PSScriptRoot '..\README.md') -Raw
$installation = Get-Content (Join-Path $PSScriptRoot '..\docs\getting-started\installation.md') -Raw
$userGuide = Get-Content (Join-Path $PSScriptRoot '..\docs\USER-GUIDE.md') -Raw
$npm = Get-Content (Join-Path $PSScriptRoot '..\npm\arifce\package.json') -Raw | ConvertFrom-Json
$projectVersion = [regex]::Match($project, '<Version>(?<v>[^<]+)</Version>').Groups['v'].Value
$versions = [ordered]@{
    README = [regex]::Match($readme, 'V(?<v>\d+\.\d+\.\d+) is the current release').Groups['v'].Value
    Installation = [regex]::Match($installation, 'ArifCE V(?<v>\d+\.\d+\.\d+)').Groups['v'].Value
    UserGuide = [regex]::Match($userGuide, 'releases/download/v(?<v>\d+\.\d+\.\d+)').Groups['v'].Value
    NpmLauncher = [string]$npm.version
}
if ([string]::IsNullOrWhiteSpace($projectVersion)) { throw 'CLI package version is missing.' }
foreach ($entry in $versions.GetEnumerator()) {
    if ($entry.Value -ne $projectVersion) { throw "$($entry.Key) version '$($entry.Value)' does not match CLI package version '$projectVersion'." }
}
Write-Host "Version consistency: $projectVersion"
