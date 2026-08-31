[CmdletBinding()]
param(
    [string]$Output = 'docs/evidence/ab-run-v0.7.json',
    [int]$TaskCount = 20
)

$ErrorActionPreference = 'Stop'
if ($TaskCount -ne 20) { throw 'The A/B harness requires exactly 20 matched tasks.' }
$repo = Split-Path -Parent $PSScriptRoot
$cli = Join-Path $repo 'src/ArifCE.Cli/ArifCE.Cli.csproj'
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-example-ab-' + [Guid]::NewGuid().ToString('N'))
$baselineRoot = Join-Path $root 'baseline'
$arifceRoot = Join-Path $root 'arifce'
New-Item -ItemType Directory -Force -Path $baselineRoot, $arifceRoot | Out-Null

function Invoke-Measured([string]$WorkingDirectory, [string[]]$Arguments) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $output = & dotnet run --project $cli --no-restore -- @Arguments 2>&1 | Out-String
    $exit = $LASTEXITCODE
    $sw.Stop()
    [ordered]@{ durationMs = $sw.ElapsedMilliseconds; exitCode = $exit; outputChars = $output.Length }
}

try {
    Push-Location $baselineRoot; git init --quiet; Pop-Location
    Push-Location $arifceRoot; git init --quiet; & dotnet run --project $cli --no-restore -- init | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'ArifCE initialization failed.' }; Pop-Location
    $baseline = [Collections.Generic.List[object]]::new(); $arifce = [Collections.Generic.List[object]]::new()
    for ($i = 1; $i -le $TaskCount; $i++) {
        $task = "example-task-{0:D2}" -f $i
        Push-Location $baselineRoot; $b = Invoke-Measured $baselineRoot @('status'); Pop-Location
        Push-Location $arifceRoot; $a = Invoke-Measured $arifceRoot @('context', '--task', "Review $task", '--budget', '600'); Pop-Location
        $baseline.Add([ordered]@{ taskId = $task; arm = 'baseline'; success = ($b.exitCode -eq 0); durationMs = $b.durationMs; steps = 1; reads = 1; contextTokens = 0; outputChars = $b.outputChars })
        $arifce.Add([ordered]@{ taskId = $task; arm = 'arifce'; success = ($a.exitCode -eq 0); durationMs = $a.durationMs; steps = 1; reads = 1; contextTokens = [Math]::Ceiling($a.outputChars / 4); outputChars = $a.outputChars })
    }
    $result = [ordered]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); fixture = 'temporary independent example Git repositories'; fixtureCommit = 'none (generated fixture)'; taskCount = $TaskCount; commands = @('git init + arifce status', 'arifce init + arifce context --task ... --budget 600'); baseline = $baseline; arifce = $arifce; interpretation = 'Raw matched-task smoke metadata only. This fixture does not measure correctness or claim product effectiveness.' }
    $path = Join-Path $repo $Output; New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null; $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding utf8
    Write-Output "Wrote $Output (20 matched tasks)."
}
finally { Pop-Location; if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
