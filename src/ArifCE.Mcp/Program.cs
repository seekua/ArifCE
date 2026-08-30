using System.Text.Json;
using ArifCE.Core;
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
            Tool("arifce_context", "Compose bounded repository context for an LLM task.", new { type = "object", required = new[] { "task" }, properties = new { task = new { type = "string" }, budget = new { type = "integer", minimum = 1, maximum = 20000 } } }),
            Tool("arifce_checkpoint", "Record a project checkpoint with an explicit summary.", new { type = "object", required = new[] { "summary" }, properties = new { summary = new { type = "string", minLength = 1 } } }),
            Tool("arifce_task_create", "Create a tracked task in the canonical project store.", new { type = "object", required = new[] { "title" }, properties = new { title = new { type = "string" }, risk = new { type = "string", @enum = new[] { "Low", "Medium", "High", "Critical" } } } }),
            Tool("arifce_decision_create", "Record a project decision and its historical rationale.", new { type = "object", required = new[] { "title", "decision" }, properties = new { title = new { type = "string" }, decision = new { type = "string" }, historicalRationale = new { type = "string" } } }),
            Tool("arifce_attempt_record", "Record a failed or rejected approach for an existing task.", new { type = "object", required = new[] { "taskId", "approach", "result", "reason" }, properties = new { taskId = new { type = "string" }, approach = new { type = "string" }, result = new { type = "string" }, reason = new { type = "string" } } }),
            Tool("arifce_claim_create", "Create an explicit claim requiring evidence.", new { type = "object", required = new[] { "statement" }, properties = new { statement = new { type = "string" }, risk = new { type = "string", @enum = new[] { "Low", "Medium", "High", "Critical" } } } }),
            Tool("arifce_finding_create", "Record an actionable project finding.", new { type = "object", required = new[] { "title", "description" }, properties = new { title = new { type = "string" }, description = new { type = "string" }, severity = new { type = "string", @enum = new[] { "Low", "Medium", "High", "Critical" } }, taskId = new { type = "string" }, path = new { type = "string" } } }),
            Tool("arifce_review_record", "Record a review verdict for an existing claim.", new { type = "object", required = new[] { "claimId", "reviewer", "verdict", "summary" }, properties = new { claimId = new { type = "string" }, reviewer = new { type = "string" }, verdict = new { type = "string", @enum = new[] { "Agree", "PartiallyAgree", "Disagree", "Inconclusive" } }, summary = new { type = "string" } } }),
            Tool("arifce_acceptance_create", "Accept a supported or verified claim with explicit human rationale.", new { type = "object", required = new[] { "claimId", "actor", "rationale" }, properties = new { claimId = new { type = "string" }, actor = new { type = "string" }, rationale = new { type = "string" } } }),
            Tool("arifce_handoff", "Create a semantic handoff from the current project state.", new { type = "object", properties = new { } })
            ,Tool("arifce_llm_providers", "List locally configured LLM providers without exposing API keys.", new { type = "object", properties = new { } })
            ,Tool("arifce_llm_run", "Run a local LLM task and persist canonical evidence; explicit approval is required.", new { type = "object", required = new[] { "task", "prompt", "approved" }, properties = new { task = new { type = "string" }, prompt = new { type = "string" }, claimId = new { type = "string" }, approved = new { type = "boolean" } } })
            ,Tool("arifce_llm_review", "Run an approved local LLM reviewer for a claim and persist a review record.", new { type = "object", required = new[] { "claimId", "prompt", "reviewer", "rationale", "approved" }, properties = new { claimId = new { type = "string" }, prompt = new { type = "string" }, reviewer = new { type = "string" }, rationale = new { type = "string" }, approved = new { type = "boolean" } } })
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
            "arifce_context" => await ContextAsync(root, arguments),
            "arifce_checkpoint" => (await service.CheckpointAsync(root, Required(arguments, "summary"))).Id,
            "arifce_task_create" => (await service.CreateTaskAsync(root, Required(arguments, "title"), Enum(arguments, "risk", RiskLevel.Medium))).Id,
            "arifce_decision_create" => (await service.CreateDecisionAsync(root, Required(arguments, "title"), Required(arguments, "decision"), Optional(arguments, "historicalRationale"))).Id,
            "arifce_attempt_record" => (await service.RecordAttemptAsync(root, Required(arguments, "taskId"), Required(arguments, "approach"), Required(arguments, "result"), Required(arguments, "reason"))).Id,
            "arifce_claim_create" => (await service.CreateClaimAsync(root, Required(arguments, "statement"), Enum(arguments, "risk", RiskLevel.Medium))).Id,
            "arifce_finding_create" => (await service.CreateFindingAsync(root, Required(arguments, "title"), Required(arguments, "description"), Enum(arguments, "severity", RiskLevel.Medium), Optional(arguments, "taskId"), Optional(arguments, "path"))).Id,
            "arifce_review_record" => (await service.RecordReviewAsync(root, Required(arguments, "claimId"), Required(arguments, "reviewer"), Enum(arguments, "verdict", ReviewVerdict.Inconclusive), Required(arguments, "summary"), [])).Id,
            "arifce_acceptance_create" => (await service.CreateAcceptanceAsync(root, Required(arguments, "claimId"), Required(arguments, "actor"), Required(arguments, "rationale"))).Id,
            "arifce_handoff" => (await service.HandoffAsync(root)).Markdown,
            "arifce_llm_providers" => await LlmProvidersAsync(),
            "arifce_llm_run" => await LlmRunAsync(root, arguments),
            "arifce_llm_review" => await LlmReviewAsync(root, arguments),
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

    private static async Task<string> ContextAsync(string root, JsonElement arguments)
    {
        var task = Required(arguments, "task");
        var budget = arguments.TryGetProperty("budget", out var value) && value.TryGetInt32(out var parsed) ? Math.Clamp(parsed, 1, 20000) : 4000;
        var context = await new LlmContextComposer(new IndexStore()).ComposeAsync(root, task, budget);
        return JsonSerializer.Serialize(new { context.Task, context.Content, context.EstimatedTokens, context.Sources });
    }

    private static async Task<string> LlmProvidersAsync()
    {
        var profiles = await new LocalLlmSettingsStore().ListAsync();
        return JsonSerializer.Serialize(profiles.Select(p => new { p.Id, provider = p.Provider.ToString(), p.Model, p.Endpoint, p.Enabled }));
    }

    private static async Task<string> LlmRunAsync(string root, JsonElement arguments)
    {
        if (!arguments.TryGetProperty("approved", out var approved) || approved.ValueKind != JsonValueKind.True) throw new McpException(-32602, "Explicit approved=true is required for LLM execution.");
        var profiles = (await new LocalLlmSettingsStore().ListAsync()).Where(p => p.Enabled).ToList();
        if (profiles.Count == 0) throw new McpException(-32602, "No enabled local LLM providers are configured.");
        var router = new LlmRouter(profiles.Select(p => (LlmProviderFactory.Create(p), p)));
        var orchestrator = new LlmOrchestrator(router, new CanonicalStore(), new JournalStore(), new GitInspector());
        var task = Required(arguments, "task"); var prompt = Required(arguments, "prompt");
        var claim = arguments.TryGetProperty("claimId", out var claimValue) && claimValue.ValueKind == JsonValueKind.String ? claimValue.GetString()! : "CLAIM-UNASSIGNED";
        var result = await orchestrator.ExecuteAsync(root, new ArifCE.Core.LlmRequest(task, prompt), claim);
        return JsonSerializer.Serialize(new { provider = result.Route.Response.ProviderId, model = result.Route.Response.Model, tokens = result.Route.Response.Usage.TotalTokens, estimatedCost = result.Route.EstimatedCost, evidenceId = result.Evidence.Id, text = result.Route.Response.Text });
    }

    private static async Task<string> LlmReviewAsync(string root, JsonElement arguments)
    {
        if (!arguments.TryGetProperty("approved", out var approved) || approved.ValueKind != JsonValueKind.True) throw new McpException(-32602, "Explicit approved=true is required for reviewer execution.");
        var profiles = (await new LocalLlmSettingsStore().ListAsync()).Where(p => p.Enabled).ToList();
        if (profiles.Count == 0) throw new McpException(-32602, "No enabled local LLM providers are configured.");
        var workflow = new LlmReviewerWorkflow(new LlmOrchestrator(new LlmRouter(profiles.Select(p => (LlmProviderFactory.Create(p), p))), new CanonicalStore(), new JournalStore(), new GitInspector()), new CanonicalStore());
        var result = await workflow.RunAsync(root, Required(arguments, "claimId"), Required(arguments, "reviewer"), Required(arguments, "rationale"), Required(arguments, "prompt"), true);
        return JsonSerializer.Serialize(new { evidenceId = result.Execution.Evidence.Id, provider = result.Execution.Route.Response.ProviderId, model = result.Execution.Route.Response.Model, review = "recorded" });
    }

    private string ProjectRoot() => locator.FindRoot(Environment.GetEnvironmentVariable("ARIFCE_PROJECT_ROOT") ?? Environment.CurrentDirectory);
    private static string Required(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.GetString()) ? property.GetString()! : throw new McpException(-32602, $"Missing required argument: {name}");
    private static string? Optional(JsonElement value, string name) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    private static T Enum<T>(JsonElement value, string name, T fallback) where T : struct, System.Enum => Optional(value, name) is { } text && System.Enum.TryParse<T>(text, true, out var parsed) ? parsed : fallback;
    private static object Tool(string name, string description, object schema) => new { name, description, inputSchema = schema };
    private static object Error(JsonElement? id, int code, string message) => new { jsonrpc = "2.0", id, error = new { code, message } };
    private static async Task WriteAsync(TextWriter output, object value) { await output.WriteLineAsync(JsonSerializer.Serialize(value)); await output.FlushAsync(); }
}

internal sealed class McpException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
