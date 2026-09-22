[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CliPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)+$')][string]$ClaimId,
    [string]$TestProject = 'ArifCE.slnx',
    [string]$Filter,
    [string[]]$Path = @(),
    [ValidateRange(1, 3600)][int]$LockTimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'benchmark-operation-lock.ps1')

if (-not (Test-Path -LiteralPath $CliPath -PathType Leaf)) { throw "ArifCE CLI assembly was not found: $CliPath" }
foreach ($value in @($TestProject, $Filter) + $Path) {
    if ($null -ne $value -and $value -match '[\r\n"]') { throw 'Benchmark verification arguments must not contain quotes or line breaks.' }
}

$verificationCommand = "dotnet test $TestProject --configuration Release --no-build --no-restore --disable-build-servers --maxcpucount:1 --nologo --verbosity:quiet -p:NuGetAudit=false"
if (-not [string]::IsNullOrWhiteSpace($Filter)) { $verificationCommand += " --filter $Filter" }
$arguments = [System.Collections.Generic.List[string]]::new()
foreach ($argument in @($CliPath, 'verify', $ClaimId, '--command', $verificationCommand)) { $arguments.Add($argument) }
foreach ($scopePath in $Path) { $arguments.Add('--path'); $arguments.Add($scopePath) }

$result = Invoke-WithBenchmarkOperationLock -Root (Get-Location).Path -TimeoutSeconds $LockTimeoutSeconds -Operation {
    $previousNodeReuse = $env:MSBUILDDISABLENODEREUSE
    try {
        $env:MSBUILDDISABLENODEREUSE = '1'
        $output = @(& dotnet @arguments 2>&1)
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
    }
    finally {
        if ($null -eq $previousNodeReuse) { Remove-Item Env:MSBUILDDISABLENODEREUSE -ErrorAction SilentlyContinue }
        else { $env:MSBUILDDISABLENODEREUSE = $previousNodeReuse }
    }
}
$result.Output | ForEach-Object { Write-Output $_ }
exit $result.ExitCode
