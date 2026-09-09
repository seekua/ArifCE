[CmdletBinding()]
param(
    [string]$Manifest = 'benchmarks/engineering-tasks.json',
    [string]$SuiteRoot = 'artifacts/engineering-benchmark',
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$Seed,
    [string]$Output = 'execution-plan.json'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
function Resolve-RepoPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $repo $Path))
}
function Get-Sha256Text([string]$Value) {
    $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}
function Get-Sha256File([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try { return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
    finally { $stream.Dispose() }
}

$manifestPath = Resolve-RepoPath $Manifest
$suite = Resolve-RepoPath $SuiteRoot
if (-not (Test-Path -LiteralPath $suite -PathType Container)) { throw 'Prepared benchmark suite is missing.' }
$definition = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($definition.schemaVersion -ne 3) { throw 'Balanced execution plans require benchmark manifest schema 3.' }
$expectedPairs = [int]$definition.tasks.Count * [int]$definition.repetitions
$expectedRuns = $expectedPairs * 2
$sessions = @(Get-ChildItem -LiteralPath $suite -Filter session.json -File -Recurse | ForEach-Object {
    $path = $_.FullName
    $session = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    [pscustomobject]@{ Path = $path; Root = Split-Path -Parent $path; Session = $session }
})
if ($sessions.Count -ne $expectedRuns) { throw "Expected $expectedRuns prepared sessions, found $($sessions.Count)." }

$pairs = @()
foreach ($task in $definition.tasks) {
    foreach ($trial in 1..([int]$definition.repetitions)) {
        $members = @($sessions | Where-Object { $_.Session.taskId -ceq $task.id -and [int]$_.Session.trial -eq $trial })
        if ($members.Count -ne 2 -or (@($members.Session.arm | Sort-Object) -join ',') -cne 'arifce,baseline') { throw "Task $($task.id) trial $trial is not a complete matched pair." }
        foreach ($member in $members) {
            if ($member.Session.state -cne 'PREPARED') { throw "Task $($task.id) trial $trial is not prepared." }
            if ($member.Session.model -cne $members[0].Session.model -or $member.Session.tokenBudget -ne $members[0].Session.tokenBudget -or $member.Session.permissionProfile -cne $members[0].Session.permissionProfile -or $member.Session.fixtureTree -cne $members[0].Session.fixtureTree) { throw "Task $($task.id) trial $trial is not matched." }
        }
        $pairs += [pscustomobject]@{
            TaskId = [string]$task.id
            Trial = $trial
            SortKey = Get-Sha256Text "$Seed|$($task.id)|$trial"
            Members = $members
        }
    }
}
$orderedPairs = @($pairs | Sort-Object SortKey, TaskId, Trial)
$baselineFirstCount = [int][Math]::Floor($expectedPairs / 2)
$runs = @()
$sequence = 0
for ($pairIndex = 0; $pairIndex -lt $orderedPairs.Count; $pairIndex++) {
    $pair = $orderedPairs[$pairIndex]
    $firstArm = if ($pairIndex -lt $baselineFirstCount) { 'baseline' } else { 'arifce' }
    $secondArm = if ($firstArm -eq 'baseline') { 'arifce' } else { 'baseline' }
    foreach ($arm in @($firstArm, $secondArm)) {
        $sequence++
        $member = @($pair.Members | Where-Object { $_.Session.arm -ceq $arm })[0]
        $relativeRoot = [IO.Path]::GetRelativePath($suite, $member.Root).Replace([IO.Path]::DirectorySeparatorChar, '/')
        $runs += [ordered]@{
            sequence = $sequence
            pairSequence = $pairIndex + 1
            taskId = $pair.TaskId
            trial = $pair.Trial
            arm = $arm
            relativeTrialRoot = $relativeRoot
            sessionSha256 = Get-Sha256File $member.Path
            promptSha256 = Get-Sha256File (Join-Path $member.Root 'prompt.md')
        }
    }
}

$outputPath = if ([IO.Path]::IsPathRooted($Output)) { [IO.Path]::GetFullPath($Output) } else { Join-Path $suite $Output }
if (Test-Path -LiteralPath $outputPath) { throw "Refusing to overwrite execution plan: $outputPath" }
$plan = [ordered]@{
    schemaVersion = 1
    kind = 'balanced-reproducible-benchmark-execution-plan'
    seedSha256 = Get-Sha256Text $Seed
    manifestSha256 = Get-Sha256File $manifestPath
    pairCount = $expectedPairs
    runCount = $expectedRuns
    balance = [ordered]@{
        baselineFirst = $baselineFirstCount
        arifceFirst = $expectedPairs - $baselineFirstCount
    }
    policy = 'Matched arms stay adjacent. Pair order is SHA-256 seeded. First-arm assignment is exactly balanced when pair count is even.'
    runs = $runs
}
$plan | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputPath -Encoding utf8
Write-Output "Wrote balanced execution plan with $expectedPairs pairs and $expectedRuns runs to $outputPath. No agent was invoked."
