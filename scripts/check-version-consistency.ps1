$ErrorActionPreference = 'Stop'
$project = Get-Content (Join-Path $PSScriptRoot '..\src\ArifCE.Cli\ArifCE.Cli.csproj') -Raw
$readme = Get-Content (Join-Path $PSScriptRoot '..\README.md') -Raw
$projectVersion = [regex]::Match($project, '<Version>(?<v>[^<]+)</Version>').Groups['v'].Value
$readmeVersion = [regex]::Match($readme, 'V(?<v>\d+\.\d+\.\d+) is published').Groups['v'].Value
if ([string]::IsNullOrWhiteSpace($projectVersion) -or $projectVersion -ne $readmeVersion) {
    throw "README version '$readmeVersion' does not match CLI package version '$projectVersion'."
}
Write-Host "Version consistency: $projectVersion"
