[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TrialRoot,
    [string]$RawLog,
    [ValidateRange(0, [long]::MaxValue)]
    [long]$TokensConsumed = 0,
    [ValidateSet('provider', 'agent-host', 'unavailable')]
    [string]$TokenSource = 'unavailable',
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
$trial = [IO.Path]::GetFullPath($TrialRoot)
$sessionPath = Join-Path $trial 'session.json'
$promptPath = Join-Path $trial 'prompt.md'
$checkout = Join-Path $trial 'checkout'
$resultPath = Join-Path $trial 'result.json'
$agentLogPath = Join-Path $trial 'agent.log'
$patchPath = Join-Path $trial 'change.patch'
$evaluatorLogPath = Join-Path $trial 'evaluator.log'
function Hash-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required provenance file is missing: $Path" }
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}
function Git-One([string[]]$Arguments) {
    $output = @(& git -C $checkout @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)" }
    return ($output | Select-Object -First 1).Trim()
}

if (-not (Test-Path -LiteralPath $sessionPath -PathType Leaf) -or -not (Test-Path -LiteralPath $checkout -PathType Container)) {
    throw 'TrialRoot must contain session.json and checkout.'
}
$session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json

if ($VerifyOnly) {
    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'result.json is missing.' }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    foreach ($pair in @(
        @($sessionPath, $result.provenance.sessionSha256),
        @($promptPath, $result.provenance.promptSha256),
        @($agentLogPath, $result.provenance.agentLogSha256),
        @($patchPath, $result.provenance.patchSha256),
        @($evaluatorLogPath, $result.evaluation.outputSha256)
    )) {
        if ((Hash-File $pair[0]) -ne $pair[1]) { throw "Provenance hash mismatch: $($pair[0])" }
    }
    $head = Git-One @('rev-parse', 'HEAD')
    $tree = Git-One @('rev-parse', 'HEAD^{tree}')
    if ($head -ne $result.provenance.finalCommit -or $tree -ne $result.provenance.finalTree) { throw 'Checkout no longer matches the recorded final commit and tree.' }
    if ([bool]$result.evaluation.checksPassed -ne ([int]$result.evaluation.exitCode -eq 0)) { throw 'Evaluator outcome is internally inconsistent.' }
    Write-Output "Verified benchmark provenance for $($result.taskId)/$($result.arm)."
    return
}

if (Test-Path -LiteralPath $resultPath) { throw "Completed trial will not be overwritten: $resultPath" }
if ([string]::IsNullOrWhiteSpace($RawLog) -or -not (Test-Path -LiteralPath $RawLog -PathType Leaf)) { throw '-RawLog must name the captured agent-host log.' }
$status = @(& git -C $checkout status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect the trial checkout.' }
if ($status.Count -ne 0) { throw 'Commit all trial changes before completion; dirty results are rejected.' }
$finalCommit = Git-One @('rev-parse', 'HEAD')
$finalTree = Git-One @('rev-parse', 'HEAD^{tree}')
if ($finalCommit -eq $session.isolatedCommit -or $finalTree -eq $session.fixtureTree) { throw 'The trial contains no committed candidate change.' }

$resolvedRawLog = [IO.Path]::GetFullPath($RawLog)
if ($resolvedRawLog -ne $agentLogPath) { Copy-Item -LiteralPath $resolvedRawLog -Destination $agentLogPath }
@(& git -C $checkout diff --binary $session.isolatedCommit $finalCommit -- .) | Set-Content -LiteralPath $patchPath -Encoding utf8
if ($LASTEXITCODE -ne 0 -or (Get-Item -LiteralPath $patchPath).Length -eq 0) { throw 'Unable to capture a non-empty candidate patch.' }

$started = [DateTimeOffset]::Parse([string]$session.preparedAtUtc)
$testStarted = [DateTimeOffset]::UtcNow
Push-Location $checkout
try {
    & dotnet test ArifCE.slnx --configuration Release *> $evaluatorLogPath
    $exitCode = $LASTEXITCODE
}
finally { Pop-Location }
$completed = [DateTimeOffset]::UtcNow

$result = [ordered]@{
    schemaVersion = 1
    runId = $session.runId
    taskId = $session.taskId
    arm = $session.arm
    fixtureCommit = $session.fixtureCommit
    model = $session.model
    tokenBudget = $session.tokenBudget
    durationMs = [Math]::Max(0, [long]($completed - $started).TotalMilliseconds)
    tokensConsumed = $TokensConsumed
    tokenSource = $TokenSource
    provenance = [ordered]@{
        sessionSha256 = Hash-File $sessionPath
        promptSha256 = Hash-File $promptPath
        agentLogSha256 = Hash-File $agentLogPath
        initialTree = $session.fixtureTree
        finalCommit = $finalCommit
        finalTree = $finalTree
        patchSha256 = Hash-File $patchPath
    }
    evaluation = [ordered]@{
        kind = 'dotnet-test'
        command = 'dotnet test ArifCE.slnx --configuration Release'
        startedAtUtc = $testStarted.ToString('O')
        completedAtUtc = $completed.ToString('O')
        exitCode = $exitCode
        checksPassed = ($exitCode -eq 0)
        outputSha256 = Hash-File $evaluatorLogPath
    }
    interpretation = 'Deterministic repository checks only. This is not an independently scored task-success claim.'
}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $resultPath -Encoding utf8
& $PSCommandPath -TrialRoot $trial -VerifyOnly
