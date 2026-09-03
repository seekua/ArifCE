using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

// Uses the installed test runner as a child-process host; no production worker API.
public sealed class BenchmarkStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-storage-evaluator-" + Guid.NewGuid().ToString("N"));
    private const string WorkerRoot = "ARIFCE_STORAGE_TEST_ROOT";
    private const string WorkerRole = "ARIFCE_STORAGE_TEST_ROLE";

    [Fact]
    public async Task Separate_processes_preserve_claim_links_and_distinct_ids()
    {
        var service = Initialize();
        await service.InitializeAsync(root, false);
        var claim = await service.CreateClaimAsync(root, "Process contention fixture", RiskLevel.Medium);
        await File.WriteAllTextAsync(Path.Combine(root, "claim-id"), claim.Id);
        var processes = new List<Process>();
        var output = new List<Task<string>>();
        try
        {
            StartWorker("A");
            await WaitFor("entered-A"); // A holds the read/update/write lock until explicitly released.
            StartWorker("B");
            StartWorker("C");
            await WaitFor("ready-B");
            await WaitFor("ready-C");
            await File.WriteAllTextAsync(Path.Combine(root, "go"), "go");
            await WaitFor("attempting-B");
            await WaitFor("attempting-C");
            await Task.Delay(750);
            Assert.False(File.Exists(Path.Combine(root, "entered-B")), "B entered while A held the mutation lock.");
            Assert.False(File.Exists(Path.Combine(root, "entered-C")), "C entered while A held the mutation lock.");
            await File.WriteAllTextAsync(Path.Combine(root, "release-A"), "release");
            foreach (var process in processes)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                await process.WaitForExitAsync(timeout.Token);
                Assert.Equal(0, process.ExitCode);
            }
            var pids = new List<int>();
            foreach (var role in new[] { "A", "B", "C" })
            {
                Assert.True(File.Exists(Path.Combine(root, "done-" + role)));
                pids.Add(int.Parse(await File.ReadAllTextAsync(Path.Combine(root, "ready-" + role))));
            }
            Assert.Equal(3, pids.Distinct().Count()); // Actual test-host PIDs, not launcher IDs or Task IDs.
            Assert.DoesNotContain(Environment.ProcessId, pids);
            var updated = await service.GetClaimAsync(root, claim.Id);
            Assert.NotNull(updated);
            Assert.Equal(new[] { "EVIDENCE-A", "EVIDENCE-B", "EVIDENCE-C" }, updated.Evidence.Order(StringComparer.Ordinal));
            var store = new CanonicalStore();
            var records = Directory.GetFiles(Path.Combine(root, ".arifce", "tasks"), "*.json");
            Assert.Equal(30, records.Length);
            var titles = new List<string>();
            foreach (var path in records)
            {
                var record = await store.ReadAsync<TaskRecord>(root, "tasks", Path.GetFileNameWithoutExtension(path));
                Assert.NotNull(record);
                Assert.Equal(record.Id.ToLowerInvariant(), Path.GetFileNameWithoutExtension(path));
                titles.Add(record.Title);
            }
            Assert.Equal(30, titles.Distinct(StringComparer.Ordinal).Count());
            Assert.Empty(Directory.GetFiles(Path.Combine(root, ".arifce", "tasks"), "*.reserve"));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, ".arifce"), "*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(root, "release-A"), "release");
            foreach (var process in processes)
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                process.Dispose();
            }
            await Task.WhenAll(output); // Drain both streams even on failure; prevent pipe deadlocks.
        }

        void StartWorker(string role)
        {
            var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("vstest");
            start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName=" + GetType().FullName + ".Storage_worker_process");
            start.ArgumentList.Add("/ResultsDirectory:" + Path.Combine(root, "worker-results-" + role));
            start.Environment[WorkerRoot] = root;
            start.Environment[WorkerRole] = role;
            var process = Process.Start(start)!;
            processes.Add(process);
            output.Add(process.StandardOutput.ReadToEndAsync());
            output.Add(process.StandardError.ReadToEndAsync());
        }
    }

    [Fact]
    public async Task Storage_worker_process()
    {
        // In a normal suite this is only a host entrypoint. Its work is asserted by the parent test.
        var directory = Environment.GetEnvironmentVariable(WorkerRoot);
        if (directory is null) return;
        var role = Environment.GetEnvironmentVariable(WorkerRole)!;
        Assert.Contains(role, new[] { "A", "B", "C" });
        var store = new CanonicalStore();
        var claimId = await File.ReadAllTextAsync(Path.Combine(directory, "claim-id"));
        await File.WriteAllTextAsync(Path.Combine(directory, "ready-" + role), Environment.ProcessId.ToString());
        if (role != "A") await WaitForFile(directory, "go");
        await File.WriteAllTextAsync(Path.Combine(directory, "attempting-" + role), "attempting");
        await store.UpdateAsync<ClaimRecord>(directory, "claims", claimId, current =>
        {
            File.WriteAllText(Path.Combine(directory, "entered-" + role), "entered");
            if (role == "A") WaitForFile(directory, "release-A").GetAwaiter().GetResult();
            return current with { Evidence = current.Evidence.Append("EVIDENCE-" + role).ToArray() };
        });
        for (var number = 0; number < 10; number++)
        {
            var id = store.NextId(directory, "tasks", "TASK");
            await store.WriteAsync(directory, "tasks", id, new TaskRecord(1, id, $"{role}-{number}", null, WorkStatus.Open, RiskLevel.Low, DateTimeOffset.UtcNow));
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "done-" + role), "done");
    }

    [Fact]
    public async Task Deleted_index_rebuild_preserves_canonical_bytes_and_retrieval()
    {
        var service = Initialize();
        await service.InitializeAsync(root, false);
        var task = await service.CreateTaskAsync(root, "amaranthtask", RiskLevel.Low);
        var decision = await service.CreateDecisionAsync(root, "amaranthdecision", "Keep canonical files authoritative", "Fixture rationale");
        var attempt = await service.RecordAttemptAsync(root, task.Id, "amaranthattempt", "FAILED", "Previously lost a link");
        var claim = await service.CreateClaimAsync(root, "amaranthclaim", RiskLevel.Medium);
        var store = new CanonicalStore();
        var snapshot = await new GitInspector().CaptureAsync(root);
        await store.WriteAsync(root, "evidence", "EVIDENCE-0001", new EvidenceRecord(1, "EVIDENCE-0001", claim.Id, "FIXTURE", null, null, "amaranthevidence", snapshot, DateTimeOffset.UtcNow));
        await store.UpdateAsync<ClaimRecord>(root, "claims", claim.Id, value => value with { Evidence = new[] { "EVIDENCE-0001" } });
        var index = new IndexStore();
        await index.RebuildAsync(root);
        var before = CanonicalHashes();
        var expected = new Dictionary<string, string>
        {
            ["amaranthtask"] = "tasks/" + task.Id.ToLowerInvariant() + ".json",
            ["amaranthdecision"] = "decisions/" + decision.Id.ToLowerInvariant() + ".json",
            ["amaranthattempt"] = "attempts/" + attempt.Id.ToLowerInvariant() + ".json",
            ["amaranthclaim"] = "claims/" + claim.Id.ToLowerInvariant() + ".json",
            ["amaranthevidence"] = "evidence/evidence-0001.json"
        };
        var prior = new Dictionary<string, (string Path, string Snippet, double Score)[]>();
        foreach (var (query, path) in expected)
        {
            var results = (await index.SearchAsync(root, query)).ToArray();
            Assert.Contains(results, item => item.Path == path);
            prior[query] = results;
        }
        Directory.Delete(Path.Combine(root, ".arifce", "index"), recursive: true);
        Assert.Equal(before, CanonicalHashes());
        await new IndexStore().RebuildAsync(root);
        foreach (var query in expected.Keys)
            Assert.Equal(prior[query], (await new IndexStore().SearchAsync(root, query)).ToArray());
        Assert.Equal(before, CanonicalHashes()); // Includes journal JSONL, not just indexed entities.
        Assert.Equal(new[] { "EVIDENCE-0001" }, (await service.GetClaimAsync(root, claim.Id))!.Evidence);
        Assert.Equal("FAILED", (await store.ReadAsync<AttemptRecord>(root, "attempts", attempt.Id))!.Result);
    }

    private ProjectService Initialize()
    {
        Directory.CreateDirectory(root);
        using var git = Process.Start(new ProcessStartInfo("git", "init") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true });
        git!.WaitForExit();
        Assert.Equal(0, git.ExitCode);
        return new(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());
    }

    private string[] CanonicalHashes() => Directory.EnumerateFiles(Path.Combine(root, ".arifce"), "*", SearchOption.AllDirectories)
        .Select(path => (Path: path, Relative: Path.GetRelativePath(Path.Combine(root, ".arifce"), path).Replace('\\', '/')))
        .Where(item => !item.Relative.StartsWith("index/", StringComparison.Ordinal) && !item.Relative.StartsWith("cache/", StringComparison.Ordinal))
        .Select(item => item.Relative + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(item.Path))))
        .Order(StringComparer.Ordinal).ToArray();

    private Task WaitFor(string marker) => WaitForFile(root, marker);
    private static async Task WaitForFile(string directory, string marker)
    {
        var timeout = Stopwatch.StartNew();
        while (!File.Exists(Path.Combine(directory, marker)))
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(45)) throw new TimeoutException("Worker barrier timed out: " + marker);
            await Task.Delay(25);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }
}
