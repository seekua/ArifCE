using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

public sealed class BenchmarkTaskContextTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-task-context-benchmark-" + Guid.NewGuid().ToString("N"));
    private readonly CanonicalStore canonical = new();
    private readonly IndexStore index = new();

    public BenchmarkTaskContextTests()
    {
        Directory.CreateDirectory(root);
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "init") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true });
        process!.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Unable to initialize evaluator Git repository.");
    }

    [Fact]
    public async Task Explicit_task_links_precede_lexical_results_and_foreign_links_are_rejected()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new GitSnapshot(null, "master", true, [], "fixture");
        await canonical.WriteAsync(root, "tasks", "TASK-0001", new TaskRecord(1, "TASK-0001", "Payment boundary", null, WorkStatus.Open, RiskLevel.Low, now, "Protect payment behavior", ["src"], ["Public behavior remains stable"], [new TaskCriterion("Tests pass", "TEST_RUN")]));
        await canonical.WriteAsync(root, "tasks", "TASK-0002", new TaskRecord(1, "TASK-0002", "Other payment work", null, WorkStatus.Open, RiskLevel.Low, now));
        await canonical.WriteAsync(root, "attempts", "ATTEMPT-0001", new AttemptRecord(1, "ATTEMPT-0001", "TASK-0001", "Use zebra cache", "failed", "Zebra repeats the race", [], now));
        await canonical.WriteAsync(root, "attempts", "ATTEMPT-0002", new AttemptRecord(1, "ATTEMPT-0002", "TASK-0002", "Foreign payment shortcut", "failed", "Other task only", [], now));
        await canonical.WriteAsync(root, "findings", "FINDING-0001", new FindingRecord(1, "FINDING-0001", "Quartz blocker", "Resolve quartz first", RiskLevel.High, WorkStatus.Open, "TASK-0001", "Quartz.cs", now));
        await canonical.WriteAsync(root, "claims", "CLAIM-0001", new ClaimRecord(1, "CLAIM-0001", "Amethyst behavior is preserved", ClaimStatus.Unverified, RiskLevel.Low, snapshot, [], now, "TASK-0001"));
        await canonical.WriteAsync(root, "claims", "CLAIM-0002", new ClaimRecord(1, "CLAIM-0002", "Foreign payment behavior", ClaimStatus.Unverified, RiskLevel.Low, snapshot, ["EVIDENCE-0002"], now, "TASK-0002"));
        await canonical.WriteAsync(root, "evidence", "EVIDENCE-0002", new EvidenceRecord(1, "EVIDENCE-0002", "CLAIM-0002", "TEST_RUN", "dotnet test", 0, "Foreign payment evidence", snapshot, now));
        await canonical.WriteAsync(root, "handoffs", "HANDOFF-0001", new HandoffRecord(1, "HANDOFF-0001", "# Handoff: TASK-0001\n\nNext action: inspect cobalt path.\n", snapshot, now));
        await canonical.WriteAsync(root, "handoffs", "HANDOFF-0002", new HandoffRecord(1, "HANDOFF-0002", "# Handoff: TASK-0002\n\nForeign payment handoff.\n", snapshot, now));

        var context = await new LlmContextComposer(index).ComposeForTaskAsync(root, "TASK-0001", 2000);

        Assert.Equal("TASK_CONTRACT", context.Items[0].Kind);
        Assert.Contains("Zebra repeats the race", context.Content);
        Assert.Contains("Quartz blocker", context.Content);
        Assert.Contains("Amethyst behavior is preserved", context.Content);
        Assert.Contains("inspect cobalt path", context.Content);
        Assert.DoesNotContain("Foreign payment shortcut", context.Content);
        Assert.DoesNotContain("Foreign payment evidence", context.Content);
        Assert.DoesNotContain("Foreign payment handoff", context.Content);
        Assert.Contains(context.Items, item => item.Path.EndsWith("attempt-0002.json", StringComparison.Ordinal) && !item.Included && item.Freshness == "OUT_OF_SCOPE" && string.IsNullOrEmpty(item.Snippet));
        Assert.Contains(context.Items, item => item.Path.EndsWith("evidence-0002.json", StringComparison.Ordinal) && !item.Included && item.Freshness == "OUT_OF_SCOPE" && string.IsNullOrEmpty(item.Snippet));
        Assert.Contains(context.Items, item => item.Path.EndsWith("handoff-0002.json", StringComparison.Ordinal) && !item.Included && item.Freshness == "OUT_OF_SCOPE" && string.IsNullOrEmpty(item.Snippet));
        Assert.Equal(context.Sources.Count, context.Sources.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.True(context.EstimatedTokens <= 2000);
        Assert.True(context.Telemetry.AssemblyMilliseconds >= 0);
    }

    [Fact]
    public async Task Task_context_budget_never_exposes_rejected_content_or_a_partial_contract()
    {
        var now = DateTimeOffset.UtcNow;
        await canonical.WriteAsync(root, "tasks", "TASK-0001", new TaskRecord(1, "TASK-0001", "Bounded task", null, WorkStatus.Open, RiskLevel.Low, now, "Preserve bounded behavior", ["src"], ["No partial contracts"], [new TaskCriterion("Tests pass", "TEST_RUN")]));
        await canonical.WriteAsync(root, "attempts", "ATTEMPT-0001", new AttemptRecord(1, "ATTEMPT-0001", "TASK-0001", new string('a', 2000), "failed", "SENSITIVE-REJECTED-CONTENT", [], now));

        var composer = new LlmContextComposer(index);
        var bounded = await composer.ComposeForTaskAsync(root, "TASK-0001", 120);
        Assert.True(bounded.EstimatedTokens <= 120);
        Assert.DoesNotContain("SENSITIVE-REJECTED-CONTENT", bounded.Content);
        Assert.All(bounded.Items.Where(item => !item.Included), item => Assert.Empty(item.Snippet));

        var impossible = await composer.ComposeForTaskAsync(root, "TASK-0001", 1);
        Assert.Empty(impossible.Content);
        Assert.Empty(impossible.Sources);
        Assert.All(impossible.Items, item => Assert.Empty(item.Snippet));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
