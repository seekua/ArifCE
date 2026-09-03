using System.Diagnostics;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

// Synthetic policy fixtures, never evidence that a real build/model run occurred.
public sealed class BenchmarkSafetyTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-safety-evaluator-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Secret_boundary_checks_provider_calls_and_persisted_response()
    {
        InitializeGit(root);
        var store = new CanonicalStore();
        var provider = new CountingProvider();
        var profile = new LlmProviderProfile("fixture", LlmProviderKind.Ollama, "fixture-model", null, null);
        var orchestrator = new LlmOrchestrator(new LlmRouter(new[] { ((ILlmProvider)provider, profile) }), store, new JournalStore(), new GitInspector());
        var clean = await orchestrator.ExecuteAsync(root, new LlmRequest("review", "Inspect this safe change"), "CLAIM-0001");
        Assert.Equal(1, provider.Calls);
        Assert.Contains("fixture response", clean.Route.Response.Text);
        var evidenceDirectory = Path.Combine(root, ".arifce", "evidence");
        Assert.Single(Directory.GetFiles(evidenceDirectory, "*.json"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ExecuteAsync(root, new LlmRequest("review", "password=hunter2"), "CLAIM-0001"));
        Assert.Equal(1, provider.Calls); // A provider call followed by an exception must fail this test.
        Assert.Single(Directory.GetFiles(evidenceDirectory, "*.json"));

        provider.ResponseText = "fixture response password=hunter2";
        provider.RawResponse = "raw password=hunter2";
        var secret = await orchestrator.ExecuteAsync(root, new LlmRequest("review", "Inspect another safe change"), "CLAIM-0001");
        Assert.Equal(2, provider.Calls);
        Assert.Contains("fixture response", secret.Route.Response.Text);
        Assert.DoesNotContain("hunter2", secret.Route.Response.Text);
        Assert.True(string.IsNullOrEmpty(secret.Route.Response.RawResponse));
        var persisted = await store.ReadAsync<EvidenceRecord>(root, "evidence", secret.Evidence.Id);
        Assert.NotNull(persisted);
        Assert.Contains("fixture response", persisted.Summary);
        Assert.DoesNotContain("hunter2", persisted.Summary);
        Assert.Equal(2, Directory.GetFiles(evidenceDirectory, "*.json").Length);
        foreach (var path in Directory.GetFiles(evidenceDirectory, "*.json"))
            Assert.DoesNotContain("hunter2", await File.ReadAllTextAsync(path));
        Assert.DoesNotContain("hunter2", await File.ReadAllTextAsync(Path.Combine(root, ".arifce", "journal", "events.jsonl")));
    }

    [Fact]
    public async Task High_risk_acceptance_checks_each_requirement_and_success()
    {
        foreach (var scenario in new[] { "complete", "missing-build", "missing-tests", "missing-review", "failed-build", "failed-tests", "disagreeing-review", "stale" })
        {
            var directory = Path.Combine(root, scenario);
            InitializeGit(directory);
            var store = new CanonicalStore();
            var git = new GitInspector();
            var service = new ProjectService(store, new JournalStore(), new IndexStore(), git);
            await service.InitializeAsync(directory, false);
            var claim = await service.CreateClaimAsync(directory, "Synthetic high-risk policy fixture", RiskLevel.High);
            var snapshot = await git.CaptureAsync(directory);
            var ids = new List<string>();
            if (scenario != "missing-build")
            {
                const string id = "EVIDENCE-0001";
                await store.WriteAsync(directory, "evidence", id, new EvidenceRecord(1, id, claim.Id, "BUILD", null, scenario == "failed-build" ? 1 : 0, "Synthetic build fixture", snapshot, DateTimeOffset.UtcNow));
                ids.Add(id);
            }
            if (scenario != "missing-tests")
            {
                const string id = "EVIDENCE-0002";
                await store.WriteAsync(directory, "evidence", id, new EvidenceRecord(1, id, claim.Id, "TEST_RUN", null, scenario == "failed-tests" ? 1 : 0, "Synthetic test fixture", snapshot, DateTimeOffset.UtcNow));
                ids.Add(id);
            }
            await store.WriteAsync(directory, "claims", claim.Id, claim with { Status = ClaimStatus.Supported, Evidence = ids });
            if (scenario != "missing-review")
                await store.WriteAsync(directory, "reviews", "REVIEW-0001", new ReviewRecord(1, "REVIEW-0001", claim.Id, "fixture-reviewer", scenario == "disagreeing-review" ? ReviewVerdict.Disagree : ReviewVerdict.Agree, "Synthetic review fixture", [], snapshot, DateTimeOffset.UtcNow));
            if (scenario == "stale") await File.WriteAllTextAsync(Path.Combine(directory, "changed.cs"), "changed after evidence");

            if (scenario == "complete")
            {
                var acceptance = await service.CreateAcceptanceAsync(directory, claim.Id, "fixture-owner", "All fixture requirements satisfied");
                var persisted = await service.GetAcceptanceAsync(directory, acceptance.Id);
                Assert.NotNull(persisted);
                Assert.Equal(AcceptanceStatus.Accepted, persisted.Status);
                Assert.Equal(claim.Id, persisted.ClaimId);
            }
            else
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAcceptanceAsync(directory, claim.Id, "fixture-owner", scenario));
                var acceptanceDirectory = Path.Combine(directory, ".arifce", "acceptances");
                Assert.True(!Directory.Exists(acceptanceDirectory) || !Directory.EnumerateFiles(acceptanceDirectory, "*.json").Any(), scenario);
                Assert.NotNull(await service.GetClaimAsync(directory, claim.Id));
            }
        }
    }

    private static void InitializeGit(string directory)
    {
        Directory.CreateDirectory(directory);
        using var process = Process.Start(new ProcessStartInfo("git", "init --quiet") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true });
        process!.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    private sealed class CountingProvider : ILlmProvider
    {
        public int Calls { get; private set; }
        public string ResponseText { get; set; } = "fixture response";
        public string RawResponse { get; set; } = "raw fixture response";
        public string ProviderId => "fixture";
        public LlmProviderKind Kind => LlmProviderKind.Ollama;
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new LlmResponse(ProviderId, "fixture-model", ResponseText, new LlmUsage(3, 2), TimeSpan.Zero, RawResponse));
        }
        public Task<LlmConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(new LlmConnectionResult(ProviderId, true, "fixture", TimeSpan.Zero));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
