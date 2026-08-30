using System.Diagnostics;
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
        await Service.InitializeAsync(root, false); var claim = await Service.CreateClaimAsync(root, "Command succeeds", RiskLevel.Low); var result = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true");
        Assert.Equal(ClaimStatus.Supported, result.Claim.Status); Assert.Equal("UNVERIFIED_COMMAND", result.Evidence.Kind); Assert.Equal(0, result.Evidence.ExitCode);
        await File.WriteAllTextAsync(Path.Combine(root, "changed.txt"), "change"); var after = await git.CaptureAsync(root); Assert.NotEqual(result.Evidence.Snapshot.Digest, after.Digest);
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
    }

    [Fact]
    public async Task Acceptance_is_separate_and_requires_current_evidence()
    {
        await Service.InitializeAsync(root, false);
        var claim = await Service.CreateClaimAsync(root, "Deterministic command passes", RiskLevel.Low);
        var verified = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true");
        var acceptance = await Service.CreateAcceptanceAsync(root, claim.Id, "product-owner", "Acceptance criteria reviewed");
        Assert.Equal(AcceptanceStatus.Accepted, acceptance.Status);
        Assert.Equal(claim.Id, acceptance.ClaimId);
        var revoked = await Service.RevokeAcceptanceAsync(root, acceptance.Id);
        Assert.Equal(AcceptanceStatus.Revoked, revoked.Status);
        Assert.Equal(ClaimStatus.Supported, verified.Claim.Status);
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
        Assert.Equal(ClaimStatus.Verified, passed.Claim.Status); Assert.Equal(0, passed.Evidence.ExitCode);
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
        Assert.Equal(ClaimStatus.Verified, result.Claim.Status); Assert.Equal("PUBLIC_API_SURFACE", result.Evidence.Kind); Assert.Equal(0, result.Evidence.ExitCode);
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
        Assert.Equal(ClaimStatus.Verified, result.Claim.Status); Assert.Equal("SQLITE_SCHEMA", result.Evidence.Kind); Assert.Equal(0, result.Evidence.ExitCode);
    }

    [Fact]
    public async Task Definition_of_done_core_flow_survives_index_deletion()
    {
        await Service.InitializeAsync(root, false);
        var task = await Service.CreateTaskAsync(root, "Exercise the continuity flow", RiskLevel.Low);
        await Service.CheckpointAsync(root, "Task represented and checkpointed");
        var claim = await Service.CreateClaimAsync(root, "A deterministic command succeeds", RiskLevel.Low);
        var verified = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true");
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
