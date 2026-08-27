param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$smokeRoot = Join-Path $temporaryBase ('arifce-package-smoke-' + [Guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $smokeRoot 'packages'
$toolDirectory = Join-Path $smokeRoot 'tools'
$repositoryDirectory = Join-Path $smokeRoot 'repository'

try {
    New-Item -ItemType Directory -Force -Path $packageDirectory, $toolDirectory, $repositoryDirectory | Out-Null
    dotnet pack (Join-Path $PSScriptRoot '..\src\ArifCE.Cli\ArifCE.Cli.csproj') -c $Configuration --no-restore -o $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

    dotnet tool install --tool-path $toolDirectory --add-source $packageDirectory --ignore-failed-sources ArifCE.Cli --version 0.1.0
    if ($LASTEXITCODE -ne 0) { throw 'Tool installation failed.' }

    git -C $repositoryDirectory init | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Temporary Git initialization failed.' }

    $executable = Join-Path $toolDirectory ($(if ($IsWindows) { 'arifce.exe' } else { 'arifce' }))
    Push-Location $repositoryDirectory
    try {
        & $executable init
        if ($LASTEXITCODE -ne 0) { throw 'Initial arifce init failed.' }
        & $executable init
        if ($LASTEXITCODE -ne 0) { throw 'Idempotent arifce init failed.' }

        $taskId = (& $executable task create 'Package fixture continuity task' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $taskId -notmatch '^TASK-\d{4}$') { throw "Task creation failed: $taskId" }
        $decisionId = (& $executable decision create 'Package fixture storage choice' --decision 'Use canonical JSON records' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $decisionId -notmatch '^ADR-\d{4}$') { throw "Decision creation failed: $decisionId" }
        $attemptId = (& $executable attempt record $taskId 'Discard transcript dump' --result 'rejected' --reason 'Handoff must remain semantic' --evidence $decisionId | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $attemptId -notmatch '^ATTEMPT-\d{4}$') { throw "Attempt recording failed: $attemptId" }
        $checkpointId = (& $executable checkpoint --summary 'Packaged CLI fixture checkpoint' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $checkpointId -notmatch '^CHECKPOINT-\d{4}$') { throw "Checkpoint creation failed: $checkpointId" }

        $contextOutput = (& $executable context 'package fixture continuity task' --budget 400 | Out-String)
        if ($LASTEXITCODE -ne 0 -or $contextOutput -notmatch 'tasks/task-\d{4}\.json' -or $contextOutput -match 'Estimated total:\s*0') { throw 'Budgeted context did not retrieve the fixture task.' }

        $claimId = (& $executable claim create 'The packaged CLI can execute a deterministic command' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $claimId -notmatch '^CLAIM-\d{4}$') { throw "Claim creation failed: $claimId" }
        $verificationOutput = (& $executable verify $claimId --command 'dotnet --version' | Out-String)
        if ($LASTEXITCODE -ne 0 -or $verificationOutput -notmatch "${claimId}: Supported \(EVIDENCE-\d{4}\)") { throw 'Deterministic claim verification failed.' }
        New-Item -ItemType Directory -Force -Path (Join-Path $repositoryDirectory 'src') | Out-Null
        Set-Content -NoNewline -LiteralPath (Join-Path $repositoryDirectory 'src\Boundary.cs') -Value 'namespace PackageFixture;'
        $architectureClaimId = (& $executable claim create 'The packaged CLI verifies an architecture boundary' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $architectureClaimId -notmatch '^CLAIM-\d{4}$') { throw "Architecture claim creation failed: $architectureClaimId" }
        $architectureOutput = (& $executable architecture check $architectureClaimId --forbid '__ARIFCE_PACKAGE_FIXTURE_FORBIDDEN_7C31__' --path src | Out-String)
        if ($LASTEXITCODE -ne 0 -or $architectureOutput -notmatch "${architectureClaimId}: \w+ \(EVIDENCE-\d{4}\)") { throw 'Packaged architecture boundary verification failed.' }
        $apiAssembly = (Get-ChildItem -LiteralPath $toolDirectory -Filter 'ArifCE.Core.dll' -Recurse | Select-Object -First 1).FullName
        if ([string]::IsNullOrWhiteSpace($apiAssembly)) { throw 'Packaged core assembly was not found.' }
        Copy-Item -Force -LiteralPath $apiAssembly -Destination (Join-Path $repositoryDirectory 'ArifCE.Core.dll')
        & $executable api baseline ArifCE.Core.dll --baseline api-baseline.json
        if ($LASTEXITCODE -ne 0) { throw 'Packaged API baseline creation failed.' }
        $apiClaimId = (& $executable claim create 'The packaged CLI API baseline remains compatible' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $apiClaimId -notmatch '^CLAIM-\d{4}$') { throw "API claim creation failed: $apiClaimId" }
        $apiOutput = (& $executable api compare ArifCE.Core.dll --baseline api-baseline.json --claim $apiClaimId | Out-String)
        if ($LASTEXITCODE -ne 0 -or $apiOutput -notmatch "${apiClaimId}: \w+ \(EVIDENCE-\d{4}\)") { throw 'Packaged API compatibility verification failed.' }
        $findingId = (& $executable finding create 'Package fixture review finding' --description 'Exercise canonical finding linkage' --severity 'LOW' --task $taskId --path 'src/**' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $findingId -notmatch '^FINDING-\d{4}$') { throw "Finding creation failed: $findingId" }
        $reviewId = (& $executable review record $claimId --reviewer 'package-smoke' --verdict 'INCONCLUSIVE' --summary 'Fixture review cannot establish semantic truth' --finding $findingId | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $reviewId -notmatch '^REVIEW-\d{4}$') { throw "Review recording failed: $reviewId" }
        & $executable finding resolve $findingId
        if ($LASTEXITCODE -ne 0) { throw 'Finding resolution failed.' }

        $handoffOutput = (& $executable handoff | Out-String)
        if ($LASTEXITCODE -ne 0 -or $handoffOutput -notmatch 'Package fixture continuity task' -or $handoffOutput -notmatch 'Latest Decision' -or $handoffOutput -notmatch 'Latest Failed Attempt' -or $handoffOutput -notmatch 'Latest Evidence' -or $handoffOutput -notmatch 'Latest Finding' -or $handoffOutput -notmatch 'Latest Review' -or $handoffOutput -notmatch 'Saved HANDOFF-\d{4}') { throw 'Semantic handoff did not contain the required continuity and trust state.' }

        $refactorId = (& $executable refactor start 'Package fixture refactor' 'Exercise packaged guarded completion' --invariant 'Preserve fixture behavior' --inventory 'fixture-item' --forbid '__ARIFCE_PACKAGE_FIXTURE_FORBIDDEN_7C31__' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $refactorId -notmatch '^REF-\d{4}$') { throw "Refactor creation failed: $refactorId" }
        & $executable refactor workstream $refactorId 'fixture' --owner 'package-smoke' --path 'src/**' --path 'tests/**'
        if ($LASTEXITCODE -ne 0) { throw 'Refactor workstream creation failed.' }
        & $executable refactor safepoint $refactorId 'before-fixture' --note 'Packaged CLI rollback point'
        if ($LASTEXITCODE -ne 0) { throw 'Refactor safe point creation failed.' }
        & $executable refactor resolve $refactorId 'fixture-item'
        if ($LASTEXITCODE -ne 0) { throw 'Refactor inventory resolution failed.' }
        & $executable refactor verify $refactorId
        if ($LASTEXITCODE -ne 0) { throw 'Refactor guard verification failed.' }
        & $executable refactor finish $refactorId
        if ($LASTEXITCODE -ne 0) { throw 'Guarded refactor completion failed.' }

        & $executable task complete $taskId
        if ($LASTEXITCODE -ne 0) { throw 'Task completion failed.' }
        & $executable status
        if ($LASTEXITCODE -ne 0) { throw 'arifce status failed.' }

        $taskRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/tasks/$($taskId.ToLowerInvariant()).json") | ConvertFrom-Json
        $decisionRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/decisions/$($decisionId.ToLowerInvariant()).json") | ConvertFrom-Json
        $attemptRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/attempts/$($attemptId.ToLowerInvariant()).json") | ConvertFrom-Json
        $claimRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/claims/$($claimId.ToLowerInvariant()).json") | ConvertFrom-Json
        $architectureClaimRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/claims/$($architectureClaimId.ToLowerInvariant()).json") | ConvertFrom-Json
        $apiClaimRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/claims/$($apiClaimId.ToLowerInvariant()).json") | ConvertFrom-Json
        $findingRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/findings/$($findingId.ToLowerInvariant()).json") | ConvertFrom-Json
        $reviewRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/reviews/$($reviewId.ToLowerInvariant()).json") | ConvertFrom-Json
        $refactorRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/refactors/$($refactorId.ToLowerInvariant()).json") | ConvertFrom-Json
        if ($taskRecord.status -ne 'COMPLETED') { throw 'Canonical task state is not completed.' }
        if ($decisionRecord.historicalRationale -ne 'Unknown.') { throw 'Decision did not preserve unknown historical rationale.' }
        if ($attemptRecord.taskId -ne $taskId -or $attemptRecord.result -ne 'rejected') { throw 'Canonical failed-attempt state is incomplete.' }
        if ($claimRecord.status -ne 'SUPPORTED' -or $claimRecord.evidence.Count -lt 1) { throw 'Canonical claim/evidence state is incomplete.' }
        if ($architectureClaimRecord.evidence.Count -ne 1) { throw 'Canonical architecture-boundary state is incomplete.' }
        if ($apiClaimRecord.evidence.Count -ne 1) { throw 'Canonical API evidence state is incomplete.' }
        if ($findingRecord.status -ne 'COMPLETED' -or $reviewRecord.claimId -ne $claimId -or $reviewRecord.verdict -ne 'INCONCLUSIVE') { throw 'Canonical finding/review state is incomplete.' }
        if ($refactorRecord.status -ne 'COMPLETED' -or $refactorRecord.inventory.Count -ne 0 -or $refactorRecord.workstreams.Count -ne 1 -or $refactorRecord.safePoints.Count -ne 1) { throw 'Canonical refactor state is incomplete.' }

        Remove-Item -Force -LiteralPath (Join-Path $repositoryDirectory '.arifce/index/arifce.db')
        & $executable rebuild
        if ($LASTEXITCODE -ne 0) { throw 'arifce rebuild failed.' }
        $freshContext = (& $executable context 'package fixture continuity task' --budget 400 | Out-String)
        if ($LASTEXITCODE -ne 0 -or $freshContext -notmatch 'tasks/task-\d{4}\.json') { throw 'Context retrieval failed after index rebuild.' }
        Add-Content -LiteralPath (Join-Path $repositoryDirectory '.arifce/journal/events.jsonl') -Value '{partial-final'
        $diagnosis = (& $executable doctor | Out-String)
        if ($LASTEXITCODE -ne 0 -or $diagnosis -notmatch 'CORRUPT Journal line') { throw 'Doctor did not diagnose the partial journal line.' }
        $repair = (& $executable doctor --repair | Out-String)
        if ($LASTEXITCODE -ne 0 -or $repair -notmatch 'Repaired journal' -or $repair -notmatch 'Doctor: healthy') { throw 'Doctor did not repair the journal safely.' }
        if ((Get-ChildItem -LiteralPath (Join-Path $repositoryDirectory '.arifce/backups/journal') -Filter '*.bak').Count -ne 1) { throw 'Journal repair backup was not created.' }
        & $executable doctor
        if ($LASTEXITCODE -ne 0) { throw 'arifce doctor failed.' }
    }
    finally {
        Pop-Location
    }

    Write-Output 'Package smoke test passed.'
}
finally {
    $resolvedSmokeRoot = [System.IO.Path]::GetFullPath($smokeRoot)
    if ($resolvedSmokeRoot.StartsWith((Join-Path $temporaryBase 'arifce-package-smoke-'), [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedSmokeRoot)) {
        Remove-Item -Recurse -Force -LiteralPath $resolvedSmokeRoot
    }
}
