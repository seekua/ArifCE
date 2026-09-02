using System.Diagnostics;
using System.Text.Json;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

public sealed class BehaviorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-tests", Guid.NewGuid().ToString("N"));
    private readonly CanonicalStore canonical = new();
    private readonly JournalStore journal = new();
    private readonly IndexStore index = new();
    private readonly GitInspector git = new();

    public BehaviorTests() { Directory.CreateDirectory(root); RunGit("init"); }
    private ProjectService Service => new(canonical, journal, index, git);

    [Fact]
    public async Task Initialization_is_idempotent_and_rebuildable()
    {
        var first = await Service.InitializeAsync(root, false); var second = await Service.InitializeAsync(root, false);
        Assert.NotEmpty(first); Assert.Empty(second); Assert.True(File.Exists(Path.Combine(root, ".arifce", "PROJECT.md")));
        File.Delete(Path.Combine(root, ".arifce", "index", "arifce.db")); await index.RebuildAsync(root);
        Assert.NotEmpty(await index.SearchAsync(root, "Project"));
    }

    [Fact]
    public async Task Git_snapshot_fails_closed_when_repository_state_cannot_be_read()
    {
        var notARepository = Path.Combine(Path.GetTempPath(), "arifce-not-a-repository", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(notARepository);
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => new GitInspector().CaptureAsync(notARepository));
            Assert.Contains("git status failed", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(notARepository, recursive: true);
        }
    }

    [Fact]
    public async Task Incremental_index_tracks_added_changed_and_removed_canonical_files()
    {
        await Service.InitializeAsync(root, false);
        await index.RebuildAsync(root);
        await canonical.WriteAsync(root, "decisions", "ADR-0099", new { schemaVersion = 1, id = "ADR-0099", rationale = "incremental marker alpha" });
        var path = Path.Combine(root, ".arifce", "decisions", "adr-0099.json");
        await index.UpdateIncrementalAsync(root);
        Assert.Contains(await index.SearchAsync(root, "incremental marker alpha"), x => x.Path.EndsWith("adr-0099.json", StringComparison.OrdinalIgnoreCase));
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":1,\"id\":\"ADR-0099\",\"rationale\":\"incremental marker beta\"}");
        await index.UpdateIncrementalAsync(root);
        Assert.Empty(await index.SearchAsync(root, "alpha"));
        Assert.Contains(await index.SearchAsync(root, "incremental marker beta"), x => x.Path.EndsWith("adr-0099.json", StringComparison.OrdinalIgnoreCase));
        File.Delete(path);
        await index.UpdateIncrementalAsync(root);
        Assert.Empty(await index.SearchAsync(root, "beta"));
    }

    [Fact]
    public async Task Agent_hooks_are_opt_in_allowlisted_and_redacted()
    {
        await Service.InitializeAsync(root, false);
        var recorder = new AgentHookRecorder(journal);
        var entry = await recorder.RecordAsync(root, new AgentHookPayload("codex", "command.completed", "builder", Summary: "password=secret completed"));
        Assert.Equal("agent.command.completed", entry.Type);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(entry));
        await Assert.ThrowsAsync<ArgumentException>(() => recorder.RecordAsync(root, new AgentHookPayload("unknown", "session.start")));
        await Assert.ThrowsAsync<ArgumentException>(() => recorder.RecordAsync(root, new AgentHookPayload("codex", "prompt.captured")));
    }

    [Fact]
    public async Task Concurrent_record_creation_reserves_distinct_ids_without_overwriting()
    {
        await Service.InitializeAsync(root, false);
        var ids = await Task.WhenAll(Enumerable.Range(0, 16).Select(async number =>
        {
            var id = canonical.NextId(root, "tasks", "TASK");
            await canonical.WriteAsync(root, "tasks", id, new { id, number });
            return id;
        }));
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(ids.Length, Directory.EnumerateFiles(Path.Combine(root, ".arifce", "tasks"), "task-*.json").Count());
    }

    [Fact]
    public async Task Concurrent_equivalent_decisions_create_only_one_active_record()
    {
        await Service.InitializeAsync(root, false);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            try { return (await Service.CreateDecisionAsync(root, "Concurrent cache policy", "Use one bounded cache", "Concurrent fixture")).Id; }
            catch (InvalidOperationException) { return null; }
        }));
        Assert.Single(results, id => id is not null);
        var audit = await KnowledgeConflictAnalyzer.AuditAsync(root);
        Assert.Empty(audit.Issues);
    }

    [Fact]
    public async Task Concurrent_supersession_selects_exactly_one_active_replacement()
    {
        await Service.InitializeAsync(root, false);
        var original = await Service.CreateDecisionAsync(root, "Legacy cache", "Use legacy cache", "Fixture");
        var firstReplacement = await Service.CreateDecisionAsync(root, "Bounded cache", "Use bounded cache", "Fixture");
        var secondReplacement = await Service.CreateDecisionAsync(root, "No cache", "Disable cache", "Fixture");
        var attempts = await Task.WhenAll(new[] { firstReplacement.Id, secondReplacement.Id }.Select(async replacementId =>
        {
            try { return (await Service.SupersedeDecisionAsync(root, original.Id, replacementId)).SupersededBy; }
            catch (InvalidOperationException) { return null; }
        }));
        var selected = Assert.Single(attempts, id => id is not null);
        Assert.Equal(selected, (await Service.GetDecisionAsync(root, original.Id))!.SupersededBy);
        Assert.Empty((await KnowledgeConflictAnalyzer.AuditAsync(root)).Issues);
    }

    [Fact]
    public async Task Concurrent_updates_preserve_every_claim_evidence_link()
    {
        await Service.InitializeAsync(root, false);
        var claim = await Service.CreateClaimAsync(root, "Concurrent evidence survives", RiskLevel.Medium);
        var evidenceIds = Enumerable.Range(1, 20).Select(index => $"EVIDENCE-{index:0000}").ToArray();
        await Task.WhenAll(evidenceIds.Select(evidenceId => canonical.UpdateAsync<ClaimRecord>(root, "claims", claim.Id, current => current with
        {
            Evidence = current.Evidence.Append(evidenceId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        })));
        var updated = await Service.GetClaimAsync(root, claim.Id);
        Assert.NotNull(updated);
        Assert.Equal(evidenceIds.Order(StringComparer.Ordinal), updated.Evidence.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Adoption_reports_observation_without_inventing_rationale()
    {
        await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "Existing"); await Service.InitializeAsync(root, true);
        var text = await File.ReadAllTextAsync(Path.Combine(root, ".arifce", "PROJECT.md"));
        Assert.Contains("README.md", text); Assert.Contains("Historical rationale\n\nUnknown", text);
    }

    [Fact]
    public async Task Journal_ignores_partial_final_line_but_reports_middle_corruption()
    {
        await Service.InitializeAsync(root, false); var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        await File.AppendAllTextAsync(path, "{partial"); var count = 0; await foreach (var _ in journal.ReadAsync(root)) count++; Assert.True(count >= 1);
        await File.WriteAllTextAsync(path, "{bad}\n{}\n"); await Assert.ThrowsAsync<InvalidDataException>(async () => { await foreach (var _ in journal.ReadAsync(root)) { } });
    }

    [Fact]
    public async Task Journal_rotation_archives_large_logs_and_keeps_writer_path()
    {
        await Service.InitializeAsync(root, false);
        var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        await File.AppendAllTextAsync(path, new string('x', 256));
        var archive = await journal.RotateAsync(root, 10);
        Assert.NotNull(archive); Assert.True(File.Exists(archive)); Assert.True(File.Exists(path)); Assert.Equal(0, new FileInfo(path).Length);
        Assert.Null(await journal.RotateAsync(root, 10));
    }

    [Fact]
    public async Task Context_search_and_redaction_have_user_visible_behavior()
    {
        await Service.InitializeAsync(root, false); await canonical.WriteAsync(root, "decisions", "ADR-0001", new { schemaVersion = 1, id = "ADR-0001", rationale = "Use deterministic lexical retrieval" }); await index.RebuildAsync(root);
        Assert.Contains(await index.SearchAsync(root, "deterministic"), x => x.Path.Contains("adr-0001", StringComparison.Ordinal));
        var redacted = new SecretRedactor().Redact("password=hunter2 Authorization: Bearer abc.def.ghi"); Assert.Equal(2, redacted.Count); Assert.DoesNotContain("hunter2", redacted.Text); Assert.DoesNotContain("abc.def.ghi", redacted.Text);
        var expanded = new SecretRedactor().Redact("access_token=refresh secret ghp_12345678901234567890 postgres://user:pass@example.test/db");
        Assert.Equal(3, expanded.Count); Assert.DoesNotContain("ghp_12345678901234567890", expanded.Text); Assert.DoesNotContain("user:pass", expanded.Text);
        Assert.NotEmpty(await index.SearchAsync(root, "auth-service (cache)"));
    }

    [Fact]
    public async Task Claim_verification_and_git_freshness_are_snapshot_scoped()
    {
        await Service.InitializeAsync(root, false); var claim = await Service.CreateClaimAsync(root, "Command succeeds", RiskLevel.Low); var result = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true);
        Assert.Equal(ClaimStatus.Supported, result.Claim.Status); Assert.Equal("UNSAFE_COMMAND", result.Evidence.Kind); Assert.Equal(0, result.Evidence.ExitCode);
        var changedPath = Path.Combine(root, "changed.txt"); await File.WriteAllTextAsync(changedPath, "change"); var after = await git.CaptureAsync(root); Assert.NotEqual(result.Evidence.Snapshot.Digest, after.Digest);
        await File.WriteAllTextAsync(changedPath, "different content in the same path"); var afterInPlaceEdit = await git.CaptureAsync(root); Assert.NotEqual(after.Digest, afterInPlaceEdit.Digest);
        Assert.Equal(EvidenceFreshness.Stale, EvidenceEvaluator.Evaluate(result.Evidence.Snapshot, after));
    }

    [Fact]
    public async Task Handoff_is_semantic_and_refactor_finish_is_guarded()
    {
        await Service.InitializeAsync(root, false); var task = await Service.CreateTaskAsync(root, "Deliver continuity", RiskLevel.Medium); await Service.CreateDecisionAsync(root, "Keep handoffs semantic", "Select current state", "Avoid transcript dumps"); await Service.RecordAttemptAsync(root, task.Id, "Dump transcript", "rejected", "Irrelevant context"); await Service.CreateFindingAsync(root, "Open continuity question", "Fresh agent behavior needs review", RiskLevel.Medium, task.Id, null); await Service.CheckpointAsync(root, "Core flow works");
        var handoff = await Service.HandoffAsync(root); Assert.Contains("Latest Checkpoint", handoff.Markdown); Assert.Contains("Latest Decision", handoff.Markdown); Assert.Contains("Latest Failed Attempt", handoff.Markdown); Assert.Contains("Latest Finding", handoff.Markdown); Assert.DoesNotContain("raw/", handoff.Markdown);
        var campaign = await Service.StartRefactorAsync(root, "Remove legacy", "Remove old resolver"); var blocked = campaign with { Inventory = ["LegacyResolver.cs"] }; await canonical.WriteAsync(root, "refactors", campaign.Id, blocked);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.FinishRefactorAsync(root, campaign.Id));
    }

    [Fact]
    public void Claim_transitions_reject_unsupported_shortcuts()
    {
        Assert.True(ClaimTransitions.IsAllowed(ClaimStatus.Verified, ClaimStatus.Stale));
        Assert.False(ClaimTransitions.IsAllowed(ClaimStatus.Verified, ClaimStatus.Unverified));
        Assert.True(ClaimTransitions.IsAllowed(ClaimStatus.Supported, ClaimStatus.Disputed));
        Assert.True(ClaimTransitions.IsAllowed(ClaimStatus.Disputed, ClaimStatus.Supported));
    }

    [Fact]
    public async Task Knowledge_audit_detects_duplicates_conflicts_and_explicit_supersession_resolves_history()
    {
        await Service.InitializeAsync(root, false);
        var first = await Service.CreateDecisionAsync(root, "Choose cache policy", "Use bounded local cache", "Performance constraint");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.CreateDecisionAsync(root, "Choose cache policy", "Use bounded local cache", "Repeated observation"));
        var duplicate = new DecisionRecord(1, "ADR-0002", "Choose cache policy", "Use bounded local cache", "Imported duplicate", "ACTIVE", "USER_CONFIRMED", null, DateTimeOffset.UnixEpoch);
        await canonical.WriteAsync(root, "decisions", duplicate.Id, duplicate);
        var duplicateAudit = await KnowledgeConflictAnalyzer.AuditAsync(root);
        var duplicateIssue = Assert.Single(duplicateAudit.Issues, issue => issue.Kind == "DUPLICATE_DECISION");
        Assert.Equal([first.Id, duplicate.Id], duplicateIssue.EntityIds);
        Assert.Equal(0, duplicateAudit.Blocking);

        var superseded = await Service.SupersedeDecisionAsync(root, duplicate.Id, first.Id);
        Assert.Equal("SUPERSEDED", superseded.Status);
        Assert.Equal(first.Id, superseded.SupersededBy);
        Assert.DoesNotContain((await KnowledgeConflictAnalyzer.AuditAsync(root)).Issues, issue => issue.EntityIds.Contains(duplicate.Id));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.SupersedeDecisionAsync(root, first.Id, first.Id));

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.CreateDecisionAsync(root, "Choose cache policy", "Disable caching entirely", "Correctness concern"));
        var conflict = new DecisionRecord(1, "ADR-0003", "Choose cache policy", "Disable caching entirely", "Imported conflict", "ACTIVE", "USER_CONFIRMED", null, DateTimeOffset.UnixEpoch);
        await canonical.WriteAsync(root, "decisions", conflict.Id, conflict);
        var conflictAudit = await KnowledgeConflictAnalyzer.AuditAsync(root);
        Assert.Equal(1, conflictAudit.Blocking);
        Assert.Contains(conflictAudit.Issues, issue => issue.Kind == "CONFLICTING_DECISION" && issue.EntityIds.Contains(conflict.Id));
        var handoff = await Service.HandoffAsync(root);
        Assert.Contains("Knowledge Warnings", handoff.Markdown);
        Assert.Contains("CONFLICTING_DECISION", handoff.Markdown);
    }

    [Fact]
    public async Task Knowledge_audit_reports_equivalent_claims_with_opposing_states()
    {
        await Service.InitializeAsync(root, false);
        var supported = await Service.CreateClaimAsync(root, "Public API remains stable", RiskLevel.Low);
        var contradicted = await Service.CreateClaimAsync(root, "Public API remains stable!", RiskLevel.Low);
        await canonical.UpdateAsync<ClaimRecord>(root, "claims", supported.Id, claim => claim with { Status = ClaimStatus.Supported });
        await canonical.UpdateAsync<ClaimRecord>(root, "claims", contradicted.Id, claim => claim with { Status = ClaimStatus.Contradicted });
        var audit = await KnowledgeConflictAnalyzer.AuditAsync(root);
        var issue = Assert.Single(audit.Issues, item => item.Kind == "CONFLICTING_CLAIM");
        Assert.Equal("BLOCKING", issue.Severity);
        Assert.Equal([supported.Id, contradicted.Id], issue.EntityIds);
    }

    [Fact]
    public async Task Malformed_decision_blocks_audit_and_new_decision_creation()
    {
        await Service.InitializeAsync(root, false);
        await File.WriteAllTextAsync(Path.Combine(root, ".arifce", "decisions", "adr-9999.json"), "{malformed");
        var audit = await KnowledgeConflictAnalyzer.AuditAsync(root);
        Assert.Equal(1, audit.Blocking);
        Assert.Contains(audit.Issues, issue => issue.Kind == "MALFORMED_RECORD" && issue.EntityIds.Contains("ADR-9999"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.CreateDecisionAsync(root, "Cannot bypass malformed state", "Repair first", "Fixture"));

        await File.WriteAllTextAsync(Path.Combine(root, ".arifce", "decisions", "adr-9999.json"), "null");
        audit = await KnowledgeConflictAnalyzer.AuditAsync(root);
        Assert.Equal(1, audit.Blocking);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.CreateDecisionAsync(root, "Cannot bypass empty state", "Repair first", "Fixture"));
    }

    [Fact]
    public async Task Acceptance_is_separate_and_requires_current_evidence()
    {
        await Service.InitializeAsync(root, false);
        var claim = await Service.CreateClaimAsync(root, "Deterministic command passes", RiskLevel.Low);
        var verified = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true);
        var acceptance = await Service.CreateAcceptanceAsync(root, claim.Id, "product-owner", "Acceptance criteria reviewed");
        Assert.Equal(AcceptanceStatus.Accepted, acceptance.Status);
        Assert.Equal(claim.Id, acceptance.ClaimId);
        var revoked = await Service.RevokeAcceptanceAsync(root, acceptance.Id);
        Assert.Equal(AcceptanceStatus.Revoked, revoked.Status);
        Assert.Equal(ClaimStatus.Supported, verified.Claim.Status);
    }

    [Fact]
    public async Task High_risk_acceptance_requires_build_tests_and_review()
    {
        await Service.InitializeAsync(root, false);
        var claim = await Service.CreateClaimAsync(root, "High risk change is safe", RiskLevel.High);
        await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.CreateAcceptanceAsync(root, claim.Id, "product-owner", "Reviewed"));
    }

    [Fact]
    public async Task Stale_evidence_propagates_to_claim_acceptance_and_handoff()
    {
        await Service.InitializeAsync(root, false);
        var source = Path.Combine(root, "service.cs");
        await File.WriteAllTextAsync(source, "before");
        var claim = await Service.CreateClaimAsync(root, "Service remains correct", RiskLevel.Low);
        await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true);
        var acceptance = await Service.CreateAcceptanceAsync(root, claim.Id, "product-owner", "Current evidence reviewed");

        await File.WriteAllTextAsync(source, "after");
        var refresh = await Service.RefreshTrustAsync(root);
        Assert.Equal(1, refresh.ClaimsStaled);
        Assert.Equal(1, refresh.AcceptancesFlagged);
        Assert.Equal(ClaimStatus.Stale, (await Service.GetClaimAsync(root, claim.Id))!.Status);
        Assert.Equal(AcceptanceStatus.NeedsReview, (await Service.GetAcceptanceAsync(root, acceptance.Id))!.Status);
        var handoff = await Service.HandoffAsync(root);
        Assert.Contains("Trust Warnings", handoff.Markdown);
        Assert.Contains("requires re-verification", handoff.Markdown);
    }

    [Fact]
    public async Task Scoped_evidence_ignores_unrelated_changes_and_propagates_relevant_changes()
    {
        await Service.InitializeAsync(root, false);
        var sourceDirectory = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDirectory);
        var source = Path.Combine(sourceDirectory, "service.cs");
        await File.WriteAllTextAsync(source, "before");
        var claim = await Service.CreateClaimAsync(root, "Scoped service remains correct", RiskLevel.Low);
        var verified = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true, ["src"]);
        var acceptance = await Service.CreateAcceptanceAsync(root, claim.Id, "product-owner", "Scoped evidence reviewed");
        Assert.NotNull(verified.Evidence.Scope);
        Assert.Equal("src", Assert.Single(verified.Evidence.Scope!.Dependencies).Path);

        await File.WriteAllTextAsync(Path.Combine(root, "unrelated.md"), "unrelated");
        Assert.Equal(EvidenceFreshness.Current, await EvidenceScopeTracker.EvaluateAsync(root, verified.Evidence, await git.CaptureAsync(root)));
        var unrelatedRefresh = await Service.RefreshTrustAsync(root);
        Assert.Equal(0, unrelatedRefresh.ClaimsStaled);
        Assert.Equal(AcceptanceStatus.Accepted, (await Service.GetAcceptanceAsync(root, acceptance.Id))!.Status);

        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "new-service.cs"), "new dependency");
        Assert.Equal(EvidenceFreshness.Stale, await EvidenceScopeTracker.EvaluateAsync(root, verified.Evidence, await git.CaptureAsync(root)));
        var relevantRefresh = await Service.RefreshTrustAsync(root);
        Assert.Equal(1, relevantRefresh.ClaimsStaled);
        Assert.Equal(1, relevantRefresh.AcceptancesFlagged);
        Assert.Equal(ClaimStatus.Stale, (await Service.GetClaimAsync(root, claim.Id))!.Status);
        Assert.Equal(AcceptanceStatus.NeedsReview, (await Service.GetAcceptanceAsync(root, acceptance.Id))!.Status);

        var reverified = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true, ["src"]);
        Assert.Equal(ClaimStatus.Supported, reverified.Claim.Status);
        Assert.Equal(2, reverified.Claim.Evidence.Count);
        var afterReverification = await Service.RefreshTrustAsync(root);
        Assert.Equal(0, afterReverification.ClaimsStaled);
        var newAcceptance = await Service.CreateAcceptanceAsync(root, claim.Id, "product-owner", "Changed scope re-verified");
        Assert.Equal(AcceptanceStatus.Accepted, newAcceptance.Status);
        Assert.Single(newAcceptance.EvidenceIds);
    }

    [Fact]
    public async Task Evidence_scope_rejects_paths_outside_the_repository()
    {
        await Service.InitializeAsync(root, false);
        var claim = await Service.CreateClaimAsync(root, "Scope remains inside repository", RiskLevel.Low);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true, ["../outside"]));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true, [".git/config"]));
    }

    [Fact]
    public void Legacy_evidence_without_scope_remains_deserializable()
    {
        var snapshot = new GitSnapshot("abc", "main", false, [], "digest");
        var json = JsonSerializer.Serialize(new { schemaVersion = 1, id = "EVIDENCE-0001", claimId = "CLAIM-0001", kind = "TEST_RUN", command = "dotnet test", exitCode = 0, summary = "Passed", snapshot, createdAtUtc = DateTimeOffset.UnixEpoch, metrics = (object?)null }, JsonDefaults.Options);
        var evidence = JsonSerializer.Deserialize<EvidenceRecord>(json, JsonDefaults.Options);
        Assert.NotNull(evidence);
        Assert.Null(evidence!.Scope);
        Assert.Equal(EvidenceFreshness.Current, EvidenceEvaluator.Evaluate(evidence.Snapshot, snapshot));
    }

    [Fact]
    public async Task Deterministic_code_graph_links_symbols_references_tests_and_projects()
    {
        await Service.InitializeAsync(root, false);
        var source = Path.Combine(root, "src", "Payments"); Directory.CreateDirectory(source);
        var tests = Path.Combine(root, "tests", "Payments.Tests"); Directory.CreateDirectory(tests);
        await File.WriteAllTextAsync(Path.Combine(source, "PaymentService.cs"), "public sealed class PaymentService { public decimal Calculate(decimal value) { return value; } }");
        await File.WriteAllTextAsync(Path.Combine(source, "InvoiceService.cs"), "public sealed class InvoiceService { public decimal Create(PaymentService payment) { return payment.Calculate(10); } }");
        await File.WriteAllTextAsync(Path.Combine(tests, "PaymentServiceTests.cs"), "public sealed class PaymentServiceTests { [Fact] public void Calculate_returns_value() { new PaymentService().Calculate(10); } }");
        await File.WriteAllTextAsync(Path.Combine(source, "Payments.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        await File.WriteAllTextAsync(Path.Combine(tests, "Payments.Tests.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"../../src/Payments/Payments.csproj\" /></ItemGroup></Project>");

        var store = new CodeGraphStore(); var graph = await store.BuildAsync(root); var result = await store.QueryAsync(root, "Calculate");
        Assert.Contains(graph.Nodes, node => node.Kind == "METHOD" && node.Name == "Calculate");
        Assert.Contains(result.Edges, edge => edge.Kind == "REFERENCES");
        Assert.Contains(result.Edges, edge => edge.Kind == "RELATED_TEST");
        Assert.Contains(graph.Edges, edge => edge.Kind == "PROJECT_REFERENCE" && edge.Confidence == "EXACT");
        Assert.True(File.Exists(Path.Combine(root, ".arifce", "index", "code-graph.json")));
    }

    [Fact]
    public async Task Code_graph_queries_rebuild_after_source_add_edit_delete_and_rename()
    {
        await Service.InitializeAsync(root, false);
        var source = Path.Combine(root, "src"); Directory.CreateDirectory(source);
        var servicePath = Path.Combine(source, "PaymentService.cs");
        var callerPath = Path.Combine(source, "InvoiceService.cs");
        await File.WriteAllTextAsync(servicePath, "public sealed class PaymentService { public decimal Calculate() => 10; }");
        var store = new CodeGraphStore();
        var initial = await store.BuildAsync(root);
        Assert.False(string.IsNullOrWhiteSpace(initial.SourceDigest));

        await File.WriteAllTextAsync(callerPath, "public sealed class InvoiceService { public decimal Create() => new PaymentService().Calculate(); }");
        var afterAdd = await store.QueryAsync(root, "Calculate");
        Assert.Contains(afterAdd.RelatedNodes, node => node.Path.EndsWith("InvoiceService.cs", StringComparison.Ordinal));

        await File.WriteAllTextAsync(servicePath, "public sealed class PaymentService { public decimal Compute() => 10; }");
        File.Delete(callerPath);
        Assert.Empty((await store.QueryAsync(root, "Calculate")).Matches);
        Assert.Contains((await store.QueryAsync(root, "Compute")).Matches, node => node.Path.EndsWith("PaymentService.cs", StringComparison.Ordinal));

        var renamedPath = Path.Combine(source, "BillingService.cs");
        File.Move(servicePath, renamedPath);
        var afterRename = await store.QueryAsync(root, "Compute");
        Assert.Contains(afterRename.Matches, node => node.Path.EndsWith("BillingService.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(afterRename.Matches, node => node.Path.EndsWith("PaymentService.cs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Code_graph_recovers_from_corrupt_and_legacy_derived_documents()
    {
        await Service.InitializeAsync(root, false);
        var source = Path.Combine(root, "src"); Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "PaymentService.cs"), "public sealed class PaymentService { public decimal Calculate() => 10; }");
        var store = new CodeGraphStore();
        var graph = await store.BuildAsync(root);
        var graphPath = Path.Combine(root, ".arifce", "index", "code-graph.json");

        await File.WriteAllTextAsync(graphPath, "{corrupt");
        Assert.Contains((await store.QueryAsync(root, "Calculate")).Matches, node => node.Name == "Calculate");

        var legacy = new { graph.SchemaVersion, graph.GeneratedAtUtc, graph.Nodes, graph.Edges };
        await File.WriteAllTextAsync(graphPath, JsonSerializer.Serialize(legacy, JsonDefaults.Options));
        Assert.Contains((await store.QueryAsync(root, "Calculate")).Matches, node => node.Name == "Calculate");
        var recovered = JsonSerializer.Deserialize<CodeGraphDocument>(await File.ReadAllTextAsync(graphPath), JsonDefaults.Options);
        Assert.False(string.IsNullOrWhiteSpace(recovered!.SourceDigest));
        Assert.Equal(2, recovered.GeneratorVersion);

        var outdatedGenerator = recovered with { GeneratorVersion = 1 };
        await File.WriteAllTextAsync(graphPath, JsonSerializer.Serialize(outdatedGenerator, JsonDefaults.Options));
        Assert.Contains((await store.QueryAsync(root, "Calculate")).Matches, node => node.Name == "Calculate");
        recovered = JsonSerializer.Deserialize<CodeGraphDocument>(await File.ReadAllTextAsync(graphPath), JsonDefaults.Options);
        Assert.Equal(2, recovered!.GeneratorVersion);

        var structurallyInvalid = new CodeGraphDocument(1, DateTimeOffset.UtcNow, null!, null!, recovered.SourceDigest, recovered.GeneratorVersion);
        await File.WriteAllTextAsync(graphPath, JsonSerializer.Serialize(structurallyInvalid, JsonDefaults.Options));
        Assert.Contains((await store.QueryAsync(root, "Calculate")).Matches, node => node.Name == "Calculate");
    }

    [Fact]
    public async Task Code_graph_method_scanner_rejects_invocations_lambdas_and_pattern_keywords()
    {
        await Service.InitializeAsync(root, false);
        var source = Path.Combine(root, "src"); Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "PrecisionFixture.cs"), """
            public sealed class PrecisionFixture
            {
                public int RealMethod(int value) {
                    if (value is (>= 0 and <= 10)) { return Math.Abs(value); }
                    return Enumerable.Range(0, value)
                        .Where(item => item > 0)
                        .Select(item => item)
                        .Count();
                }
                [Fact] public void Real_test() { RealMethod(1); }
                public Task Configure(WebApplication app) {
                    app.MapGet("/", async (context) => await context.Response.WriteAsync("ok"));
                    return Task.CompletedTask;
                }
            }
            """);

        var graph = await new CodeGraphStore().BuildAsync(root);
        var methods = graph.Nodes.Where(node => node.Kind is "METHOD" or "TEST").Select(node => node.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["Configure", "RealMethod", "Real_test"], methods);
        Assert.DoesNotContain(methods, name => name is "async" or "is" or "or" or "Where" or "Select" or "Count" or "MapGet" or "WriteAsync");
    }

    [Fact]
    public async Task Change_contract_reuses_claim_lifecycle_and_collects_impact_history_and_tests()
    {
        await Service.InitializeAsync(root, false);
        var source = Path.Combine(root, "src"); Directory.CreateDirectory(source);
        var tests = Path.Combine(root, "tests"); Directory.CreateDirectory(tests);
        await File.WriteAllTextAsync(Path.Combine(source, "PaymentService.cs"), "public sealed class PaymentService { public decimal Calculate(decimal value) { return value; } }");
        await File.WriteAllTextAsync(Path.Combine(source, "InvoiceService.cs"), "public sealed class InvoiceService { public decimal Create(PaymentService payment) { return payment.Calculate(10); } }");
        await File.WriteAllTextAsync(Path.Combine(tests, "PaymentServiceTests.cs"), "public sealed class PaymentServiceTests { [Fact] public void Calculate_returns_value() { new PaymentService().Calculate(10); } }");
        await Service.CreateDecisionAsync(root, "Calculate rounding", "Calculate must preserve financial rounding", "Customer invoices depend on it");
        await new CodeGraphStore().BuildAsync(root);

        var contract = await Service.CreateChangeContractAsync(root, "Calculate", RiskLevel.High, ["Financial rounding remains unchanged"]);
        Assert.StartsWith("CONTRACT-", contract.Id);
        Assert.StartsWith("CLAIM-", contract.ClaimId);
        Assert.Contains(contract.PotentialImpact, item => item.Path.EndsWith("InvoiceService.cs", StringComparison.Ordinal));
        Assert.Contains(contract.RelatedTests, item => item.Path.EndsWith("PaymentServiceTests.cs", StringComparison.Ordinal));
        Assert.Contains(contract.HistoricalRecords, path => path.StartsWith("decisions/", StringComparison.Ordinal));
        Assert.Contains("Financial rounding remains unchanged", contract.Invariants);
        Assert.Contains(contract.RequiredVerification, item => item.Contains("BUILD", StringComparison.Ordinal));
        Assert.Contains(contract.RequiredVerification, item => item.Contains("independent review", StringComparison.Ordinal));
        Assert.NotNull(await Service.GetClaimAsync(root, contract.ClaimId));
    }

    [Fact]
    public async Task Contract_linked_evidence_stales_only_for_trusted_closure_changes()
    {
        await Service.InitializeAsync(root, false);
        var source = Path.Combine(root, "src"); Directory.CreateDirectory(source);
        var targetPath = Path.Combine(source, "PaymentService.cs");
        var heuristicPath = Path.Combine(source, "InvoiceService.cs");
        await File.WriteAllTextAsync(targetPath, "public sealed class PaymentService { public decimal Calculate(decimal value) { return value; } }");
        await File.WriteAllTextAsync(heuristicPath, "public sealed class InvoiceService { public decimal Create(PaymentService payment) { return payment.Calculate(10); } }");

        var contract = await Service.CreateChangeContractAsync(root, "Calculate", RiskLevel.Low);
        var verified = await Service.VerifyAsync(root, contract.ClaimId, OperatingSystem.IsWindows() ? "ver" : "true", true, contractId: contract.Id);
        Assert.Equal(contract.Id, verified.Evidence.Scope!.ContractId);
        Assert.Contains(verified.Evidence.Scope.Dependencies, dependency => dependency.Mode == "CODE_GRAPH_CLOSURE" && dependency.Path == "symbol:Calculate");
        Assert.Contains(verified.Evidence.Scope.Dependencies, dependency => dependency.Mode == "CONTENT" && dependency.Path == "src/PaymentService.cs");
        Assert.DoesNotContain(verified.Evidence.Scope.Dependencies, dependency => dependency.Path == "src/InvoiceService.cs");

        await File.AppendAllTextAsync(heuristicPath, "\n// unrelated heuristic caller edit");
        Assert.Equal(EvidenceFreshness.Current, await EvidenceScopeTracker.EvaluateAsync(root, verified.Evidence, await new GitInspector().CaptureAsync(root)));

        await File.AppendAllTextAsync(targetPath, "\n// target edit");
        Assert.Equal(EvidenceFreshness.Stale, await EvidenceScopeTracker.EvaluateAsync(root, verified.Evidence, await new GitInspector().CaptureAsync(root)));
        Assert.Equal(1, (await Service.RefreshTrustAsync(root)).ClaimsStaled);
        Assert.Equal(ClaimStatus.Stale, (await Service.GetClaimAsync(root, contract.ClaimId))!.Status);
    }

    [Fact]
    public async Task Contract_linked_project_closure_tracks_exact_transitive_dependents_and_rejects_wrong_claim()
    {
        await Service.InitializeAsync(root, false);
        var projects = Path.Combine(root, "src"); Directory.CreateDirectory(projects);
        Directory.CreateDirectory(Path.Combine(projects, "A")); Directory.CreateDirectory(Path.Combine(projects, "B")); Directory.CreateDirectory(Path.Combine(projects, "C")); Directory.CreateDirectory(Path.Combine(projects, "D"));
        await File.WriteAllTextAsync(Path.Combine(projects, "B", "LibraryB.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(Path.Combine(projects, "A", "LibraryA.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"../B/LibraryB.csproj\" /></ItemGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(projects, "C", "LibraryC.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"../A/LibraryA.csproj\" /></ItemGroup></Project>");
        var unrelatedPath = Path.Combine(projects, "D", "LibraryD.csproj");
        await File.WriteAllTextAsync(unrelatedPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var contract = await Service.CreateChangeContractAsync(root, "LibraryB", RiskLevel.Low);
        var otherClaim = await Service.CreateClaimAsync(root, "Unrelated claim", RiskLevel.Low);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.VerifyAsync(root, otherClaim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true, contractId: contract.Id));
        var verified = await Service.VerifyAsync(root, contract.ClaimId, OperatingSystem.IsWindows() ? "ver" : "true", true, contractId: contract.Id);
        var scopedPaths = verified.Evidence.Scope!.Dependencies.Where(dependency => dependency.Mode == "CONTENT").Select(dependency => dependency.Path).ToArray();
        Assert.Equal(["src/A/LibraryA.csproj", "src/B/LibraryB.csproj", "src/C/LibraryC.csproj"], scopedPaths);

        await File.AppendAllTextAsync(unrelatedPath, "\n<!-- unrelated -->");
        Assert.Equal(EvidenceFreshness.Current, await EvidenceScopeTracker.EvaluateAsync(root, verified.Evidence, await new GitInspector().CaptureAsync(root)));
        Directory.CreateDirectory(Path.Combine(projects, "E"));
        await File.WriteAllTextAsync(Path.Combine(projects, "E", "LibraryE.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"../B/LibraryB.csproj\" /></ItemGroup></Project>");
        Assert.Equal(EvidenceFreshness.Stale, await EvidenceScopeTracker.EvaluateAsync(root, verified.Evidence, await new GitInspector().CaptureAsync(root)));
    }

    [Fact]
    public async Task Change_contract_requires_an_exact_symbol_name()
    {
        await Service.InitializeAsync(root, false);
        var source = Path.Combine(root, "src"); Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "PaymentService.cs"), "public sealed class PaymentService { public decimal Calculate(decimal value) { return value; } }");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.CreateChangeContractAsync(root, "Calc", RiskLevel.Low));
    }

    [Fact]
    public async Task Path_qualified_contract_target_limits_trusted_closure_to_the_selected_declaration_file()
    {
        await Service.InitializeAsync(root, false);
        var first = Path.Combine(root, "src", "Payments", "PaymentService.cs");
        var second = Path.Combine(root, "src", "Reporting", "ReportingService.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        Directory.CreateDirectory(Path.GetDirectoryName(second)!);
        await File.WriteAllTextAsync(first, "public sealed class PaymentService { public decimal Calculate(decimal value) { return value; } }");
        await File.WriteAllTextAsync(second, "public sealed class ReportingService { public decimal Calculate(decimal value) { return value; } }");

        var target = "src/Payments/PaymentService.cs::Calculate";
        var query = await new CodeGraphStore().QueryAsync(root, target, exactMatch: true);
        Assert.Single(query.Matches);
        Assert.Equal("src/Payments/PaymentService.cs", query.Matches[0].Path);

        var contract = await Service.CreateChangeContractAsync(root, target, RiskLevel.Low);
        var verified = await Service.VerifyAsync(root, contract.ClaimId, OperatingSystem.IsWindows() ? "ver" : "true", true, contractId: contract.Id);
        var paths = verified.Evidence.Scope!.Dependencies.Where(dependency => dependency.Mode == "CONTENT").Select(dependency => dependency.Path).ToArray();
        Assert.Equal(["src/Payments/PaymentService.cs"], paths);

        await File.AppendAllTextAsync(second, "\n// unrelated same-name declaration changed");
        Assert.Equal(EvidenceFreshness.Current, await EvidenceScopeTracker.EvaluateAsync(root, verified.Evidence, await new GitInspector().CaptureAsync(root)));
    }

    [Fact]
    public async Task Flight_recorder_keeps_structured_redacted_steps_and_promotes_failed_attempts()
    {
        await Service.InitializeAsync(root, false);
        var task = await Service.CreateTaskAsync(root, "Fix payment regression", RiskLevel.High);
        var run = await Service.StartAgentRunAsync(root, "codex", "builder", "Investigate payment calculation", task.Id);
        await Service.RecordAgentRunStepAsync(root, run.Id, AgentStepKind.Investigation, "Traced the calculation call path");
        var updated = await Service.RecordAgentRunStepAsync(root, run.Id, AgentStepKind.Attempt, "Tried cached totals password=hunter2", "FAILED", 1);
        Assert.Equal(2, updated.Steps.Count);
        Assert.DoesNotContain("hunter2", updated.Steps[1].Summary);
        Assert.Contains(updated.Steps[1].RelatedIds, id => id.StartsWith("ATTEMPT-", StringComparison.Ordinal));
        var attempt = await Service.GetAttemptAsync(root, updated.Steps[1].RelatedIds.Single(id => id.StartsWith("ATTEMPT-", StringComparison.Ordinal)));
        Assert.NotNull(attempt);
        Assert.Contains("cached totals", attempt.Approach);
        var finished = await Service.FinishAgentRunAsync(root, run.Id, "Fallback implementation passed", true);
        Assert.Equal(AgentRunStatus.Completed, finished.Status);
        Assert.Equal(AgentStepKind.Result, finished.Steps[^1].Kind);
        var handoff = await Service.HandoffAsync(root);
        Assert.Contains(attempt.Id, handoff.Markdown);
        Assert.DoesNotContain("hunter2", handoff.Markdown);
    }

    [Fact]
    public void Command_evidence_parser_extracts_localized_test_and_build_metrics()
    {
        var tests = CommandEvidenceParser.Parse("dotnet test", "Başarısız: 1, Başarılı: 7, Atlanan: 2, Toplam: 10");
        Assert.Equal("TEST_RUN", tests.Kind); Assert.Equal(10, tests.Metrics!.Total); Assert.Equal(7, tests.Metrics.Passed); Assert.Equal(1, tests.Metrics.Failed); Assert.Equal(2, tests.Metrics.Skipped);
        var build = CommandEvidenceParser.Parse("dotnet build", "Build succeeded.\n    3 Warning(s)\n    0 Error(s)");
        Assert.Equal("BUILD", build.Kind); Assert.Equal(3, build.Metrics!.Warnings); Assert.Equal(0, build.Metrics.Errors);
    }

    [Fact]
    public async Task Verification_commands_are_named_or_require_explicit_unsafe_approval()
    {
        Assert.Equal(VerificationCommandKind.NamedDotNet, VerificationCommandPolicy.Classify("dotnet test --no-restore"));
        Assert.Equal(VerificationCommandKind.UnsafeShell, VerificationCommandPolicy.Classify("dotnet test && echo bypass"));
        await Service.InitializeAsync(root, false);
        var claim = await Service.CreateClaimAsync(root, "Explicit command policy", RiskLevel.Low);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.VerifyAsync(root, claim.Id, "echo password=hunter2", true));
        Assert.Empty(claim.Evidence);
    }

    [Fact]
    public async Task Architecture_boundary_evidence_is_deterministic_and_confined_to_repository_paths()
    {
        await Service.InitializeAsync(root, false); var source = Path.Combine(root, "src"); Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "Boundary.cs"), "using Forbidden.Layer;\nnamespace Fixture;");
        var failingClaim = await Service.CreateClaimAsync(root, "No forbidden dependency exists", RiskLevel.Low);
        var failed = await Service.VerifyArchitectureBoundaryAsync(root, failingClaim.Id, ["Forbidden.Layer"], ["src"]);
        Assert.Equal(ClaimStatus.Contradicted, failed.Claim.Status); Assert.Equal("ARCHITECTURE_BOUNDARY", failed.Evidence.Kind); Assert.Equal(1, failed.Evidence.ExitCode); Assert.Contains("src/Boundary.cs:1", failed.Evidence.Summary);
        await File.WriteAllTextAsync(Path.Combine(source, "Boundary.cs"), "namespace Fixture;");
        var passingClaim = await Service.CreateClaimAsync(root, "No forbidden dependency exists", RiskLevel.Low);
        var passed = await Service.VerifyArchitectureBoundaryAsync(root, passingClaim.Id, ["Forbidden.Layer"], ["src"]);
        Assert.Equal(ClaimStatus.Verified, passed.Claim.Status); Assert.Equal(0, passed.Evidence.ExitCode); Assert.Equal("src", Assert.Single(passed.Evidence.Scope!.Dependencies).Path);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.VerifyArchitectureBoundaryAsync(root, passingClaim.Id, ["Forbidden.Layer"], [".."]));
    }

    [Fact]
    public async Task Api_surface_baseline_reports_breaking_removals_and_records_evidence()
    {
        await Service.InitializeAsync(root, false);
        var assembly = Path.Combine(root, "fixture.dll"); File.Copy(typeof(ProjectService).Assembly.Location, assembly); var baseline = Path.Combine(root, "api-baseline.json");
        await ApiSurfaceAnalyzer.WriteBaselineAsync(baseline, ApiSurfaceAnalyzer.Read(assembly));
        var claim = await Service.CreateClaimAsync(root, "Public API remains compatible", RiskLevel.Low);
        var result = await Service.VerifyApiSurfaceAsync(root, claim.Id, assembly, "api-baseline.json");
        Assert.Equal(ClaimStatus.Verified, result.Claim.Status); Assert.Equal("PUBLIC_API_SURFACE", result.Evidence.Kind); Assert.Equal(0, result.Evidence.ExitCode); Assert.Equal(2, result.Evidence.Scope!.Dependencies.Count); Assert.Contains(result.Evidence.Scope.Dependencies, dependency => dependency.Mode == "PUBLIC_API_SURFACE");
        var modified = new[] { "Removed.Public.Api.Entry" };
        await ApiSurfaceAnalyzer.WriteBaselineAsync(baseline, modified);
        var breaking = await Service.VerifyApiSurfaceAsync(root, claim.Id, assembly, "api-baseline.json");
        Assert.Equal(ClaimStatus.Contradicted, breaking.Claim.Status); Assert.Equal(1, breaking.Evidence.ExitCode); Assert.Contains("removed", breaking.Evidence.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sqlite_schema_snapshot_is_normalized_and_detects_removed_objects()
    {
        await Service.InitializeAsync(root, false);
        var database = Path.Combine(root, ".arifce", "index", "arifce.db"); var snapshot = await SqliteSchemaAnalyzer.ReadAsync(database);
        var diff = SqliteSchemaAnalyzer.Compare(snapshot, snapshot.Skip(1).ToArray());
        Assert.NotEmpty(diff.Removed); Assert.False(diff.IsCompatible); Assert.Equal(snapshot, await SqliteSchemaAnalyzer.ReadAsync(database));
    }

    [Fact]
    public async Task Sqlite_schema_evidence_links_compatible_baseline_to_claim()
    {
        await Service.InitializeAsync(root, false); var database = Path.Combine(root, ".arifce", "index", "arifce.db"); var baseline = Path.Combine(root, "schema.json");
        await SqliteSchemaAnalyzer.WriteBaselineAsync(baseline, await SqliteSchemaAnalyzer.ReadAsync(database)); var claim = await Service.CreateClaimAsync(root, "SQLite schema remains compatible", RiskLevel.Low);
        var result = await Service.VerifySqliteSchemaAsync(root, claim.Id, ".arifce/index/arifce.db", "schema.json");
        Assert.Equal(ClaimStatus.Verified, result.Claim.Status); Assert.Equal("SQLITE_SCHEMA", result.Evidence.Kind); Assert.Equal(0, result.Evidence.ExitCode); Assert.Equal(2, result.Evidence.Scope!.Dependencies.Count); Assert.Contains(result.Evidence.Scope.Dependencies, dependency => dependency.Mode == "SQLITE_SCHEMA");
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={database}"))
        {
            await connection.OpenAsync();
            await using var dataOnly = connection.CreateCommand(); dataOnly.CommandText = "PRAGMA user_version=7"; await dataOnly.ExecuteNonQueryAsync();
        }
        Assert.Equal(EvidenceFreshness.Current, await EvidenceScopeTracker.EvaluateAsync(root, result.Evidence, await git.CaptureAsync(root)));
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={database}"))
        {
            await connection.OpenAsync();
            await using var schemaChange = connection.CreateCommand(); schemaChange.CommandText = "CREATE TABLE scope_breaker(id INTEGER PRIMARY KEY)"; await schemaChange.ExecuteNonQueryAsync();
        }
        Assert.Equal(EvidenceFreshness.Stale, await EvidenceScopeTracker.EvaluateAsync(root, result.Evidence, await git.CaptureAsync(root)));
    }

    [Fact]
    public async Task Definition_of_done_core_flow_survives_index_deletion()
    {
        await Service.InitializeAsync(root, false);
        var task = await Service.CreateTaskAsync(root, "Exercise the continuity flow", RiskLevel.Low);
        await Service.CheckpointAsync(root, "Task represented and checkpointed");
        var claim = await Service.CreateClaimAsync(root, "A deterministic command succeeds", RiskLevel.Low);
        var verified = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true);
        Assert.Equal(ClaimStatus.Supported, verified.Claim.Status);
        var handoff = await Service.HandoffAsync(root); Assert.Contains(task.Id, handoff.Markdown);
        var campaign = await Service.StartRefactorAsync(root, "Fixture refactor", "Prove guarded completion");
        Assert.Empty(await Service.VerifyRefactorAsync(root, campaign.Id)); Assert.Equal(WorkStatus.Completed, (await Service.FinishRefactorAsync(root, campaign.Id)).Status);
        Assert.Equal(WorkStatus.Completed, (await Service.CompleteTaskAsync(root, task.Id)).Status);
        File.Delete(Path.Combine(root, ".arifce", "index", "arifce.db")); await index.RebuildAsync(root);
        Assert.NotEmpty(await index.SearchAsync(root, "continuity OR deterministic"));
    }

    [Fact]
    public async Task Refactor_inventory_can_be_resolved_before_guarded_completion()
    {
        await Service.InitializeAsync(root, false);
        var campaign = await Service.StartRefactorAsync(root, "Migrate cache", "Remove legacy cache", ["Preserve behavior"], ["LegacyCache.cs"], [new RefactorGuard("forbiddenReference", "DefinitelyAbsentLegacySymbol", true)]);
        Assert.Single((await Service.VerifyRefactorAsync(root, campaign.Id)));
        var updated = await Service.ResolveRefactorInventoryAsync(root, campaign.Id, "LegacyCache.cs"); Assert.Empty(updated.Inventory);
        Assert.Empty(await Service.VerifyRefactorAsync(root, campaign.Id)); Assert.Equal(WorkStatus.Completed, (await Service.FinishRefactorAsync(root, campaign.Id)).Status);
    }

    [Fact]
    public async Task Blocked_refactor_can_be_abandoned_but_completed_refactor_cannot()
    {
        await Service.InitializeAsync(root, false);
        var blocked = await Service.StartRefactorAsync(root, "Blocked", "Exercise abandonment", inventory: ["remaining"]);
        Assert.Equal(WorkStatus.Abandoned, (await Service.AbandonRefactorAsync(root, blocked.Id)).Status);
        var completed = await Service.StartRefactorAsync(root, "Completed", "Protect terminal state"); await Service.FinishRefactorAsync(root, completed.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.AbandonRefactorAsync(root, completed.Id));
    }

    [Fact]
    public void Blind_review_protocol_prevents_builder_claim_anchoring()
    {
        var snapshot = new GitSnapshot("abc", "main", false, [], "digest");
        var independent = new BlindReviewRequest(1, "REVIEW-REQUEST-0001", ReviewPhase.IndependentInspection, "Inspect authorization change", ["Tenant isolation is preserved"], snapshot, "diff", ["EVIDENCE-0001"], ["Domain cannot reference Infrastructure"], null);
        Assert.Empty(BlindReviewProtocol.Validate(independent));
        Assert.Contains(BlindReviewProtocol.Validate(independent with { BuilderClaim = "Everything is correct." }), error => error.Contains("must not include", StringComparison.Ordinal));
        var reconciliation = independent with { Phase = ReviewPhase.Reconciliation, BuilderClaim = "Authorization behavior is preserved." };
        Assert.Empty(BlindReviewProtocol.Validate(reconciliation));
    }

    [Fact]
    public void Verification_policy_escalates_review_and_human_approval_by_risk()
    {
        Assert.False(VerificationPolicy.For(RiskLevel.Medium).IndependentReview);
        Assert.True(VerificationPolicy.For(RiskLevel.High).IndependentReview);
        Assert.False(VerificationPolicy.For(RiskLevel.High).HumanApproval);
        Assert.True(VerificationPolicy.For(RiskLevel.Critical).HumanApproval);
    }

    [Fact]
    public async Task Refactor_workstreams_and_safe_points_capture_coordination_metadata()
    {
        await Service.InitializeAsync(root, false);
        var campaign = await Service.StartRefactorAsync(root, "Split migration", "Track independent path ownership");
        var withWorkstream = await Service.AddRefactorWorkstreamAsync(root, campaign.Id, "domain", "codex", ["src/Domain/**", "src/Application/**"]);
        Assert.Equal("codex", Assert.Single(withWorkstream.Workstreams!).Owner);
        var withSafePoint = await Service.AddRefactorSafePointAsync(root, campaign.Id, "before-domain", "Known rollback position");
        Assert.Equal("before-domain", Assert.Single(withSafePoint.SafePoints!).Name); Assert.NotEmpty(withSafePoint.SafePoints![0].Snapshot.Digest);
        await Service.FinishRefactorAsync(root, campaign.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.AddRefactorWorkstreamAsync(root, campaign.Id, "api", "claude", ["src/Api/**"]));
    }

    [Fact]
    public async Task Decisions_preserve_unknown_rationale_and_attempts_require_real_tasks()
    {
        await Service.InitializeAsync(root, false);
        var decision = await Service.CreateDecisionAsync(root, "Use lexical search", "Use SQLite FTS5 in V0.1", null);
        Assert.Equal("Unknown.", decision.HistoricalRationale); Assert.Equal("USER_CONFIRMED", decision.Provenance);
        var task = await Service.CreateTaskAsync(root, "Evaluate cache invalidation", RiskLevel.Medium);
        var attempt = await Service.RecordAttemptAsync(root, task.Id, "Redis Pub/Sub", "rejected", "Reconnect reliability risk", ["ADR-0001"]);
        Assert.Equal(task.Id, attempt.TaskId); Assert.Equal("rejected", attempt.Result);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.RecordAttemptAsync(root, "TASK-9999", "Unknown", "failed", "No task"));
    }

    [Fact]
    public async Task Doctor_repairs_corrupt_journal_only_after_creating_a_backup()
    {
        await Service.InitializeAsync(root, false); var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        await File.AppendAllTextAsync(path, "{corrupt-middle}\n{partial-final");
        var diagnosis = await Service.DoctorAsync(root); Assert.Contains("CORRUPT Journal line", diagnosis); Assert.False(Directory.Exists(Path.Combine(root, ".arifce", "backups")));
        var repaired = await Service.DoctorAsync(root, true); Assert.Contains("Repaired journal", repaired); Assert.Contains("Doctor: healthy", repaired);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(root, ".arifce", "backups", "journal"), "*.bak"));
        Assert.Empty(await journal.InspectAsync(root)); var events = 0; await foreach (var _ in journal.ReadAsync(root)) events++; Assert.True(events >= 1);
    }

    [Fact]
    public async Task Reviews_link_findings_without_turning_agreement_into_truth()
    {
        await Service.InitializeAsync(root, false); var task = await Service.CreateTaskAsync(root, "Review risky change", RiskLevel.High); var claim = await Service.CreateClaimAsync(root, "Authorization is correct", RiskLevel.High);
        var finding = await Service.CreateFindingAsync(root, "Missing tenant test", "No tenant-isolation regression test was found.", RiskLevel.High, task.Id, "tests/AuthTests.cs");
        var agreeing = await Service.RecordReviewAsync(root, claim.Id, "claude", ReviewVerdict.Agree, "Implementation appears consistent.", []);
        Assert.Equal(ReviewVerdict.Agree, agreeing.Verdict); Assert.Equal(ClaimStatus.Unverified, (await Service.GetClaimAsync(root, claim.Id))!.Status);
        await Service.RecordReviewAsync(root, claim.Id, "codex", ReviewVerdict.Disagree, "Required test is missing.", [finding.Id]);
        Assert.Equal(ClaimStatus.Disputed, (await Service.GetClaimAsync(root, claim.Id))!.Status);
        Assert.Equal(WorkStatus.Completed, (await Service.ResolveFindingAsync(root, finding.Id)).Status);
    }

    [Fact]
    public void Canonical_enums_write_upper_snake_case_and_read_legacy_values()
    {
        var current = System.Text.Json.JsonSerializer.Serialize(WorkStatus.InProgress, JsonDefaults.Options); Assert.Equal("\"IN_PROGRESS\"", current);
        var legacy = System.Text.Json.JsonSerializer.Deserialize<WorkStatus>("\"InProgress\"", JsonDefaults.Options); Assert.Equal(WorkStatus.InProgress, legacy);
        var snake = System.Text.Json.JsonSerializer.Deserialize<ClaimStatus>("\"PARTIALLY_VERIFIED\"", JsonDefaults.Options); Assert.Equal(ClaimStatus.PartiallyVerified, snake);
    }

    private void RunGit(string arguments) { using var process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true }); process!.WaitForExit(); Assert.Equal(0, process.ExitCode); }
    public void Dispose() { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
