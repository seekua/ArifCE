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
        var initialize = await process.StandardOutput.ReadLineAsync();
        var tools = await process.StandardOutput.ReadLineAsync();
        process.Kill();
        Assert.Contains("\"protocolVersion\":\"2025-03-26\"", initialize);
        Assert.Contains("arifce_status", tools);
        Assert.Contains("arifce_handoff", tools);
    }
}
