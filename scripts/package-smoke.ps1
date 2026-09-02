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
$projectPath = Join-Path $PSScriptRoot '..\src\ArifCE.Cli\ArifCE.Cli.csproj'
$projectXml = Get-Content -Raw -LiteralPath $projectPath
$packageVersion = [regex]::Match($projectXml, '<Version>(?<v>[^<]+)</Version>').Groups['v'].Value
if ([string]::IsNullOrWhiteSpace($packageVersion)) { throw 'CLI package version was not found.' }

try {
    New-Item -ItemType Directory -Force -Path $packageDirectory, $toolDirectory, $repositoryDirectory | Out-Null
    dotnet pack $projectPath -c $Configuration --no-restore -o $packageDirectory
    if ($LASTEXITCODE -ne 0) { throw 'dotnet pack failed.' }

    dotnet tool install --tool-path $toolDirectory --add-source $packageDirectory --ignore-failed-sources ArifCE.Cli --version $packageVersion
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

        $taskId = (& $executable task create 'Package fixture continuity task' --risk HIGH | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $taskId -notmatch '^TASK-\d{4}$') { throw "Task creation failed: $taskId" }
        $invalidTaskOutput = (& $executable task create 'Package fixture invalid option' --unknown HIGH 2>&1 | Out-String)
        if ($LASTEXITCODE -eq 0 -or $invalidTaskOutput -notmatch 'supports only --risk') { throw 'Unsupported task-create options were not rejected.' }
        $decisionId = (& $executable decision create 'Package fixture storage choice' --decision 'Use canonical JSON records' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $decisionId -notmatch '^ADR-\d{4}$') { throw "Decision creation failed: $decisionId" }
        $supersededDecisionId = (& $executable decision create 'Legacy package fixture storage choice' --decision 'Use transient memory' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $supersededDecisionId -notmatch '^ADR-\d{4}$') { throw "Supersession fixture creation failed: $supersededDecisionId" }
        & $executable decision supersede $supersededDecisionId --by $decisionId
        if ($LASTEXITCODE -ne 0) { throw 'Explicit decision supersession failed.' }
        $knowledgeAudit = (& $executable knowledge audit | Out-String)
        if ($LASTEXITCODE -ne 0 -or $knowledgeAudit -notmatch 'Blocking:\s+0' -or $knowledgeAudit -notmatch 'Warnings:\s+0') { throw 'Packaged canonical knowledge audit failed.' }
        $attemptId = (& $executable attempt record $taskId 'Discard transcript dump' --result 'rejected' --reason 'Handoff must remain semantic' --evidence $decisionId | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $attemptId -notmatch '^ATTEMPT-\d{4}$') { throw "Attempt recording failed: $attemptId" }
        $checkpointId = (& $executable checkpoint --summary 'Packaged CLI fixture checkpoint' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $checkpointId -notmatch '^CHECKPOINT-\d{4}$') { throw "Checkpoint creation failed: $checkpointId" }

        $contextOutput = (& $executable context 'package fixture continuity task' --budget 400 | Out-String)
        if ($LASTEXITCODE -ne 0 -or $contextOutput -notmatch 'tasks/task-\d{4}\.json' -or $contextOutput -match 'Estimated total:\s*0') { throw 'Budgeted context did not retrieve the fixture task.' }

        Set-Content -NoNewline -LiteralPath (Join-Path $repositoryDirectory 'verification-scope.txt') -Value 'stable verification input'
        $claimId = (& $executable claim create 'The packaged CLI can execute a deterministic command' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $claimId -notmatch '^CLAIM-\d{4}$') { throw "Claim creation failed: $claimId" }
        # `dotnet --version` is intentionally outside the named build/test allowlist.
        # The smoke fixture opts in explicitly so packaging tests the guarded unsafe-command path.
        $verificationOutput = (& $executable verify $claimId --command 'dotnet --version' --path verification-scope.txt --allow-unsafe-command | Out-String)
        if ($LASTEXITCODE -ne 0 -or $verificationOutput -notmatch "${claimId}: Supported \(EVIDENCE-\d{4}\)") { throw 'Deterministic claim verification failed.' }
        New-Item -ItemType Directory -Force -Path (Join-Path $repositoryDirectory 'src') | Out-Null
        Set-Content -NoNewline -LiteralPath (Join-Path $repositoryDirectory 'src\Boundary.cs') -Value 'namespace PackageFixture;'
        Set-Content -NoNewline -LiteralPath (Join-Path $repositoryDirectory 'src\GraphFixture.cs') -Value 'public sealed class GraphFixture { public void InitialGraphSymbol() { } }'
        $graphBuild = (& $executable codegraph build | Out-String)
        if ($LASTEXITCODE -ne 0 -or $graphBuild -notmatch 'Code graph built:') { throw 'Packaged code-graph build failed.' }
        Set-Content -NoNewline -LiteralPath (Join-Path $repositoryDirectory 'src\GraphFixture.cs') -Value 'public sealed class GraphFixture { public void FreshGraphSymbol() { } }'
        $graphQuery = (& $executable codegraph query FreshGraphSymbol | Out-String)
        if ($LASTEXITCODE -ne 0 -or $graphQuery -notmatch 'METHOD\s+FreshGraphSymbol') { throw 'Packaged code-graph query did not rebuild after a source edit.' }
        $graphDocument = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory '.arifce\index\code-graph.json') | ConvertFrom-Json
        if ($graphDocument.generatorVersion -ne 2) { throw 'Packaged code graph does not carry the current generator version.' }
        $contractOutput = (& $executable contract create FreshGraphSymbol --risk LOW | Out-String)
        $contractId = [regex]::Match($contractOutput, 'CONTRACT-\d{4}').Value
        $contractClaimId = [regex]::Match($contractOutput, 'Claim:\s*(CLAIM-\d{4})').Groups[1].Value
        if ($LASTEXITCODE -ne 0 -or $contractId -notmatch '^CONTRACT-\d{4}$' -or $contractClaimId -notmatch '^CLAIM-\d{4}$') { throw 'Packaged change-contract creation failed.' }
        $contractVerification = (& $executable verify $contractClaimId --command 'dotnet --version' --contract $contractId --allow-unsafe-command | Out-String)
        $contractEvidenceId = [regex]::Match($contractVerification, 'EVIDENCE-\d{4}').Value
        if ($LASTEXITCODE -ne 0 -or $contractEvidenceId -notmatch '^EVIDENCE-\d{4}$') { throw 'Packaged contract-linked verification failed.' }
        $contractEvidence = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/evidence/$($contractEvidenceId.ToLowerInvariant()).json") | ConvertFrom-Json
        if ($contractEvidence.scope.contractId -ne $contractId -or $contractEvidence.scope.dependencies.mode -notcontains 'CODE_GRAPH_CLOSURE' -or $contractEvidence.scope.dependencies.path -notcontains 'src/GraphFixture.cs') { throw 'Packaged contract evidence did not persist its trusted dependency closure.' }
        $architectureClaimId = (& $executable claim create 'The packaged CLI verifies an architecture boundary' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $architectureClaimId -notmatch '^CLAIM-\d{4}$') { throw "Architecture claim creation failed: $architectureClaimId" }
        $architectureOutput = (& $executable architecture check $architectureClaimId --forbid '__ARIFCE_PACKAGE_FIXTURE_FORBIDDEN_7C31__' --path src | Out-String)
        if ($LASTEXITCODE -ne 0 -or $architectureOutput -notmatch "${architectureClaimId}: \w+ \(EVIDENCE-\d{4}\)") { throw 'Packaged architecture boundary verification failed.' }
        $apiAssembly = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\src\ArifCE.Core\bin\Release\net10.0\ArifCE.Core.dll'))
        if (-not (Test-Path -LiteralPath $apiAssembly)) { throw 'Built core assembly fixture was not found.' }
        Copy-Item -Force -LiteralPath $apiAssembly -Destination (Join-Path $repositoryDirectory 'ArifCE.Cli.dll')
        & $executable api baseline ArifCE.Cli.dll --baseline api-baseline.json
        if ($LASTEXITCODE -ne 0) { throw 'Packaged API baseline creation failed.' }
        $apiClaimId = (& $executable claim create 'The packaged CLI API baseline remains compatible' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $apiClaimId -notmatch '^CLAIM-\d{4}$') { throw "API claim creation failed: $apiClaimId" }
        $apiOutput = (& $executable api compare ArifCE.Cli.dll --baseline api-baseline.json --claim $apiClaimId | Out-String)
        if ($LASTEXITCODE -ne 0 -or $apiOutput -notmatch "${apiClaimId}: \w+ \(EVIDENCE-\d{4}\)") { throw 'Packaged API compatibility verification failed.' }
        & $executable schema baseline .arifce/index/arifce.db --baseline schema-baseline.json
        if ($LASTEXITCODE -ne 0) { throw 'Packaged SQLite schema baseline creation failed.' }
        $schemaClaimId = (& $executable claim create 'The packaged SQLite schema remains compatible' | Select-Object -Last 1).Trim()
        if ($LASTEXITCODE -ne 0 -or $schemaClaimId -notmatch '^CLAIM-\d{4}$') { throw "Schema claim creation failed: $schemaClaimId" }
        $schemaOutput = (& $executable schema compare .arifce/index/arifce.db --baseline schema-baseline.json --claim $schemaClaimId | Out-String)
        if ($LASTEXITCODE -ne 0 -or $schemaOutput -notmatch "${schemaClaimId}: \w+ \(EVIDENCE-\d{4}\)") { throw 'Packaged SQLite schema verification failed.' }
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
        $supersededDecisionRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/decisions/$($supersededDecisionId.ToLowerInvariant()).json") | ConvertFrom-Json
        $attemptRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/attempts/$($attemptId.ToLowerInvariant()).json") | ConvertFrom-Json
        $claimRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/claims/$($claimId.ToLowerInvariant()).json") | ConvertFrom-Json
        $architectureClaimRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/claims/$($architectureClaimId.ToLowerInvariant()).json") | ConvertFrom-Json
        $apiClaimRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/claims/$($apiClaimId.ToLowerInvariant()).json") | ConvertFrom-Json
        $findingRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/findings/$($findingId.ToLowerInvariant()).json") | ConvertFrom-Json
        $reviewRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/reviews/$($reviewId.ToLowerInvariant()).json") | ConvertFrom-Json
        $refactorRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/refactors/$($refactorId.ToLowerInvariant()).json") | ConvertFrom-Json
        if ($taskRecord.status -ne 'COMPLETED' -or $taskRecord.title -ne 'Package fixture continuity task' -or $taskRecord.risk -ne 'HIGH') { throw 'Canonical task state or task risk parsing is incorrect.' }
        if ($decisionRecord.historicalRationale -ne 'Unknown.') { throw 'Decision did not preserve unknown historical rationale.' }
        if ($supersededDecisionRecord.status -ne 'SUPERSEDED' -or $supersededDecisionRecord.supersededBy -ne $decisionId) { throw 'Decision supersession history is incomplete.' }
        if ($attemptRecord.taskId -ne $taskId -or $attemptRecord.result -ne 'rejected') { throw 'Canonical failed-attempt state is incomplete.' }
        if ($claimRecord.status -ne 'SUPPORTED' -or $claimRecord.evidence.Count -lt 1) { throw 'Unrelated repository changes incorrectly invalidated scoped evidence.' }
        if ($architectureClaimRecord.evidence.Count -ne 1) { throw 'Canonical architecture-boundary state is incomplete.' }
        if ($apiClaimRecord.evidence.Count -ne 1) { throw 'Canonical API evidence state is incomplete.' }
        $schemaClaimRecord = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/claims/$($schemaClaimId.ToLowerInvariant()).json") | ConvertFrom-Json
        if ($schemaClaimRecord.evidence.Count -ne 1 -or $schemaClaimRecord.status -notin @('SUPPORTED', 'VERIFIED')) { throw 'Canonical SQLite schema evidence was invalidated without a schema change.' }
        if ($findingRecord.status -ne 'COMPLETED' -or $reviewRecord.claimId -ne $claimId -or $reviewRecord.verdict -ne 'INCONCLUSIVE') { throw 'Canonical finding/review state is incomplete.' }
        if ($refactorRecord.status -ne 'COMPLETED' -or $refactorRecord.inventory.Count -ne 0 -or $refactorRecord.workstreams.Count -ne 1 -or $refactorRecord.safePoints.Count -ne 1) { throw 'Canonical refactor state is incomplete.' }

        Set-Content -NoNewline -LiteralPath (Join-Path $repositoryDirectory 'verification-scope.txt') -Value 'changed verification input'
        & $executable trust refresh
        if ($LASTEXITCODE -ne 0) { throw 'Scoped trust refresh failed.' }
        $staleScopedClaim = Get-Content -Raw -LiteralPath (Join-Path $repositoryDirectory ".arifce/claims/$($claimId.ToLowerInvariant()).json") | ConvertFrom-Json
        if ($staleScopedClaim.status -ne 'STALE') { throw 'Relevant scoped change did not invalidate evidence.' }

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
