using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

public sealed class BenchmarkFlightRecorderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-flight-recorder-benchmark-" + Guid.NewGuid().ToString("N"));
    private readonly ProjectService service = new(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());

    public BenchmarkFlightRecorderTests()
    {
        Directory.CreateDirectory(root);
        using var process = Process.Start(new ProcessStartInfo("git", "init") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true });
        process!.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Unable to initialize the evaluator Git repository.");
    }

    [Fact]
    public async Task Recorder_redacts_every_persisted_text_field_and_promotes_failed_attempts()
    {
        await service.InitializeAsync(root, false);
        var task = await service.CreateTaskAsync(root, "Repair payment regression", RiskLevel.High);
        var run = await service.StartAgentRunAsync(
            root,
            "OpenAI api_key=provider-secret",
            "builder password=agent-secret",
            "Inspect Authorization: Bearer goal-secret",
            task.Id);

        Assert.DoesNotContain("provider-secret", run.Provider);
        Assert.DoesNotContain("agent-secret", run.Agent);
        Assert.DoesNotContain("goal-secret", run.Goal);
        await service.RecordAgentRunStepAsync(root, run.Id, AgentStepKind.Investigation, new string('i', 1200));
        await service.RecordAgentRunStepAsync(root, run.Id, AgentStepKind.Evidence, "Observed deterministic test output");
        await service.RecordAgentRunStepAsync(root, run.Id, AgentStepKind.Decision, "Preserve public rounding behavior");
        var updated = await service.RecordAgentRunStepAsync(
            root,
            run.Id,
            AgentStepKind.Attempt,
            "Tried cached totals password=summary-secret",
            "FAILED secret=outcome-secret",
            1,
            ["CLAIM-0001", "claim-0001"]);

        Assert.Equal([AgentStepKind.Investigation, AgentStepKind.Evidence, AgentStepKind.Decision, AgentStepKind.Attempt], updated.Steps.Select(step => step.Kind));
        Assert.Equal(1001, updated.Steps[0].Summary.Length);
        Assert.DoesNotContain("summary-secret", updated.Steps[^1].Summary);
        Assert.DoesNotContain("outcome-secret", updated.Steps[^1].Outcome);
        Assert.Single(updated.Steps[^1].RelatedIds, id => id.Equals("CLAIM-0001", StringComparison.OrdinalIgnoreCase));
        var attemptId = Assert.Single(updated.Steps[^1].RelatedIds, id => id.StartsWith("ATTEMPT-", StringComparison.Ordinal));
        var attempt = await service.GetAttemptAsync(root, attemptId);
        Assert.NotNull(attempt);
        Assert.Equal(task.Id, attempt.TaskId);
        Assert.Contains("cached totals", attempt.Approach);

        var persistedText = await PersistedRunAndJournalText(run.Id);
        foreach (var secret in new[] { "provider-secret", "agent-secret", "goal-secret", "summary-secret", "outcome-secret" })
            Assert.DoesNotContain(secret, persistedText, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", persistedText, StringComparison.Ordinal);

        var finished = await service.FinishAgentRunAsync(root, run.Id, "Fallback implementation passed", true);
        Assert.Equal(AgentRunStatus.Completed, finished.Status);
        Assert.NotNull(finished.CompletedAtUtc);
        Assert.Equal(AgentStepKind.Result, finished.Steps[^1].Kind);
        Assert.Equal("PASSED", finished.Steps[^1].Outcome);
        var handoff = await service.HandoffAsync(root);
        Assert.Contains(attemptId, handoff.Markdown);
        Assert.DoesNotContain("summary-secret", handoff.Markdown);
    }

    [Fact]
    public async Task Recorder_enforces_terminal_transitions_and_rejects_invalid_links_without_writes()
    {
        await service.InitializeAsync(root, false);
        var before = CanonicalHashes();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAgentRunAsync(root, "codex", "builder", "Missing task", "TASK-9999"));
        Assert.Equal(before, CanonicalHashes());

        var completed = await service.StartAgentRunAsync(root, "codex", "builder", "Complete a change");
        var failed = await service.StartAgentRunAsync(root, "claude", "reviewer", "Review a change");
        var invalidLink = await service.StartAgentRunAsync(root, "gemini", "analyst", "Reject unsafe links");
        var linkHashes = CanonicalHashes();
        await Assert.ThrowsAsync<ArgumentException>(() => service.RecordAgentRunStepAsync(root, invalidLink.Id, AgentStepKind.Evidence, "Unsafe link", relatedIds: ["password=link-secret"]));
        Assert.Equal(linkHashes, CanonicalHashes());
        var completedResult = await service.FinishAgentRunAsync(root, completed.Id, "Done", true);
        var failedResult = await service.FinishAgentRunAsync(root, failed.Id, "Rejected", false);
        Assert.Equal(AgentRunStatus.Completed, completedResult.Status);
        Assert.Equal(AgentRunStatus.Failed, failedResult.Status);
        Assert.Equal(0, completedResult.Steps[^1].ExitCode);
        Assert.Equal(1, failedResult.Steps[^1].ExitCode);
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(root, ".arifce", "attempts"), "*.json"));

        var terminalHashes = CanonicalHashes();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAgentRunStepAsync(root, completed.Id, AgentStepKind.Evidence, "Late evidence"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinishAgentRunAsync(root, failed.Id, "Second finish", false));
        Assert.Equal(terminalHashes, CanonicalHashes());
    }

    [Fact]
    public async Task Recorder_reserves_a_terminal_step_and_enforces_a_finite_step_budget()
    {
        await service.InitializeAsync(root, false);
        var run = await service.StartAgentRunAsync(root, "codex", "builder", "Bounded structured capture");
        for (var index = 0; index < 63; index++)
            await service.RecordAgentRunStepAsync(root, run.Id, AgentStepKind.Investigation, $"Structured observation {index + 1}");

        var before = CanonicalHashes();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAgentRunStepAsync(root, run.Id, AgentStepKind.Evidence, "Would consume the terminal slot"));
        Assert.Equal(before, CanonicalHashes());
        var finished = await service.FinishAgentRunAsync(root, run.Id, "Bounded run complete", true);
        Assert.Equal(64, finished.Steps.Count);
        Assert.Equal(AgentStepKind.Result, finished.Steps[^1].Kind);
        Assert.True(new FileInfo(Path.Combine(root, ".arifce", "runs", run.Id.ToLowerInvariant() + ".json")).Length < 150_000);
    }

    private async Task<string> PersistedRunAndJournalText(string runId)
    {
        var run = await File.ReadAllTextAsync(Path.Combine(root, ".arifce", "runs", runId.ToLowerInvariant() + ".json"));
        var journal = await File.ReadAllTextAsync(Path.Combine(root, ".arifce", "journal", "events.jsonl"));
        return run + journal;
    }

    private Dictionary<string, string> CanonicalHashes() => Directory
        .EnumerateFiles(Path.Combine(root, ".arifce"), "*", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}index{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(path => Path.GetRelativePath(root, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
