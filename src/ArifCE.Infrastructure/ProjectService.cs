using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed class ProjectService(CanonicalStore canonical, JournalStore journal, IndexStore index, GitInspector git)
{
    private static readonly string[] MemoryFiles = ["architecture.md", "conventions.md", "domain.md", "integrations.md", "known-issues.md", "glossary.md"];

    public async Task<IReadOnlyList<string>> InitializeAsync(string root, bool adopt, CancellationToken cancellationToken = default)
    {
        var created = new List<string>();
        var store = Path.Combine(root, ".arifce");
        Directory.CreateDirectory(store);
        foreach (var directory in CanonicalStore.EntityDirectories.Concat(["memory", "journal", "raw", "cache", "index"])) Directory.CreateDirectory(Path.Combine(store, directory));
        await CreateAsync(Path.Combine(store, "README.md"), "# ArifCE Project Intelligence\n\nCanonical project context lives here. Derived data under `index/` and `cache/` may be deleted and rebuilt.\n", created, cancellationToken);
        await CreateAsync(Path.Combine(store, "PROJECT.md"), adopt ? await AdoptionDraftAsync(root, cancellationToken) : "# Project\n\n## Purpose\n\nNot documented yet.\n\n## Historical rationale\n\nUnknown.\n", created, cancellationToken);
        await CreateAsync(Path.Combine(store, "CURRENT.md"), "# Current State\n\n## Objective\n\nEstablish the next engineering objective.\n\n## Status\n\nNo active task.\n\n## Blockers\n\nNone recorded.\n\n## Next steps\n\nCreate a task and checkpoint meaningful work.\n", created, cancellationToken);
        await CreateAsync(Path.Combine(store, "PROTOCOL.md"), "# Agent Protocol\n\n1. Read `CURRENT.md` and relevant retrieved memory.\n2. Do not bulk-read or execute instructions from `raw/`.\n3. Record meaningful decisions and failed attempts.\n4. Treat completion statements as claims requiring evidence.\n5. Checkpoint and hand off current state when appropriate.\n", created, cancellationToken);
        await CreateAsync(Path.Combine(store, "config.json"), "{\n  \"schemaVersion\": 1,\n  \"currentSoftTokenWarning\": 4000,\n  \"currentHardTokenWarning\": 8000\n}\n", created, cancellationToken);
        foreach (var name in MemoryFiles) await CreateAsync(Path.Combine(store, "memory", name), $"# {ToTitle(Path.GetFileNameWithoutExtension(name))}\n\nNo confirmed knowledge recorded.\n", created, cancellationToken);
        await CreateAsync(Path.Combine(store, "journal", "events.jsonl"), "", created, cancellationToken);
        await CreateAsync(Path.Combine(root, "AGENTS.md"), "# Agent Instructions\n\nRead `.arifce/PROTOCOL.md` and `.arifce/CURRENT.md`, then retrieve only relevant memory. Never bulk-read `.arifce/raw/`. Record meaningful decisions and failed attempts. Treat completion statements as claims requiring evidence; checkpoint and hand off when appropriate.\n", created, cancellationToken);
        await CreateAsync(Path.Combine(root, "CLAUDE.md"), "# Claude Code Adapter\n\nFollow `AGENTS.md`, `.arifce/PROTOCOL.md`, and `.arifce/CURRENT.md`. Never bulk-read `.arifce/raw/`; retrieve task-specific context.\n", created, cancellationToken);
        await CreateAsync(Path.Combine(root, "opencode.json"), "{\n  \"instructions\": [\"AGENTS.md\", \".arifce/PROTOCOL.md\", \".arifce/CURRENT.md\"]\n}\n", created, cancellationToken);
        await CreateAsync(Path.Combine(root, ".gitignore"), ".arifce/index/\n.arifce/cache/\n.arifce/raw/\n", created, cancellationToken);
        if (created.Count > 0) await journal.AppendAsync(root, new JournalEvent(1, Guid.NewGuid().ToString("N"), adopt ? "project.adopted" : "project.initialized", DateTimeOffset.UtcNow, "PROJECT", new { created }), cancellationToken);
        await index.RebuildAsync(root, cancellationToken);
        return created;
    }

    public async Task<string> StatusAsync(string root, CancellationToken cancellationToken = default)
    {
        var snapshot = await git.CaptureAsync(root, cancellationToken);
        var tasks = Count(root, "tasks"); var claims = Count(root, "claims"); var checkpoints = Count(root, "checkpoints");
        return $"ArifCE status\nRoot: {root}\nBranch: {snapshot.Branch ?? "unknown"}\nCommit: {snapshot.Commit ?? "none"}\nWorktree: {(snapshot.IsDirty ? "dirty" : "clean")}\nTasks: {tasks}\nClaims: {claims}\nCheckpoints: {checkpoints}\nIndex: {(File.Exists(Path.Combine(root, ".arifce", "index", "arifce.db")) ? "present" : "missing")}";
    }

    public async Task<TaskRecord> CreateTaskAsync(string root, string title, RiskLevel risk, CancellationToken cancellationToken = default)
    {
        var id = canonical.NextId(root, "tasks", "TASK");
        var item = new TaskRecord(1, id, title, null, WorkStatus.Open, risk, DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "tasks", id, item, cancellationToken);
        await RecordAsync(root, "task.created", id, item, cancellationToken); return item;
    }

    public Task<TaskRecord?> GetTaskAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<TaskRecord>(root, "tasks", id, cancellationToken);

    public async Task<TaskRecord> CompleteTaskAsync(string root, string id, CancellationToken cancellationToken = default)
    {
        var item = await GetTaskAsync(root, id, cancellationToken) ?? throw new InvalidOperationException($"Task {id} was not found.");
        if (item.Status is WorkStatus.Abandoned or WorkStatus.Completed) throw new InvalidOperationException($"Task {id} is already {item.Status}.");
        var updated = await canonical.UpdateAsync<TaskRecord>(root, "tasks", id, current =>
        {
            if (current.Status is WorkStatus.Abandoned or WorkStatus.Completed) throw new InvalidOperationException($"Task {id} is already {current.Status}.");
            return current with { Status = WorkStatus.Completed };
        }, cancellationToken);
        await RecordAsync(root, "task.completed", id, updated, cancellationToken); return updated;
    }

    public async Task<DecisionRecord> CreateDecisionAsync(string root, string title, string decision, string? historicalRationale, CancellationToken cancellationToken = default)
    {
        await using var semanticLock = await FileMutationLock.AcquireAsync(root, "decisions", "semantic-create", cancellationToken);
        var decisionDirectory = Path.Combine(root, ".arifce", "decisions");
        if (Directory.Exists(decisionDirectory)) foreach (var path in Directory.EnumerateFiles(decisionDirectory, "*.json").Order(StringComparer.Ordinal))
        {
            DecisionRecord? existing;
            try { existing = JsonSerializer.Deserialize<DecisionRecord>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options); }
            catch (JsonException exception) { throw new InvalidOperationException($"Cannot create a decision while canonical record {Path.GetFileName(path)} is malformed.", exception); }
            if (existing is null) throw new InvalidOperationException($"Cannot create a decision while canonical record {Path.GetFileName(path)} is empty or invalid.");
            if (!string.Equals(existing.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(existing.SupersededBy)) continue;
            if (KnowledgeConflictAnalyzer.NormalizeText(existing.Title) != KnowledgeConflictAnalyzer.NormalizeText(title)) continue;
            var relation = KnowledgeConflictAnalyzer.NormalizeText(existing.Decision) == KnowledgeConflictAnalyzer.NormalizeText(decision) ? "duplicates" : "conflicts with";
            throw new InvalidOperationException($"New decision {relation} active decision {existing.Id}. Review it and use explicit supersession instead of creating ambiguous canonical state.");
        }
        var id = canonical.NextId(root, "decisions", "ADR");
        var item = new DecisionRecord(1, id, title, decision, string.IsNullOrWhiteSpace(historicalRationale) ? "Unknown." : historicalRationale, "ACTIVE", "USER_CONFIRMED", null, DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "decisions", id, item, cancellationToken); await RecordAsync(root, "decision.created", id, item, cancellationToken); return item;
    }

    public Task<DecisionRecord?> GetDecisionAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<DecisionRecord>(root, "decisions", id, cancellationToken);

    public async Task<DecisionRecord> SupersedeDecisionAsync(string root, string id, string replacementId, CancellationToken cancellationToken = default)
    {
        if (id.Equals(replacementId, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("A decision cannot supersede itself.");
        await using var semanticLock = await FileMutationLock.AcquireAsync(root, "decisions", "semantic-create", cancellationToken);
        var current = await GetDecisionAsync(root, id, cancellationToken) ?? throw new InvalidOperationException($"Decision {id} was not found.");
        var replacement = await GetDecisionAsync(root, replacementId, cancellationToken) ?? throw new InvalidOperationException($"Replacement decision {replacementId} was not found.");
        if (!string.Equals(current.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(current.SupersededBy)) throw new InvalidOperationException($"Decision {id} is not active.");
        if (!string.Equals(replacement.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(replacement.SupersededBy)) throw new InvalidOperationException($"Replacement decision {replacementId} is not active.");
        var updated = await canonical.UpdateAsync<DecisionRecord>(root, "decisions", id, value =>
        {
            if (!string.Equals(value.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(value.SupersededBy)) throw new InvalidOperationException($"Decision {id} is not active.");
            return value with { Status = "SUPERSEDED", SupersededBy = replacement.Id };
        }, cancellationToken);
        await RecordAsync(root, "decision.superseded", id, new { replacementId = replacement.Id }, cancellationToken);
        return updated;
    }

    public async Task<AttemptRecord> RecordAttemptAsync(string root, string taskId, string approach, string result, string reason, IReadOnlyList<string>? evidenceIds = null, CancellationToken cancellationToken = default)
    {
        if (await GetTaskAsync(root, taskId, cancellationToken) is null) throw new InvalidOperationException($"Task {taskId} was not found.");
        var id = canonical.NextId(root, "attempts", "ATTEMPT");
        var item = new AttemptRecord(1, id, taskId, approach, result, reason, evidenceIds ?? [], DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "attempts", id, item, cancellationToken); await RecordAsync(root, "attempt.recorded", id, item, cancellationToken); return item;
    }

    public Task<AttemptRecord?> GetAttemptAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<AttemptRecord>(root, "attempts", id, cancellationToken);

    public async Task<FindingRecord> CreateFindingAsync(string root, string title, string description, RiskLevel severity, string? taskId, string? path, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(taskId) && await GetTaskAsync(root, taskId, cancellationToken) is null) throw new InvalidOperationException($"Task {taskId} was not found.");
        var id = canonical.NextId(root, "findings", "FINDING"); var item = new FindingRecord(1, id, title, description, severity, WorkStatus.Open, taskId, path, DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "findings", id, item, cancellationToken); await RecordAsync(root, "finding.created", id, item, cancellationToken); return item;
    }

    public Task<FindingRecord?> GetFindingAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<FindingRecord>(root, "findings", id, cancellationToken);

    public async Task<FindingRecord> ResolveFindingAsync(string root, string id, CancellationToken cancellationToken = default)
    {
        var item = await GetFindingAsync(root, id, cancellationToken) ?? throw new InvalidOperationException($"Finding {id} was not found.");
        if (item.Status == WorkStatus.Completed) throw new InvalidOperationException($"Finding {id} is already resolved.");
        var updated = await canonical.UpdateAsync<FindingRecord>(root, "findings", id, current => current with { Status = WorkStatus.Completed }, cancellationToken); await RecordAsync(root, "finding.resolved", id, updated, cancellationToken); return updated;
    }

    public async Task<ReviewRecord> RecordReviewAsync(string root, string claimId, string reviewer, ReviewVerdict verdict, string summary, IReadOnlyList<string> findingIds, CancellationToken cancellationToken = default)
    {
        var claim = await GetClaimAsync(root, claimId, cancellationToken) ?? throw new InvalidOperationException($"Claim {claimId} was not found.");
        foreach (var findingId in findingIds) if (await GetFindingAsync(root, findingId, cancellationToken) is null) throw new InvalidOperationException($"Finding {findingId} was not found.");
        var id = canonical.NextId(root, "reviews", "REVIEW"); var item = new ReviewRecord(1, id, claimId, reviewer, verdict, summary, findingIds, await git.CaptureAsync(root, cancellationToken), DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "reviews", id, item, cancellationToken);
        if (verdict == ReviewVerdict.Disagree && ClaimTransitions.IsAllowed(claim.Status, ClaimStatus.Disputed)) await canonical.UpdateAsync<ClaimRecord>(root, "claims", claim.Id, current => ClaimTransitions.IsAllowed(current.Status, ClaimStatus.Disputed) ? current with { Status = ClaimStatus.Disputed } : current, cancellationToken);
        await RecordAsync(root, "review.created", id, item, cancellationToken); return item;
    }

    public Task<ReviewRecord?> GetReviewAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<ReviewRecord>(root, "reviews", id, cancellationToken);

    public async Task<CheckpointRecord> CheckpointAsync(string root, string summary, CancellationToken cancellationToken = default)
    {
        var id = canonical.NextId(root, "checkpoints", "CHECKPOINT");
        var item = new CheckpointRecord(1, id, summary, await git.CaptureAsync(root, cancellationToken), DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "checkpoints", id, item, cancellationToken);
        await RecordAsync(root, "checkpoint.created", id, item, cancellationToken); return item;
    }

    public async Task<ClaimRecord> CreateClaimAsync(string root, string statement, RiskLevel risk, CancellationToken cancellationToken = default)
    {
        var id = canonical.NextId(root, "claims", "CLAIM");
        var item = new ClaimRecord(1, id, statement, ClaimStatus.Unverified, risk, await git.CaptureAsync(root, cancellationToken), [], DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "claims", id, item, cancellationToken);
        await RecordAsync(root, "claim.created", id, item, cancellationToken); return item;
    }

    public Task<ClaimRecord?> GetClaimAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<ClaimRecord>(root, "claims", id, cancellationToken);

    public async Task<AcceptanceRecord> CreateAcceptanceAsync(string root, string claimId, string actor, string rationale, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("Acceptance actor is required.");
        if (string.IsNullOrWhiteSpace(rationale)) throw new ArgumentException("Acceptance rationale is required.");
        var claim = await GetClaimAsync(root, claimId, cancellationToken) ?? throw new InvalidOperationException($"Claim {claimId} was not found.");
        if (claim.Status is ClaimStatus.Contradicted or ClaimStatus.Stale or ClaimStatus.Unverified) throw new InvalidOperationException($"Claim {claimId} must have current supporting evidence before acceptance.");
        var current = await git.CaptureAsync(root, cancellationToken);
        var evidence = new List<string>();
        foreach (var evidenceId in claim.Evidence)
        {
            var item = await canonical.ReadAsync<EvidenceRecord>(root, "evidence", evidenceId, cancellationToken);
            if (item is not null && await EvidenceScopeTracker.EvaluateAsync(root, item, current, cancellationToken) == EvidenceFreshness.Current) evidence.Add(evidenceId);
        }
        if (evidence.Count == 0) throw new InvalidOperationException($"Claim {claimId} has no current supporting evidence.");
        await ValidateAcceptancePolicyAsync(root, claim, evidence, cancellationToken);
        var findings = Path.Combine(root, ".arifce", "findings");
        if (Directory.Exists(findings)) foreach (var path in Directory.EnumerateFiles(findings, "*.json"))
        {
            var finding = JsonSerializer.Deserialize<FindingRecord>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options);
            if (finding is { Status: WorkStatus.Open, Severity: RiskLevel.High or RiskLevel.Critical }) throw new InvalidOperationException("Open high or critical findings block acceptance.");
        }
        var id = canonical.NextId(root, "acceptances", "ACCEPTANCE");
        var record = new AcceptanceRecord(1, id, claimId, actor, AcceptanceStatus.Accepted, rationale, current, evidence, DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "acceptances", id, record, cancellationToken); await RecordAsync(root, "acceptance.accepted", id, record, cancellationToken); return record;
    }

    private static async Task ValidateAcceptancePolicyAsync(string root, ClaimRecord claim, IReadOnlyList<string> evidenceIds, CancellationToken cancellationToken)
    {
        var requirements = VerificationPolicy.For(claim.Risk);
        if (!requirements.Build && !requirements.Tests && !requirements.IndependentReview) return;

        var evidenceDirectory = Path.Combine(root, ".arifce", "evidence");
        var evidence = new List<EvidenceRecord>();
        foreach (var id in evidenceIds)
        {
            var path = Path.Combine(evidenceDirectory, id.ToLowerInvariant() + ".json");
            if (!File.Exists(path)) throw new InvalidOperationException($"Evidence {id} is missing.");
            var item = JsonSerializer.Deserialize<EvidenceRecord>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options)
                ?? throw new InvalidOperationException($"Evidence {id} is invalid.");
            evidence.Add(item);
        }

        if (requirements.Build && !evidence.Any(item => item.Kind.Equals("BUILD", StringComparison.OrdinalIgnoreCase) && item.ExitCode == 0 && (item.Metrics?.Errors is null or 0)))
            throw new InvalidOperationException("High-risk acceptance requires a successful BUILD evidence record.");
        if (requirements.Tests && !evidence.Any(item => item.Kind.Equals("TEST_RUN", StringComparison.OrdinalIgnoreCase) && item.ExitCode == 0 && (item.Metrics?.Failed is null or 0)))
            throw new InvalidOperationException("High-risk acceptance requires a successful TEST_RUN evidence record.");
        if (requirements.IndependentReview)
        {
            var reviewDirectory = Path.Combine(root, ".arifce", "reviews");
            var hasReview = Directory.Exists(reviewDirectory) && Directory.EnumerateFiles(reviewDirectory, "*.json")
                .Select(path => JsonSerializer.Deserialize<ReviewRecord>(File.ReadAllText(path), JsonDefaults.Options))
                .Any(review => review is not null && review.ClaimId.Equals(claim.Id, StringComparison.OrdinalIgnoreCase) && review.Verdict is ReviewVerdict.Agree or ReviewVerdict.PartiallyAgree);
            if (!hasReview) throw new InvalidOperationException("High-risk acceptance requires an agreeing independent review.");
        }
    }

    public Task<AcceptanceRecord?> GetAcceptanceAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<AcceptanceRecord>(root, "acceptances", id, cancellationToken);

    public async Task<AcceptanceRecord> RevokeAcceptanceAsync(string root, string id, CancellationToken cancellationToken = default)
    {
        var record = await GetAcceptanceAsync(root, id, cancellationToken) ?? throw new InvalidOperationException($"Acceptance {id} was not found.");
        var updated = await canonical.UpdateAsync<AcceptanceRecord>(root, "acceptances", id, current => current with { Status = AcceptanceStatus.Revoked, RevokedAtUtc = DateTimeOffset.UtcNow }, cancellationToken);
        await RecordAsync(root, "acceptance.revoked", id, updated, cancellationToken); return updated;
    }

    public async Task<(ClaimRecord Claim, EvidenceRecord Evidence)> VerifyAsync(string root, string claimId, string commandText, bool allowUnsafeCommand = false, IReadOnlyList<string>? scopePaths = null, CancellationToken cancellationToken = default, string? contractId = null)
    {
        var claim = await canonical.ReadAsync<ClaimRecord>(root, "claims", claimId, cancellationToken) ?? throw new InvalidOperationException($"Claim {claimId} was not found.");
        var commandRedaction = new SecretRedactor().Redact(commandText);
        if (commandRedaction.Count > 0) throw new InvalidOperationException("Verification command contains a detectable secret and was blocked before execution.");
        var policy = VerificationCommandPolicy.Classify(commandText);
        if (policy == VerificationCommandKind.UnsafeShell && !allowUnsafeCommand) throw new InvalidOperationException("Unrecognized verification commands require explicit --allow-unsafe-command approval.");
        var before = await git.CaptureAsync(root, cancellationToken);
        EvidenceScope? scope;
        if (string.IsNullOrWhiteSpace(contractId)) scope = await EvidenceScopeTracker.CaptureAsync(root, scopePaths, cancellationToken);
        else
        {
            var contract = await GetChangeContractAsync(root, contractId, cancellationToken) ?? throw new InvalidOperationException($"Change contract {contractId} was not found.");
            if (!contract.ClaimId.Equals(claim.Id, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Change contract {contract.Id} is linked to claim {contract.ClaimId}, not {claim.Id}.");
            scope = await EvidenceScopeTracker.CaptureForContractAsync(root, contract, scopePaths, cancellationToken);
        }
        var result = policy == VerificationCommandKind.NamedDotNet
            ? await RunNamedCommandAsync(root, commandText, cancellationToken)
            : await RunShellCommandAsync(root, commandText, cancellationToken);
        var evidenceId = canonical.NextId(root, "evidence", "EVIDENCE");
        var parsed = CommandEvidenceParser.Parse(commandText, result.Output);
        var evidenceKind = policy == VerificationCommandKind.UnsafeShell ? "UNSAFE_COMMAND" : parsed.Kind == "COMMAND" ? "UNVERIFIED_COMMAND" : parsed.Kind;
        var safeOutput = new SecretRedactor().Redact(result.Output).Text;
        var evidence = new EvidenceRecord(1, evidenceId, claim.Id, evidenceKind, commandText, result.ExitCode, Truncate(safeOutput, 1000), before, DateTimeOffset.UtcNow, parsed.Metrics, scope);
        await canonical.WriteAsync(root, "evidence", evidenceId, evidence, cancellationToken);
        var status = result.ExitCode == 0 ? (policy == VerificationCommandKind.UnsafeShell || parsed.Kind == "COMMAND" ? ClaimStatus.Supported : claim.Risk == RiskLevel.Low ? ClaimStatus.Verified : ClaimStatus.Supported) : ClaimStatus.Contradicted;
        var updated = await canonical.UpdateAsync<ClaimRecord>(root, "claims", claim.Id, current => current with { Status = status, Evidence = current.Evidence.Append(evidenceId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() }, cancellationToken);
        await RecordAsync(root, "evidence.recorded", evidenceId, evidence, cancellationToken); return (updated, evidence);
    }

    public async Task<(ClaimRecord Claim, EvidenceRecord Evidence)> VerifyArchitectureBoundaryAsync(string root, string claimId, IReadOnlyList<string> forbiddenReferences, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        if (forbiddenReferences.Count == 0) throw new ArgumentException("At least one --forbid value is required.");
        if (paths.Count == 0) throw new ArgumentException("At least one --path value is required.");
        var claim = await GetClaimAsync(root, claimId, cancellationToken) ?? throw new InvalidOperationException($"Claim {claimId} was not found.");
        var before = await git.CaptureAsync(root, cancellationToken);
        var scope = await EvidenceScopeTracker.CaptureAsync(root, paths, cancellationToken);
        var scan = await ArchitectureBoundaryScanner.ScanAsync(root, forbiddenReferences, paths, cancellationToken);
        var evidenceId = canonical.NextId(root, "evidence", "EVIDENCE");
        var command = $"arifce architecture check {claimId} {string.Join(' ', forbiddenReferences.Select(value => $"--forbid {value}"))} {string.Join(' ', paths.Select(path => $"--path {path}"))}";
        var summary = scan.Violations.Count == 0
            ? $"Architecture boundary check passed. {scan.FilesScanned} source file(s) scanned; forbidden references: {string.Join(", ", forbiddenReferences.Order(StringComparer.Ordinal))}."
            : $"Architecture boundary check failed with {scan.Violations.Count} violation(s) in {scan.FilesScanned} source file(s).\n{string.Join('\n', scan.Violations.Take(20))}";
        var evidence = new EvidenceRecord(1, evidenceId, claim.Id, "ARCHITECTURE_BOUNDARY", command, scan.Violations.Count == 0 ? 0 : 1, summary, before, DateTimeOffset.UtcNow, new EvidenceMetrics(scan.FilesScanned, scan.FilesScanned - scan.ViolatingFiles, scan.Violations.Count, null), scope);
        await canonical.WriteAsync(root, "evidence", evidenceId, evidence, cancellationToken);
        var status = scan.Violations.Count == 0 ? (claim.Risk == RiskLevel.Low ? ClaimStatus.Verified : ClaimStatus.Supported) : ClaimStatus.Contradicted;
        var updated = await canonical.UpdateAsync<ClaimRecord>(root, "claims", claim.Id, current => current with { Status = status, Evidence = current.Evidence.Append(evidenceId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() }, cancellationToken);
        await RecordAsync(root, "evidence.recorded", evidenceId, evidence, cancellationToken);
        return (updated, evidence);
    }

    public async Task<(ClaimRecord Claim, EvidenceRecord Evidence)> VerifyApiSurfaceAsync(string root, string claimId, string assemblyPath, string baselinePath, CancellationToken cancellationToken = default)
    {
        var claim = await GetClaimAsync(root, claimId, cancellationToken) ?? throw new InvalidOperationException($"Claim {claimId} was not found.");
        var assembly = ResolveRepositoryPath(root, assemblyPath); var baseline = ResolveRepositoryPath(root, baselinePath);
        var current = ApiSurfaceAnalyzer.Read(assembly); var previous = await ApiSurfaceAnalyzer.ReadBaselineAsync(baseline, cancellationToken); var diff = ApiSurfaceAnalyzer.Compare(previous, current);
        var evidenceId = canonical.NextId(root, "evidence", "EVIDENCE"); var summary = $"API compatibility {(diff.IsCompatible ? "passed" : "failed")}. Added: {diff.Added.Count}; removed: {diff.Removed.Count}; changed: {diff.Changed.Count}.";
        if (diff.Removed.Count > 0) summary += $"\nRemoved:\n{string.Join('\n', diff.Removed.Take(20))}";
        var snapshot = await git.CaptureAsync(root, cancellationToken);
        var scope = await EvidenceScopeTracker.CaptureApiSurfaceAsync(root, assemblyPath, baselinePath, cancellationToken);
        var evidence = new EvidenceRecord(1, evidenceId, claim.Id, "PUBLIC_API_SURFACE", $"arifce api compare {assemblyPath} --baseline {baselinePath}", diff.IsCompatible ? 0 : 1, summary, snapshot, DateTimeOffset.UtcNow, new EvidenceMetrics(current.Count, diff.Added.Count, diff.Removed.Count, diff.Changed.Count), scope);
        await canonical.WriteAsync(root, "evidence", evidenceId, evidence, cancellationToken);
        var status = diff.IsCompatible ? (claim.Risk == RiskLevel.Low ? ClaimStatus.Verified : ClaimStatus.Supported) : ClaimStatus.Contradicted;
        var updated = await canonical.UpdateAsync<ClaimRecord>(root, "claims", claim.Id, currentClaim => currentClaim with { Status = status, Evidence = currentClaim.Evidence.Append(evidenceId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() }, cancellationToken); await RecordAsync(root, "evidence.recorded", evidenceId, evidence, cancellationToken); return (updated, evidence);
    }

    public async Task<(ClaimRecord Claim, EvidenceRecord Evidence)> VerifySqliteSchemaAsync(string root, string claimId, string databasePath, string baselinePath, CancellationToken cancellationToken = default)
    {
        var claim = await GetClaimAsync(root, claimId, cancellationToken) ?? throw new InvalidOperationException($"Claim {claimId} was not found.");
        var database = ResolveRepositoryPath(root, databasePath); var baseline = ResolveRepositoryPath(root, baselinePath); var current = await SqliteSchemaAnalyzer.ReadAsync(database, cancellationToken); var diff = SqliteSchemaAnalyzer.Compare(await SqliteSchemaAnalyzer.ReadBaselineAsync(baseline, cancellationToken), current);
        var id = canonical.NextId(root, "evidence", "EVIDENCE"); var summary = $"SQLite schema compatibility {(diff.IsCompatible ? "passed" : "failed")}. Added: {diff.Added.Count}; removed: {diff.Removed.Count}; changed: {diff.Changed.Count}.";
        var snapshot = await git.CaptureAsync(root, cancellationToken);
        var scope = await EvidenceScopeTracker.CaptureSqliteSchemaAsync(root, databasePath, baselinePath, cancellationToken);
        var evidence = new EvidenceRecord(1, id, claim.Id, "SQLITE_SCHEMA", $"arifce schema compare {databasePath} --baseline {baselinePath}", diff.IsCompatible ? 0 : 1, summary, snapshot, DateTimeOffset.UtcNow, new EvidenceMetrics(current.Count, diff.Added.Count, diff.Removed.Count, diff.Changed.Count), scope);
        await canonical.WriteAsync(root, "evidence", id, evidence, cancellationToken); var status = diff.IsCompatible ? (claim.Risk == RiskLevel.Low ? ClaimStatus.Verified : ClaimStatus.Supported) : ClaimStatus.Contradicted; var updated = await canonical.UpdateAsync<ClaimRecord>(root, "claims", claim.Id, currentClaim => currentClaim with { Status = status, Evidence = currentClaim.Evidence.Append(id).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() }, cancellationToken); await RecordAsync(root, "evidence.recorded", id, evidence, cancellationToken); return (updated, evidence);
    }

    private static string ResolveRepositoryPath(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root); var resolved = Path.GetFullPath(Path.Combine(fullRoot, path)); var relative = Path.GetRelativePath(fullRoot, resolved);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new ArgumentException($"Path '{path}' is outside the repository root.");
        if (!File.Exists(resolved)) throw new ArgumentException($"Path '{path}' does not exist."); return resolved;
    }

    public async Task<AgentRunRecord> StartAgentRunAsync(string root, string provider, string agent, string goal, string? taskId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(agent) || string.IsNullOrWhiteSpace(goal)) throw new ArgumentException("Provider, agent, and goal are required.");
        if (!string.IsNullOrWhiteSpace(taskId) && await GetTaskAsync(root, taskId, cancellationToken) is null) throw new InvalidOperationException($"Task {taskId} was not found.");
        var safeGoal = new SecretRedactor().Redact(goal).Text;
        var id = canonical.NextId(root, "runs", "RUN");
        var run = new AgentRunRecord(1, id, provider.Trim().ToLowerInvariant(), agent.Trim(), Truncate(safeGoal, 1000), taskId, AgentRunStatus.Running, [], await git.CaptureAsync(root, cancellationToken), DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "runs", id, run, cancellationToken);
        await RecordAsync(root, "run.started", id, new { run.Provider, run.Agent, run.Goal, run.TaskId }, cancellationToken);
        return run;
    }

    public Task<AgentRunRecord?> GetAgentRunAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<AgentRunRecord>(root, "runs", id, cancellationToken);

    public async Task<AgentRunRecord> RecordAgentRunStepAsync(string root, string id, AgentStepKind kind, string summary, string? outcome = null, int? exitCode = null, IReadOnlyList<string>? relatedIds = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(summary)) throw new ArgumentException("A structured step summary is required.", nameof(summary));
        var safeSummary = Truncate(new SecretRedactor().Redact(summary).Text, 1000);
        var run = await GetAgentRunAsync(root, id, cancellationToken) ?? throw new InvalidOperationException($"Run {id} was not found.");
        if (run.Status != AgentRunStatus.Running) throw new InvalidOperationException($"Run {id} is already {run.Status}.");
        var links = (relatedIds ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (kind == AgentStepKind.Attempt && IsFailedOutcome(outcome, exitCode) && !string.IsNullOrWhiteSpace(run.TaskId))
        {
            var attempt = await RecordAttemptAsync(root, run.TaskId, safeSummary, outcome ?? "FAILED", $"Agent run {id} recorded a failed attempt.", links, cancellationToken);
            links.Add(attempt.Id);
        }
        var step = new AgentRunStep(Guid.NewGuid().ToString("N"), kind, safeSummary, outcome, exitCode, links.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), DateTimeOffset.UtcNow);
        var updated = await canonical.UpdateAsync<AgentRunRecord>(root, "runs", id, current =>
        {
            if (current.Status != AgentRunStatus.Running) throw new InvalidOperationException($"Run {id} is already {current.Status}.");
            return current with { Steps = current.Steps.Append(step).ToArray() };
        }, cancellationToken);
        await RecordAsync(root, "run.step-recorded", id, new { step.Id, step.Kind, step.Outcome, step.ExitCode, step.RelatedIds }, cancellationToken);
        return updated;
    }

    public async Task<AgentRunRecord> FinishAgentRunAsync(string root, string id, string summary, bool succeeded, CancellationToken cancellationToken = default)
    {
        await RecordAgentRunStepAsync(root, id, AgentStepKind.Result, summary, succeeded ? "PASSED" : "FAILED", succeeded ? 0 : 1, cancellationToken: cancellationToken);
        var updated = await canonical.UpdateAsync<AgentRunRecord>(root, "runs", id, current => current with { Status = succeeded ? AgentRunStatus.Completed : AgentRunStatus.Failed, CompletedAtUtc = DateTimeOffset.UtcNow }, cancellationToken);
        await RecordAsync(root, succeeded ? "run.completed" : "run.failed", id, new { updated.Status, updated.CompletedAtUtc }, cancellationToken);
        return updated;
    }

    private static bool IsFailedOutcome(string? outcome, int? exitCode) => exitCode is not null and not 0 || outcome?.Equals("FAILED", StringComparison.OrdinalIgnoreCase) == true || outcome?.Equals("REJECTED", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<ChangeContractRecord> CreateChangeContractAsync(string root, string target, RiskLevel risk, IReadOnlyList<string>? invariants = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target)) throw new ArgumentException("A change target is required.", nameof(target));
        var graphResult = await new CodeGraphStore().QueryAsync(root, target, cancellationToken, exactMatch: true);
        if (graphResult.Matches.Count == 0) throw new InvalidOperationException($"No code-graph symbol matches '{target}'. Build the graph and use an exact symbol name.");
        var candidates = graphResult.RelatedNodes.Select(node => new ChangeImpactItem(node.Kind, node.Name, node.Path, RelationshipConfidence(node.Id)))
            .Distinct().OrderBy(item => item.Path, StringComparer.Ordinal).ToArray();
        var impact = candidates.Where(item => item.Kind is not ("TEST" or "TEST_FILE")).ToArray();
        var tests = candidates.Where(item => item.Kind is "TEST" or "TEST_FILE").ToArray();

        string RelationshipConfidence(string nodeId)
        {
            var links = graphResult.Edges.Where(edge => edge.From == nodeId || edge.To == nodeId).ToArray();
            if (links.Any(edge => edge.Confidence == "EXACT")) return "EXACT";
            if (links.Any(edge => edge.Confidence == "STRUCTURAL")) return "STRUCTURAL";
            return "HEURISTIC";
        }
        var history = (await index.SearchAsync(root, target, 20, cancellationToken)).Where(hit => hit.Path.StartsWith("decisions/", StringComparison.OrdinalIgnoreCase) || hit.Path.StartsWith("attempts/", StringComparison.OrdinalIgnoreCase) || hit.Path.StartsWith("findings/", StringComparison.OrdinalIgnoreCase) || hit.Path.StartsWith("refactors/", StringComparison.OrdinalIgnoreCase) || hit.Path.StartsWith("claims/", StringComparison.OrdinalIgnoreCase)).Select(hit => hit.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var policy = VerificationPolicy.For(risk);
        var required = new List<string> { "Rebuild the deterministic code graph and review every impact candidate." };
        if (policy.Build) required.Add("Attach successful BUILD evidence to the linked claim.");
        if (policy.Tests) required.Add("Attach successful TEST_RUN evidence to the linked claim.");
        if (policy.IndependentReview) required.Add("Record an agreeing independent review for the linked claim.");
        if (policy.HumanApproval) required.Add("Record explicit human acceptance with rationale.");
        required.AddRange(tests.Select(test => $"Run related test candidate: {test.Path}."));
        var claim = await CreateClaimAsync(root, $"Change contract for {target} is satisfied", risk, cancellationToken);
        var id = canonical.NextId(root, "contracts", "CONTRACT");
        var contract = new ChangeContractRecord(1, id, target, risk, WorkStatus.Open, claim.Id, impact, tests, history, invariants ?? [], required.Distinct(StringComparer.Ordinal).ToArray(), await git.CaptureAsync(root, cancellationToken), DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "contracts", id, contract, cancellationToken);
        await RecordAsync(root, "contract.created", id, contract, cancellationToken);
        return contract;
    }

    public Task<ChangeContractRecord?> GetChangeContractAsync(string root, string id, CancellationToken cancellationToken = default) => canonical.ReadAsync<ChangeContractRecord>(root, "contracts", id, cancellationToken);

    public async Task<TrustRefreshResult> RefreshTrustAsync(string root, CancellationToken cancellationToken = default)
    {
        var current = await git.CaptureAsync(root, cancellationToken);
        var warnings = new List<string>();
        var staleClaims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var claimsStaled = 0;
        var claimDirectory = Path.Combine(root, ".arifce", "claims");
        if (Directory.Exists(claimDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(claimDirectory, "*.json").Order(StringComparer.Ordinal))
            {
                ClaimRecord? claim;
                try { claim = JsonSerializer.Deserialize<ClaimRecord>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options); }
                catch (JsonException) { warnings.Add($"Malformed claim record: {Path.GetFileName(path)}."); continue; }
                if (claim is null || claim.Status is not (ClaimStatus.Supported or ClaimStatus.PartiallyVerified or ClaimStatus.Verified or ClaimStatus.Stale)) continue;
                var hasCurrentEvidence = false;
                foreach (var evidenceId in claim.Evidence)
                {
                    var evidencePath = Path.Combine(root, ".arifce", "evidence", evidenceId.ToLowerInvariant() + ".json");
                    try
                    {
                        var evidence = File.Exists(evidencePath) ? JsonSerializer.Deserialize<EvidenceRecord>(await File.ReadAllTextAsync(evidencePath, cancellationToken), JsonDefaults.Options) : null;
                        if (evidence is not null && await EvidenceScopeTracker.EvaluateAsync(root, evidence, current, cancellationToken) == EvidenceFreshness.Current) hasCurrentEvidence = true;
                    }
                    catch (JsonException) { }
                }
                var isStale = claim.Status == ClaimStatus.Stale || !hasCurrentEvidence;
                if (!isStale) continue;
                staleClaims.Add(claim.Id);
                warnings.Add($"Claim {claim.Id} is stale and requires re-verification.");
                if (claim.Status == ClaimStatus.Stale) continue;
                await canonical.UpdateAsync<ClaimRecord>(root, "claims", claim.Id, value => value with { Status = ClaimStatus.Stale }, cancellationToken);
                claimsStaled++;
            }
        }

        var acceptancesFlagged = 0;
        var acceptanceDirectory = Path.Combine(root, ".arifce", "acceptances");
        if (Directory.Exists(acceptanceDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(acceptanceDirectory, "*.json").Order(StringComparer.Ordinal))
            {
                AcceptanceRecord? acceptance;
                try { acceptance = JsonSerializer.Deserialize<AcceptanceRecord>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options); }
                catch (JsonException) { warnings.Add($"Malformed acceptance record: {Path.GetFileName(path)}."); continue; }
                if (acceptance is null || acceptance.Status != AcceptanceStatus.Accepted || !staleClaims.Contains(acceptance.ClaimId)) continue;
                await canonical.UpdateAsync<AcceptanceRecord>(root, "acceptances", acceptance.Id, value => value.Status == AcceptanceStatus.Accepted ? value with { Status = AcceptanceStatus.NeedsReview } : value, cancellationToken);
                warnings.Add($"Acceptance {acceptance.Id} needs review because claim {acceptance.ClaimId} is stale.");
                acceptancesFlagged++;
            }
        }

        if (claimsStaled > 0 || acceptancesFlagged > 0)
            await RecordAsync(root, "trust.refreshed", "TRUST", new { claimsStaled, acceptancesFlagged, warnings }, cancellationToken);
        return new TrustRefreshResult(claimsStaled, acceptancesFlagged, warnings);
    }

    public async Task<HandoffRecord> HandoffAsync(string root, CancellationToken cancellationToken = default)
    {
        var trust = await RefreshTrustAsync(root, cancellationToken);
        var knowledge = await KnowledgeConflictAnalyzer.AuditAsync(root, cancellationToken);
        var snapshot = await git.CaptureAsync(root, cancellationToken);
        var current = await BoundedCurrentAsync(root, cancellationToken);
        var tasks = await LatestJsonAsync(root, "tasks", cancellationToken); var decisions = await LatestJsonAsync(root, "decisions", cancellationToken); var attempts = await LatestJsonAsync(root, "attempts", cancellationToken); var checkpoints = await LatestJsonAsync(root, "checkpoints", cancellationToken); var claims = await LatestJsonAsync(root, "claims", cancellationToken); var evidence = await LatestJsonAsync(root, "evidence", cancellationToken); var findings = await LatestJsonAsync(root, "findings", cancellationToken); var reviews = await LatestJsonAsync(root, "reviews", cancellationToken);
        var trustWarnings = trust.Warnings.Count == 0 ? "No stale trust relationships detected." : string.Join('\n', trust.Warnings.Select(warning => $"- WARNING: {warning}"));
        var knowledgeWarnings = knowledge.Issues.Count == 0 ? "No deterministic duplicate or conflict indicators detected." : string.Join('\n', knowledge.Issues.Select(issue => $"- {issue.Severity}: {issue.Kind} ({string.Join(", ", issue.EntityIds)}) — {issue.Summary}"));
        var markdown = $"# Handoff\n\n## Trust Warnings\n\n{trustWarnings}\n\n## Knowledge Warnings\n\n{knowledgeWarnings}\n\n## Current State\n\n{current}\n\n## Latest Task\n\n{tasks}\n\n## Latest Decision\n\n{decisions}\n\n## Latest Failed Attempt\n\n{attempts}\n\n## Latest Checkpoint\n\n{checkpoints}\n\n## Latest Claim\n\n{claims}\n\n## Latest Evidence\n\n{evidence}\n\n## Latest Finding\n\n{findings}\n\n## Latest Review\n\n{reviews}\n\n## Git State\n\n- Branch: {snapshot.Branch ?? "unknown"}\n- Commit: {snapshot.Commit ?? "none"}\n- Dirty: {snapshot.IsDirty}\n- Modified files: {(snapshot.ChangedFiles.Count == 0 ? "none" : string.Join(", ", snapshot.ChangedFiles))}\n\n## Next Recommended Actions\n\nReview open work, resolve knowledge warnings, retrieve targeted context, and verify claims against the current snapshot.\n";
        var id = canonical.NextId(root, "handoffs", "HANDOFF"); var item = new HandoffRecord(1, id, markdown, snapshot, DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "handoffs", id, item, cancellationToken); await RecordAsync(root, "handoff.created", id, item, cancellationToken); return item;
    }

    public async Task<RefactorCampaign> StartRefactorAsync(string root, string title, string objective, IReadOnlyList<string>? invariants = null, IReadOnlyList<string>? inventory = null, IReadOnlyList<RefactorGuard>? guards = null, CancellationToken cancellationToken = default)
    {
        var id = canonical.NextId(root, "refactors", "REF"); var item = new RefactorCampaign(1, id, title, objective, WorkStatus.InProgress, invariants ?? [], inventory ?? [], guards ?? [], DateTimeOffset.UtcNow);
        await canonical.WriteAsync(root, "refactors", id, item, cancellationToken); await RecordAsync(root, "refactor.started", id, item, cancellationToken); return item;
    }

    public async Task<RefactorCampaign> ResolveRefactorInventoryAsync(string root, string id, string inventoryItem, CancellationToken cancellationToken = default)
    {
        var item = await canonical.ReadAsync<RefactorCampaign>(root, "refactors", id, cancellationToken) ?? throw new InvalidOperationException($"Refactor {id} was not found.");
        var updated = await canonical.UpdateAsync<RefactorCampaign>(root, "refactors", id, current =>
        {
            var remaining = current.Inventory.Where(x => !string.Equals(x, inventoryItem, StringComparison.Ordinal)).ToArray();
            if (remaining.Length == current.Inventory.Count) throw new InvalidOperationException($"Inventory item '{inventoryItem}' was not found in {id}.");
            return current with { Inventory = remaining };
        }, cancellationToken);
        await RecordAsync(root, "refactor.inventory-resolved", id, new { inventoryItem, remaining = updated.Inventory.Count }, cancellationToken); return updated;
    }

    public async Task<RefactorCampaign> AddRefactorWorkstreamAsync(string root, string id, string name, string owner, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var item = await canonical.ReadAsync<RefactorCampaign>(root, "refactors", id, cancellationToken) ?? throw new InvalidOperationException($"Refactor {id} was not found.");
        if (item.Status is WorkStatus.Completed or WorkStatus.Abandoned) throw new InvalidOperationException($"Refactor {id} is terminal.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(owner) || paths.Count == 0) throw new ArgumentException("Workstream name, owner, and at least one path are required.");
        var updated = await canonical.UpdateAsync<RefactorCampaign>(root, "refactors", id, current =>
        {
            if (current.Status is WorkStatus.Completed or WorkStatus.Abandoned) throw new InvalidOperationException($"Refactor {id} is terminal.");
            var workstreams = current.Workstreams ?? [];
            if (workstreams.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException($"Workstream '{name}' already exists.");
            return current with { Workstreams = workstreams.Concat([new RefactorWorkstream(name, owner, paths, WorkStatus.Open)]).ToArray() };
        }, cancellationToken);
        await RecordAsync(root, "refactor.workstream-added", id, new { name, owner, paths }, cancellationToken); return updated;
    }

    public async Task<RefactorCampaign> AddRefactorSafePointAsync(string root, string id, string name, string? notes, CancellationToken cancellationToken = default)
    {
        var item = await canonical.ReadAsync<RefactorCampaign>(root, "refactors", id, cancellationToken) ?? throw new InvalidOperationException($"Refactor {id} was not found.");
        if (item.Status is WorkStatus.Completed or WorkStatus.Abandoned) throw new InvalidOperationException($"Refactor {id} is terminal.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Safe point name is required.");
        var safePoint = new RefactorSafePoint(name, await git.CaptureAsync(root, cancellationToken), notes, DateTimeOffset.UtcNow);
        var updated = await canonical.UpdateAsync<RefactorCampaign>(root, "refactors", id, current =>
        {
            if (current.Status is WorkStatus.Completed or WorkStatus.Abandoned) throw new InvalidOperationException($"Refactor {id} is terminal.");
            var safePoints = current.SafePoints ?? [];
            if (safePoints.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException($"Safe point '{name}' already exists.");
            return current with { SafePoints = safePoints.Concat([safePoint]).ToArray() };
        }, cancellationToken);
        await RecordAsync(root, "refactor.safe-point-added", id, safePoint, cancellationToken); return updated;
    }

    public async Task<RefactorCampaign> FinishRefactorAsync(string root, string id, CancellationToken cancellationToken = default)
    {
        var item = await canonical.ReadAsync<RefactorCampaign>(root, "refactors", id, cancellationToken) ?? throw new InvalidOperationException($"Refactor {id} was not found.");
        var failures = await VerifyRefactorAsync(root, id, cancellationToken);
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(" ", failures));
        var completed = await canonical.UpdateAsync<RefactorCampaign>(root, "refactors", id, current =>
        {
            if (current.Inventory.Count > 0) throw new InvalidOperationException($"Inventory remaining: {current.Inventory.Count}.");
            return current with { Status = WorkStatus.Completed };
        }, cancellationToken); await RecordAsync(root, "refactor.completed", id, completed, cancellationToken); return completed;
    }

    public async Task<RefactorCampaign> AbandonRefactorAsync(string root, string id, CancellationToken cancellationToken = default)
    {
        var item = await canonical.ReadAsync<RefactorCampaign>(root, "refactors", id, cancellationToken) ?? throw new InvalidOperationException($"Refactor {id} was not found.");
        if (item.Status == WorkStatus.Completed) throw new InvalidOperationException($"Completed refactor {id} cannot be abandoned.");
        var abandoned = await canonical.UpdateAsync<RefactorCampaign>(root, "refactors", id, current =>
        {
            if (current.Status == WorkStatus.Completed) throw new InvalidOperationException($"Completed refactor {id} cannot be abandoned.");
            return current with { Status = WorkStatus.Abandoned };
        }, cancellationToken);
        await RecordAsync(root, "refactor.abandoned", id, abandoned, cancellationToken); return abandoned;
    }

    public async Task<IReadOnlyList<string>> VerifyRefactorAsync(string root, string id, CancellationToken cancellationToken = default)
    {
        var item = await canonical.ReadAsync<RefactorCampaign>(root, "refactors", id, cancellationToken) ?? throw new InvalidOperationException($"Refactor {id} was not found.");
        var failures = new List<string>();
        if (item.Inventory.Count > 0) failures.Add($"Inventory remaining: {item.Inventory.Count}.");
        foreach (var guard in item.Guards.Where(x => x.Blocking && x.Kind.Equals("forbiddenReference", StringComparison.OrdinalIgnoreCase)))
        {
            var hits = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(p => !p.Contains(Path.Combine(root, ".git"), StringComparison.OrdinalIgnoreCase) && !p.Contains(Path.Combine(root, ".arifce"), StringComparison.OrdinalIgnoreCase)).Where(IsTextFile).Any(p => File.ReadAllText(p).Contains(guard.Value, StringComparison.Ordinal));
            if (hits) failures.Add($"Blocking guard failed: forbidden reference '{guard.Value}' remains.");
        }
        await Task.CompletedTask; return failures;
    }

    public async Task<string> DoctorAsync(string root, bool repair = false, CancellationToken cancellationToken = default)
    {
        var findings = new List<string>(); var store = Path.Combine(root, ".arifce");
        foreach (var required in new[] { "PROJECT.md", "CURRENT.md", "PROTOCOL.md", "config.json", Path.Combine("journal", "events.jsonl") }) if (!File.Exists(Path.Combine(store, required))) findings.Add($"MISSING {required}");
        var journalIssues = await journal.InspectAsync(root, cancellationToken); findings.AddRange(journalIssues.Select(x => $"CORRUPT {x}"));
        string? repairSummary = null;
        if (repair && journalIssues.Count > 0)
        {
            var result = await journal.RepairAsync(root, cancellationToken);
            if (result is not null) { await index.RebuildAsync(root, cancellationToken); repairSummary = $"Repaired journal: kept {result.Value.Kept}, removed {result.Value.Removed}, backup {result.Value.BackupPath}"; findings.RemoveAll(x => x.StartsWith("CORRUPT Journal line", StringComparison.Ordinal)); }
        }
        if (!File.Exists(Path.Combine(store, "index", "arifce.db"))) findings.Add("MISSING derived index; run 'arifce rebuild'.");
        var currentPath = Path.Combine(store, "CURRENT.md");
        if (File.Exists(currentPath))
        {
            var currentLength = (await File.ReadAllTextAsync(currentPath, cancellationToken)).Length;
            var limits = await CurrentLimitsAsync(root, cancellationToken);
            if (currentLength > limits.HardChars) findings.Add($"CURRENT.md exceeds the hard {limits.HardChars / 4:N0}-token safety limit ({currentLength} characters); move historical detail to checkpoints.");
            else if (currentLength > limits.SoftChars) findings.Add($"CURRENT.md exceeds the soft {limits.SoftChars / 4:N0}-token warning ({currentLength} characters); keep active state concise.");
        }
        var journalPath = Path.Combine(store, "journal", "events.jsonl");
        if (File.Exists(journalPath))
        {
            var journalLength = new FileInfo(journalPath).Length;
            if (journalLength > 50_000_000) findings.Add($"Journal exceeds 50 MB ({journalLength:N0} bytes); archive or rotate historical events before rebuilding.");
            else if (journalLength > 10_000_000) findings.Add($"Journal exceeds 10 MB ({journalLength:N0} bytes); plan rotation or archival.");
        }
        var health = findings.Count == 0 ? "Doctor: healthy" : "Doctor findings:\n- " + string.Join("\n- ", findings);
        return repairSummary is null ? health : repairSummary + Environment.NewLine + health;
    }

    private async Task RecordAsync(string root, string type, string id, object value, CancellationToken ct) { await journal.AppendAsync(root, new JournalEvent(1, Guid.NewGuid().ToString("N"), type, DateTimeOffset.UtcNow, id, value), ct); await index.UpdateIncrementalAsync(root, ct); }
    private static int Count(string root, string directory) { var path = Path.Combine(root, ".arifce", directory); return Directory.Exists(path) ? Directory.EnumerateFiles(path, "*.json").Count() : 0; }
    private static string ToTitle(string text) => string.Join(' ', text.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
    private static async Task CreateAsync(string path, string content, List<string> created, CancellationToken ct) { if (File.Exists(path)) return; Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path, content, new UTF8Encoding(false), ct); created.Add(path); }
    private static async Task<string> AdoptionDraftAsync(string root, CancellationToken ct) { var files = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly).Select(Path.GetFileName).Where(x => x is not null && x != ".git").Order(StringComparer.OrdinalIgnoreCase).ToArray(); await Task.CompletedTask; return $"# Project\n\n## Observed repository files\n\n{string.Join('\n', files.Select(x => $"- {x}"))}\n\n## Historical rationale\n\nUnknown. No rationale is inferred from structure alone.\n"; }
    private static async Task<string> LatestJsonAsync(string root, string dir, CancellationToken ct)
    {
        var folder = Path.Combine(root, ".arifce", dir); var file = Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.json").OrderDescending().FirstOrDefault() : null;
        if (file is null) return "None recorded.";
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file, ct));
            var fields = new[] { "id", "title", "statement", "summary", "description", "status", "result", "reason", "kind", "claimId", "reviewer" }
                .Where(name => document.RootElement.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null)
                .Select(name => $"{name}: {document.RootElement.GetProperty(name).ToString()}");
            return string.Join("; ", fields) is { Length: > 0 } summary ? summary : "Record has no summary fields.";
        }
        catch (JsonException) { return "Latest record is malformed JSON; inspect the canonical file."; }
    }
    private static async Task<string> BoundedCurrentAsync(string root, CancellationToken ct)
    {
        var path = Path.Combine(root, ".arifce", "CURRENT.md"); var text = await File.ReadAllTextAsync(path, ct); var limits = await CurrentLimitsAsync(root, ct);
        return text.Length <= limits.HardChars ? text : text[..limits.HardChars] + "\n\n> CURRENT.md was truncated in this handoff at the configured hard limit. Move historical detail to checkpoints.\n";
    }
    private static async Task<(int SoftChars, int HardChars)> CurrentLimitsAsync(string root, CancellationToken ct)
    {
        var path = Path.Combine(root, ".arifce", "config.json");
        if (!File.Exists(path)) return (16000, 32000);
        try
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
            var soft = document.RootElement.TryGetProperty("currentSoftTokenWarning", out var softValue) && softValue.TryGetInt32(out var softTokens) && softTokens > 0 ? softTokens * 4 : 4000 * 4;
            var hard = document.RootElement.TryGetProperty("currentHardTokenWarning", out var hardValue) && hardValue.TryGetInt32(out var hardTokens) && hardTokens > 0 ? hardTokens * 4 : 8000 * 4;
            return (Math.Min(soft, hard), Math.Max(soft, hard));
        }
        catch (JsonException) { return (16000, 32000); }
    }
    private static bool IsTextFile(string path) => new[] { ".cs", ".md", ".json", ".xml", ".yml", ".yaml", ".txt" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length] + "…";
    private static async Task<(int ExitCode, string Output)> RunNamedCommandAsync(string root, string command, CancellationToken ct)
    {
        var tokens = VerificationCommandPolicy.Tokenize(command);
        var start = new ProcessStartInfo(tokens[0]) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var token in tokens.Skip(1)) start.ArgumentList.Add(token);
        return await RunProcessAsync(start, ct);
    }
    private static async Task<(int ExitCode, string Output)> RunShellCommandAsync(string root, string command, CancellationToken ct)
    {
        var shell = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh"; var args = OperatingSystem.IsWindows() ? $"/d /s /c \"{command}\"" : $"-c \"{command.Replace("\"", "\\\"")}\"";
        return await RunProcessAsync(new ProcessStartInfo(shell, args) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true }, ct);
    }
    private static async Task<(int ExitCode, string Output)> RunProcessAsync(ProcessStartInfo start, CancellationToken ct) { using var p = new Process { StartInfo = start }; p.Start(); var stdout = await p.StandardOutput.ReadToEndAsync(ct); var stderr = await p.StandardError.ReadToEndAsync(ct); await p.WaitForExitAsync(ct); return (p.ExitCode, stdout + stderr); }
}
