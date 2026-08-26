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
    }

    [Fact]
    public async Task Claim_verification_and_git_freshness_are_snapshot_scoped()
    {
        await Service.InitializeAsync(root, false); var claim = await Service.CreateClaimAsync(root, "Command succeeds", RiskLevel.Low); var result = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true");
        Assert.Equal(ClaimStatus.Verified, result.Claim.Status); Assert.Equal(0, result.Evidence.ExitCode);
        await File.WriteAllTextAsync(Path.Combine(root, "changed.txt"), "change"); var after = await git.CaptureAsync(root); Assert.NotEqual(result.Evidence.Snapshot.Digest, after.Digest);
        Assert.Equal(EvidenceFreshness.Stale, EvidenceEvaluator.Evaluate(result.Evidence.Snapshot, after));
    }

    [Fact]
    public async Task Handoff_is_semantic_and_refactor_finish_is_guarded()
    {
        await Service.InitializeAsync(root, false); await Service.CreateTaskAsync(root, "Deliver continuity", RiskLevel.Medium); await Service.CheckpointAsync(root, "Core flow works");
        var handoff = await Service.HandoffAsync(root); Assert.Contains("Latest Checkpoint", handoff.Markdown); Assert.DoesNotContain("raw/", handoff.Markdown);
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
    public void Command_evidence_parser_extracts_localized_test_and_build_metrics()
    {
        var tests = CommandEvidenceParser.Parse("dotnet test", "Başarısız: 1, Başarılı: 7, Atlanan: 2, Toplam: 10");
        Assert.Equal("TEST_RUN", tests.Kind); Assert.Equal(10, tests.Metrics!.Total); Assert.Equal(7, tests.Metrics.Passed); Assert.Equal(1, tests.Metrics.Failed); Assert.Equal(2, tests.Metrics.Skipped);
        var build = CommandEvidenceParser.Parse("dotnet build", "Build succeeded.\n    3 Warning(s)\n    0 Error(s)");
        Assert.Equal("BUILD", build.Kind); Assert.Equal(3, build.Metrics!.Warnings); Assert.Equal(0, build.Metrics.Errors);
    }

    [Fact]
    public async Task Definition_of_done_core_flow_survives_index_deletion()
    {
        await Service.InitializeAsync(root, false);
        var task = await Service.CreateTaskAsync(root, "Exercise the continuity flow", RiskLevel.Low);
        await Service.CheckpointAsync(root, "Task represented and checkpointed");
        var claim = await Service.CreateClaimAsync(root, "A deterministic command succeeds", RiskLevel.Low);
        var verified = await Service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true");
        Assert.Equal(ClaimStatus.Verified, verified.Claim.Status);
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

    private void RunGit(string arguments) { using var process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true }); process!.WaitForExit(); Assert.Equal(0, process.ExitCode); }
    public void Dispose() { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}
