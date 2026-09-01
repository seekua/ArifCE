[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-z0-9]+(?:-[a-z0-9]+)*$')]
    [string]$TaskId,

    [Parameter(Mandatory = $true)]
    [ValidateSet('baseline', 'arifce')]
    [string]$Arm,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Model,

    [Parameter(Mandatory = $true)]
    [ValidateRange(1, [int]::MaxValue)]
    [int]$TokenBudget,

    [string]$Manifest = 'benchmarks/engineering-tasks.json',
    [string]$OutputRoot = 'artifacts/engineering-benchmark'
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
function Resolve-RepoPath([string]$Path) {
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $repo $Path))
}
function Invoke-Git([string[]]$Arguments) {
    $output = @(& git @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)" }
    return $output
}

$manifestPath = Resolve-RepoPath $Manifest
$definition = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$task = @($definition.tasks | Where-Object id -eq $TaskId)
if ($task.Count -ne 1) { throw "Benchmark task '$TaskId' was not found exactly once in $Manifest." }
if ([string]::IsNullOrWhiteSpace($Model)) { throw 'Model must not be blank.' }

$fixtureCommit = [string]$definition.fixtureCommit
if ($fixtureCommit -notmatch '^[0-9a-fA-F]{7,40}$') { throw 'The fixture commit must be a hexadecimal Git object ID.' }
Invoke-Git @('-C', $repo, 'cat-file', '-e', "${fixtureCommit}^{commit}") | Out-Null

$output = Resolve-RepoPath $OutputRoot
$trialRoot = Join-Path (Join-Path $output $TaskId) $Arm
if (Test-Path -LiteralPath $trialRoot) {
    throw "Trial already exists and will not be overwritten: $trialRoot"
}

$checkout = Join-Path $trialRoot 'checkout'
New-Item -ItemType Directory -Path $checkout -Force | Out-Null
$archive = Join-Path $output ('.fixture-' + [Guid]::NewGuid().ToString('N') + '.zip')
try {
    Invoke-Git @('-c', 'core.autocrlf=false', '-C', $repo, 'archive', '--format=zip', "--output=$archive", $fixtureCommit) | Out-Null
    Expand-Archive -LiteralPath $archive -DestinationPath $checkout
}
catch {
    if (Test-Path -LiteralPath $trialRoot) { Remove-Item -LiteralPath $trialRoot -Recurse -Force }
    throw
}
finally {
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
}

Invoke-Git @('-C', $checkout, 'init', '--quiet') | Out-Null
Invoke-Git @('-C', $checkout, 'config', 'user.name', 'ArifCE Benchmark') | Out-Null
Invoke-Git @('-C', $checkout, 'config', 'user.email', 'benchmark@arifce.local') | Out-Null
Invoke-Git @('-C', $checkout, 'config', 'core.autocrlf', 'false') | Out-Null
Invoke-Git @('-C', $checkout, 'config', 'core.safecrlf', 'false') | Out-Null
Invoke-Git @('-C', $checkout, 'add', '--force', '--all') | Out-Null
$oldAuthorDate = $env:GIT_AUTHOR_DATE
$oldCommitterDate = $env:GIT_COMMITTER_DATE
try {
    $env:GIT_AUTHOR_DATE = '2000-01-01T00:00:00Z'
    $env:GIT_COMMITTER_DATE = '2000-01-01T00:00:00Z'
    Invoke-Git @('-C', $checkout, 'commit', '--quiet', '-m', "Isolated fixture $fixtureCommit") | Out-Null
}
finally {
    if ($null -eq $oldAuthorDate) { Remove-Item Env:GIT_AUTHOR_DATE -ErrorAction SilentlyContinue } else { $env:GIT_AUTHOR_DATE = $oldAuthorDate }
    if ($null -eq $oldCommitterDate) { Remove-Item Env:GIT_COMMITTER_DATE -ErrorAction SilentlyContinue } else { $env:GIT_COMMITTER_DATE = $oldCommitterDate }
}

$sourceTree = (Invoke-Git @('-C', $repo, 'rev-parse', "${fixtureCommit}^{tree}") | Select-Object -First 1).Trim()
$checkoutTree = (Invoke-Git @('-C', $checkout, 'rev-parse', 'HEAD^{tree}') | Select-Object -First 1).Trim()
$historyCount = [int]((Invoke-Git @('-C', $checkout, 'rev-list', '--count', 'HEAD') | Select-Object -First 1).Trim())
$remotes = @((Invoke-Git @('-C', $checkout, 'remote')) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
if ($sourceTree -ne $checkoutTree) { throw 'The isolated checkout tree does not match the fixture tree.' }
if ($historyCount -ne 1 -or $remotes.Count -ne 0) { throw 'The isolated checkout exposed Git history or a remote.' }

$armGuidance = if ($Arm -eq 'arifce') {
    'Before changing code, follow .arifce/PROTOCOL.md and use only ArifCE context available inside this isolated repository. Do not inspect or fetch any external branch, commit, patch, or prior-arm output.'
} else {
    'Work from repository source and tests without running ArifCE context, search, handoff, status, or memory commands. Do not inspect or fetch any external branch, commit, patch, or other-arm output.'
}
$prompt = @"
# Engineering benchmark trial

Task: $($task[0].id)
Category: $($task[0].category)
Arm: $Arm

## Instruction

$($task[0].instruction)

## Verification target

$($task[0].verification)

## Arm boundary

$armGuidance

Complete the task in the `checkout` directory. Do not edit `session.json` or this prompt. Report failures and negative outcomes honestly.
"@
Set-Content -LiteralPath (Join-Path $trialRoot 'prompt.md') -Value $prompt -Encoding utf8

$session = [ordered]@{
    schemaVersion = 1
    runId = [Guid]::NewGuid().ToString('D')
    state = 'PREPARED'
    preparedAtUtc = [DateTime]::UtcNow.ToString('O')
    taskId = $task[0].id
    category = $task[0].category
    arm = $Arm
    fixtureCommit = $fixtureCommit
    fixtureTree = $sourceTree
    isolatedCommit = (Invoke-Git @('-C', $checkout, 'rev-parse', 'HEAD') | Select-Object -First 1).Trim()
    isolatedHistoryCount = $historyCount
    remoteCount = $remotes.Count
    model = $Model
    tokenBudget = $TokenBudget
    checkout = 'checkout'
    prompt = 'prompt.md'
}
$session | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $trialRoot 'session.json') -Encoding utf8
Write-Output "Prepared isolated $Arm trial for $TaskId at $trialRoot"
