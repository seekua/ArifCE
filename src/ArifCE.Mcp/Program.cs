using System.Text.Json;
using ArifCE.Infrastructure;

var server = new McpServer();
await server.RunAsync(Console.In, Console.Out);

internal sealed class McpServer
{
    private readonly ProjectLocator locator = new();
    private readonly CanonicalStore canonical = new();
    private readonly JournalStore journal = new();
    private readonly IndexStore index = new();
    private readonly GitInspector git = new();

    public async Task RunAsync(TextReader input, TextWriter output)
    {
        string? line;
        while ((line = await input.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            await HandleAsync(line, output);
        }
    }

    private async Task HandleAsync(string line, TextWriter output)
    {
        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var method = root.TryGetProperty("method", out var methodValue) ? methodValue.GetString() : null;
            var id = root.TryGetProperty("id", out var idValue) ? idValue.Clone() : (JsonElement?)null;
            if (method is "notifications/initialized" or "notifications/cancelled") return;
            object result = method switch
            {
                "initialize" => Initialize(),
                "ping" => new { },
                "tools/list" => Tools(),
                "tools/call" => await CallToolAsync(root.GetProperty("params")),
                _ => throw new McpException(-32601, $"Method not found: {method}")
            };
            await WriteAsync(output, new { jsonrpc = "2.0", id, result });
        }
        catch (JsonException exception) { await WriteAsync(output, Error(null, -32700, $"Parse error: {exception.Message}")); }
        catch (McpException exception)
        {
            var id = document?.RootElement.TryGetProperty("id", out var value) == true ? value.Clone() : (JsonElement?)null;
            await WriteAsync(output, Error(id, exception.Code, exception.Message));
        }
        catch (Exception exception)
        {
            var id = document?.RootElement.TryGetProperty("id", out var value) == true ? value.Clone() : (JsonElement?)null;
            await WriteAsync(output, Error(id, -32603, exception.Message));
        }
        finally { document?.Dispose(); }
    }

    private object Initialize() => new
    {
        protocolVersion = "2025-03-26",
        capabilities = new { tools = new { listChanged = false } },
        serverInfo = new { name = "arifce", version = "0.3.0" }
    };

    private static object Tools() => new
    {
        tools = new object[]
        {
            Tool("arifce_status", "Read the current project status and Git snapshot.", new { type = "object", properties = new { } }),
            Tool("arifce_search", "Search indexed project intelligence using explainable lexical matching.", new { type = "object", required = new[] { "query" }, properties = new { query = new { type = "string" }, limit = new { type = "integer", minimum = 1, maximum = 50 } } }),
            Tool("arifce_checkpoint", "Record a project checkpoint with an explicit summary.", new { type = "object", required = new[] { "summary" }, properties = new { summary = new { type = "string", minLength = 1 } } }),
            Tool("arifce_handoff", "Create a semantic handoff from the current project state.", new { type = "object", properties = new { } })
            ,Tool("arifce_refactor_status", "Read a refactor campaign record.", new { type = "object", required = new[] { "id" }, properties = new { id = new { type = "string" } } })
            ,Tool("arifce_refactor_verify", "Run deterministic guards for a refactor campaign without finishing it.", new { type = "object", required = new[] { "id" }, properties = new { id = new { type = "string" } } })
        }
    };

    private async Task<object> CallToolAsync(JsonElement parameters)
    {
        var name = parameters.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
        var arguments = parameters.TryGetProperty("arguments", out var args) ? args : default;
        var root = ProjectRoot();
        var service = new ProjectService(canonical, journal, index, git);
        var text = name switch
        {
            "arifce_status" => await service.StatusAsync(root),
            "arifce_search" => await SearchAsync(root, arguments),
            "arifce_checkpoint" => (await service.CheckpointAsync(root, Required(arguments, "summary"))).Id,
            "arifce_handoff" => (await service.HandoffAsync(root)).Markdown,
            "arifce_refactor_status" => await RefactorStatusAsync(root, arguments),
            "arifce_refactor_verify" => await RefactorVerifyAsync(service, root, arguments),
            _ => throw new McpException(-32602, $"Unknown tool: {name}")
        };
        return new { content = new[] { new { type = "text", text } }, isError = false };
    }

    private async Task<string> RefactorStatusAsync(string root, JsonElement arguments)
    {
        var item = await canonical.ReadAsync<ArifCE.Core.RefactorCampaign>(root, "refactors", Required(arguments, "id")) ?? throw new McpException(-32602, "Refactor not found.");
        return JsonSerializer.Serialize(item);
    }

    private async Task<string> RefactorVerifyAsync(ProjectService service, string root, JsonElement arguments)
    {
        var failures = await service.VerifyRefactorAsync(root, Required(arguments, "id"));
        return failures.Count == 0 ? "All configured deterministic refactor guards pass." : string.Join(Environment.NewLine, failures);
    }

    private async Task<string> SearchAsync(string root, JsonElement arguments)
    {
        var query = Required(arguments, "query");
        var limit = arguments.TryGetProperty("limit", out var value) && value.TryGetInt32(out var parsed) ? Math.Clamp(parsed, 1, 50) : 20;
        var hits = await index.SearchAsync(root, query, limit);
        return string.Join(Environment.NewLine, hits.Select(x => $"{x.Path}\t{x.Score:F3}\t{x.Snippet.Replace(Environment.NewLine, " ")}"));
    }

    private string ProjectRoot() => locator.FindRoot(Environment.GetEnvironmentVariable("ARIFCE_PROJECT_ROOT") ?? Environment.CurrentDirectory);
    private static string Required(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.GetString()) ? property.GetString()! : throw new McpException(-32602, $"Missing required argument: {name}");
    private static object Tool(string name, string description, object schema) => new { name, description, inputSchema = schema };
    private static object Error(JsonElement? id, int code, string message) => new { jsonrpc = "2.0", id, error = new { code, message } };
    private static async Task WriteAsync(TextWriter output, object value) { await output.WriteLineAsync(JsonSerializer.Serialize(value)); await output.FlushAsync(); }
}

internal sealed class McpException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
