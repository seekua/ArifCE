using System.Diagnostics;
using Xunit;

namespace ArifCE.Tests;

public sealed class McpProtocolTests
{
    [Fact]
    public async Task Initialize_and_tools_list_are_valid_json_rpc_responses()
    {
        var server = Path.Combine(AppContext.BaseDirectory, "ArifCE.Mcp.dll");
        Assert.True(File.Exists(server), $"MCP server output was not copied to {server}");
        using var process = Process.Start(new ProcessStartInfo("dotnet", $"\"{server}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{}}");
        await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\",\"params\":{}}");
        await process.StandardInput.FlushAsync();
        var lines = new List<string>();
        for (var i = 0; i < 4 && lines.Count < 2; i++)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (line is not null && line.Contains("\"result\"", StringComparison.Ordinal)) lines.Add(line);
        }
        process.Kill();
        Assert.Contains(lines, line => line.Contains("\"protocolVersion\":\"2025-03-26\"", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("arifce_status", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("arifce_handoff", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("arifce_refactor_verify", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("arifce_context", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("arifce_llm_review", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tool_calls_reject_invalid_enums_unknown_arguments_and_path_like_ids()
    {
        var root = Directory.CreateTempSubdirectory("arifce-mcp-validation-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, ".git"));
            using var process = StartServer(root.FullName);
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_task_create\",\"arguments\":{\"title\":\"unsafe fallback\",\"risk\":\"impossible\"}}}");
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_refactor_status\",\"arguments\":{\"id\":\"../../outside\"}}}");
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{\"name\":\"arifce_status\",\"arguments\":{\"unexpected\":true}}}");
            await process.StandardInput.FlushAsync();
            var responses = new[] { await process.StandardOutput.ReadLineAsync(), await process.StandardOutput.ReadLineAsync(), await process.StandardOutput.ReadLineAsync() };
            Assert.All(responses, response => Assert.Contains("\"code\":-32602", response));
            Assert.Contains(responses, response => response!.Contains("Invalid risk value", StringComparison.Ordinal));
            Assert.Contains(responses, response => response!.Contains("valid repository entity ID", StringComparison.Ordinal));
            Assert.Contains(responses, response => response!.Contains("Unknown argument", StringComparison.Ordinal));
            process.Kill();
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Oversized_mcp_request_is_rejected_before_json_processing()
    {
        using var process = StartServer();
        await process.StandardInput.WriteLineAsync(new string('x', 262_145));
        await process.StandardInput.FlushAsync();
        var response = await process.StandardOutput.ReadLineAsync();
        Assert.Contains("\"code\":-32600", response);
        Assert.Contains("Request exceeds", response);
        process.Kill();
    }

    private static Process StartServer(string? root = null)
    {
        var server = Path.Combine(AppContext.BaseDirectory, "ArifCE.Mcp.dll");
        var start = new ProcessStartInfo("dotnet", $"\"{server}\"") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        if (root is not null) start.Environment["ARIFCE_PROJECT_ROOT"] = root;
        return Process.Start(start)!;
    }

}
