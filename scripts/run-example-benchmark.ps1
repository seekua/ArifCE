[CmdletBinding()]
param(
    [string]$Output = 'docs/evidence/ab-run-v0.7.json',
    [int]$TaskCount = 20
)

$ErrorActionPreference = 'Stop'
if ($TaskCount -ne 20) { throw 'The A/B harness requires exactly 20 matched tasks.' }
$repo = Split-Path -Parent $PSScriptRoot
$entry = Join-Path $repo 'src/ArifCE.Cli/bin/Release/net10.0/ArifCE.Cli.dll'
if (-not (Test-Path -LiteralPath $entry)) { throw 'Build the CLI in Release configuration before running the benchmark.' }
$root = Join-Path ([IO.Path]::GetTempPath()) ('arifce-example-ab-' + [Guid]::NewGuid().ToString('N'))
$baselineRoot = Join-Path $root 'baseline'
$arifceRoot = Join-Path $root 'arifce'
New-Item -ItemType Directory -Force -Path $baselineRoot, $arifceRoot | Out-Null

function Invoke-Measured([string[]]$Arguments) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $output = & dotnet $entry @Arguments 2>&1 | Out-String
    $exit = $LASTEXITCODE
    $sw.Stop()
    [ordered]@{ durationMs = $sw.ElapsedMilliseconds; exitCode = $exit; outputChars = $output.Length; output = $output }
}

try {
    Push-Location $baselineRoot; git init --quiet; Pop-Location
    Push-Location $arifceRoot; git init --quiet; $init = Invoke-Measured @('init'); if ($init.exitCode -ne 0) { throw 'ArifCE initialization failed.' }; Pop-Location
    for ($i = 1; $i -le $TaskCount; $i++) {
        $token = "signalalpha{0:D2}" -f $i
        Push-Location $arifceRoot; $created = Invoke-Measured @('decision', 'create', "Decision $token", '--decision', "Keep $token behind the repository boundary", '--rationale', "Benchmark fixture rationale for $token"); Pop-Location
        if ($created.exitCode -ne 0) { throw "Could not create fixture decision $token" }
    }
    Push-Location $arifceRoot; $rebuilt = Invoke-Measured @('rebuild'); if ($rebuilt.exitCode -ne 0) { throw 'ArifCE index rebuild failed.' }; Pop-Location
    $decisionFiles = Get-ChildItem (Join-Path $arifceRoot '.arifce/decisions') -File
    $baselineBytes = ($decisionFiles | Measure-Object -Property Length -Sum).Sum
    $baseline = [Collections.Generic.List[object]]::new(); $arifce = [Collections.Generic.List[object]]::new()
    for ($i = 1; $i -le $TaskCount; $i++) {
        $task = "signalalpha{0:D2}" -f $i
        Push-Location $baselineRoot; $b = Invoke-Measured @('status'); $bHit = $false; foreach ($file in $decisionFiles) { if ((Get-Content -LiteralPath $file.FullName -Raw) -match [regex]::Escape($task)) { $bHit = $true; break } }; Pop-Location
        Push-Location $arifceRoot; $a = Invoke-Measured @('context', $task, '--budget', '600'); $aHit = $a.output -match [regex]::Escape($task); Pop-Location
        $baseline.Add([ordered]@{ taskId = $task; arm = 'baseline'; success = ($b.exitCode -eq 0 -and $bHit); relevantHit = $bHit; durationMs = $b.durationMs; steps = 1; filesRead = $decisionFiles.Count; bytesRead = $baselineBytes; contextTokens = 0; outputChars = $b.outputChars })
        $arifce.Add([ordered]@{ taskId = $task; arm = 'arifce'; success = ($a.exitCode -eq 0 -and $aHit); relevantHit = $aHit; durationMs = $a.durationMs; steps = 1; filesRead = 1; bytesRead = $a.outputChars; contextTokens = [Math]::Ceiling($a.outputChars / 4); outputChars = $a.outputChars })
    }
    $result = [ordered]@{ generatedAtUtc = [DateTime]::UtcNow.ToString('O'); fixture = 'temporary independent Git repository with 20 canonical decision records'; fixtureCommit = 'none (generated fixture)'; taskCount = $TaskCount; commands = @('baseline: git status + full decision-file scan', 'arifce: context <unique-decision-token> --budget 600'); baseline = $baseline; arifce = $arifce; interpretation = 'Raw matched-task retrieval metadata only. This fixture does not measure correctness, quality, or product effectiveness.' }
    $path = Join-Path $repo $Output; New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null; $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding utf8
    Write-Output "Wrote $Output (20 matched tasks)."
}
finally { Pop-Location; if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force } }
