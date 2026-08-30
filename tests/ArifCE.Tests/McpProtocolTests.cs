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

}
