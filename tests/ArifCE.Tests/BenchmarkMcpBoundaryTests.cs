using System.Diagnostics;
using System.Security.Cryptography;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

public sealed class BenchmarkMcpBoundaryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-mcp-boundary-" + Guid.NewGuid().ToString("N"));

    public BenchmarkMcpBoundaryTests()
    {
        Directory.CreateDirectory(root);
        RunGit("init");
    }

    [Fact]
    public async Task Mcp_rejects_malformed_writes_before_canonical_side_effects()
    {
        var service = new ProjectService(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());
        await service.InitializeAsync(root, false);
        var before = CanonicalHashes();
        using var process = StartServer();
        var requests = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_task_create\",\"arguments\":{\"title\":\"bad risk\",\"risk\":\"Impossible\"}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_attempt_record\",\"arguments\":{\"taskId\":\"../../outside\",\"approach\":\"x\",\"result\":\"x\",\"reason\":\"x\"}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_checkpoint\",\"arguments\":{\"summary\":42}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_status\",\"arguments\":{\"unexpected\":true}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_finding_create\",\"arguments\":{\"title\":\"missing task\",\"description\":\"x\",\"taskId\":\"TASK-9999\"}}}"
        };
        foreach (var request in requests) await process.StandardInput.WriteLineAsync(request);
        await process.StandardInput.FlushAsync();
        var responses = await ReadResponses(process, requests.Length);
        Assert.All(responses, response => Assert.Contains("\"error\"", response, StringComparison.Ordinal));
        Assert.Contains("\"id\":1,\"error\":{\"code\":-32602", responses[0], StringComparison.Ordinal);
        Assert.Contains("\"id\":2,\"error\":{\"code\":-32602", responses[1], StringComparison.Ordinal);
        Assert.Contains("\"id\":3,\"error\":{\"code\":-32602", responses[2], StringComparison.Ordinal);
        Assert.Contains("\"id\":4,\"error\":{\"code\":-32602", responses[3], StringComparison.Ordinal);
        Assert.Contains("\"id\":5,\"error\":{\"code\":-32603", responses[4], StringComparison.Ordinal);
        Assert.Equal(before, CanonicalHashes());
    }

    [Fact]
    public async Task Mcp_rejects_oversize_and_duplicate_writes_without_creating_extra_records()
    {
        var service = new ProjectService(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());
        await service.InitializeAsync(root, false);
        using (var oversized = StartServer())
        {
            await oversized.StandardInput.WriteLineAsync(new string('x', 262_145));
            await oversized.StandardInput.FlushAsync();
            var response = await oversized.StandardOutput.ReadLineAsync();
            Assert.Contains("\"code\":-32600", response, StringComparison.Ordinal);
            Assert.Contains("Request exceeds", response, StringComparison.Ordinal);
        }
        var beforeWrite = CanonicalHashes();
        Assert.Equal(beforeWrite, CanonicalHashes());

        using var process = StartServer();
        const string first = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_decision_create\",\"arguments\":{\"title\":\"Keep canonical memory\",\"decision\":\"Store durable records in the repository\"}}}";
        const string duplicate = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_decision_create\",\"arguments\":{\"title\":\"Keep canonical memory\",\"decision\":\"Store durable records in the repository\"}}}";
        await process.StandardInput.WriteLineAsync(first);
        await process.StandardInput.FlushAsync();
        var created = await process.StandardOutput.ReadLineAsync();
        Assert.Contains("ADR-0001", created, StringComparison.Ordinal);
        var afterFirst = CanonicalHashes();
        await process.StandardInput.WriteLineAsync(duplicate);
        await process.StandardInput.FlushAsync();
        var rejected = await process.StandardOutput.ReadLineAsync();
        Assert.Contains("\"code\":-32603", rejected, StringComparison.Ordinal);
        Assert.Contains("duplicates active decision", rejected, StringComparison.Ordinal);
        Assert.Equal(afterFirst, CanonicalHashes());
        Assert.Single(Directory.EnumerateFiles(Path.Combine(root, ".arifce", "decisions"), "*.json"));
    }

    [Fact]
    public async Task Mcp_valid_writes_follow_canonical_domain_rules()
    {
        using var process = StartServer();
        const string taskRequest = "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_task_create\",\"arguments\":{\"title\":\"Repair a regression\",\"risk\":\"High\"}}}";
        await process.StandardInput.WriteLineAsync(taskRequest);
        await process.StandardInput.FlushAsync();
        var taskResponse = await process.StandardOutput.ReadLineAsync();
        Assert.Contains("TASK-0001", taskResponse, StringComparison.Ordinal);
        const string attemptRequest = "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_attempt_record\",\"arguments\":{\"taskId\":\"TASK-0001\",\"approach\":\"cached total\",\"result\":\"FAILED\",\"reason\":\"rounding changed\"}}}";
        await process.StandardInput.WriteLineAsync(attemptRequest);
        await process.StandardInput.FlushAsync();
        var attemptResponse = await process.StandardOutput.ReadLineAsync();
        Assert.Contains("ATTEMPT-0001", attemptResponse, StringComparison.Ordinal);

        var store = new CanonicalStore();
        var task = await store.ReadAsync<TaskRecord>(root, "tasks", "TASK-0001");
        var attempt = await store.ReadAsync<AttemptRecord>(root, "attempts", "ATTEMPT-0001");
        Assert.Equal(RiskLevel.High, task!.Risk);
        Assert.Equal(task.Id, attempt!.TaskId);
        Assert.Contains("attempt.recorded", await File.ReadAllTextAsync(Path.Combine(root, ".arifce", "journal", "events.jsonl")), StringComparison.Ordinal);
    }

    private Process StartServer()
    {
        var server = Path.Combine(AppContext.BaseDirectory, "ArifCE.Mcp.dll");
        var start = new ProcessStartInfo("dotnet", $"\"{server}\"") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.Environment["ARIFCE_PROJECT_ROOT"] = root;
        return Process.Start(start)!;
    }

    private static async Task<IReadOnlyList<string>> ReadResponses(Process process, int count)
    {
        var responses = new List<string>();
        for (var index = 0; index < count; index++) responses.Add((await process.StandardOutput.ReadLineAsync())!);
        return responses;
    }

    private Dictionary<string, string> CanonicalHashes() => Directory
        .EnumerateFiles(Path.Combine(root, ".arifce"), "*", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}index{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(path => Path.GetRelativePath(root, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.OrdinalIgnoreCase);

    private void RunGit(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true });
        process!.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Unable to initialize the evaluator Git repository.");
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
