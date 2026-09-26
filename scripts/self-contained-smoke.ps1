[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Executable
)

$ErrorActionPreference = 'Stop'
$binary = (Resolve-Path -LiteralPath $Executable).Path
$root = Join-Path ([IO.Path]::GetTempPath()) ("arifce-binary-smoke-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null

function Invoke-ArifCE {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = @(& $binary @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "arifce $($Arguments -join ' ') failed with exit code $LASTEXITCODE`n$($output -join [Environment]::NewLine)"
    }
    return ($output -join [Environment]::NewLine)
}

try {
    $help = Invoke-ArifCE @('help')
    if ($help -notmatch 'task\s+[^\r\n]*\bcheck\b') { throw 'Published executable help does not expose the task check action.' }

    git -C $root init --quiet
    if ($LASTEXITCODE -ne 0) { throw 'Unable to initialize the isolated smoke repository.' }
    Push-Location $root
    try {
        Invoke-ArifCE @('init') | Out-Null
        $taskOutput = Invoke-ArifCE @('task', 'create', 'Release continuity smoke', '--objective', 'Verify the published CLI continuity workflow', '--scope', 'README.md', '--invariant', 'The task context and handoff remain task-specific', '--done-when', 'TEST_RUN:Release smoke test passes')
        $taskId = [regex]::Match($taskOutput, 'TASK-\d{4}').Value
        if (-not $taskId) { throw "Published executable did not create a task: $taskOutput" }

        $check = Invoke-ArifCE @('task', 'check', $taskId)
        $checkRecord = $check | ConvertFrom-Json
        if ($checkRecord.taskId -ne $taskId -or $checkRecord.state -ne 'INCOMPLETE') { throw "task check did not return the task completion state for $taskId`: $check" }
        $context = Invoke-ArifCE @('context', '--task', $taskId, '--budget', '2000')
        if ($context -notmatch 'TASK_CONTRACT' -or $context -notmatch 'Verify the published CLI continuity workflow') { throw "context --task did not return task-specific context: $context" }
        $handoff = Invoke-ArifCE @('handoff', '--task', $taskId)
        if ($handoff -notmatch 'Verify the published CLI continuity workflow' -or $handoff -notmatch "Handoff: $taskId") { throw "handoff --task did not return a task-specific handoff: $handoff" }
    }
    finally {
        Pop-Location
    }

    Write-Output 'Self-contained release smoke passed: help, task check, task context, and task handoff.'
}
finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
