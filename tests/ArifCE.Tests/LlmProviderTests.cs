using System.Net;
using System.Net.Http;
using System.Text.Json;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

public sealed class LlmProviderTests
{
    [Fact]
    public async Task Local_settings_round_trip_without_project_files()
    {
        var root = Directory.CreateTempSubdirectory("arifce-llm-");
        try
        {
            var store = new LocalLlmSettingsStore(Path.Combine(root.FullName, "settings.json"));
            await store.UpsertAsync(new LlmProviderProfile("local", LlmProviderKind.Ollama, "llama3", null, null));
            var items = await store.ListAsync();
            Assert.Single(items);
            Assert.Equal(LlmProviderKind.Ollama, items[0].Provider);
            await store.RemoveAsync("local");
            Assert.Empty(await store.ListAsync());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Provider_validation_rejects_cloud_profile_without_secret()
    {
        var errors = LlmProviderValidation.Validate(new LlmProviderProfile("cloud", LlmProviderKind.OpenAI, "gpt", null, null));
        Assert.Contains(errors, e => e.Contains("API key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenAi_compatible_adapter_parses_completion_and_usage()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":2}}") });
        var profile = new LlmProviderProfile("openai", LlmProviderKind.OpenAI, "gpt-test", "https://provider.test/v1", "secret");
        var response = await new OpenAiCompatibleProvider(profile, new HttpClient(handler)).CompleteAsync(new LlmRequest("review", "hello"));
        Assert.Equal("ok", response.Text);
        Assert.Equal(5, response.Usage.TotalTokens);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
    }

    [Fact]
    public async Task Anthropic_adapter_parses_usage()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"content\":[{\"text\":\"ok\"}],\"usage\":{\"input_tokens\":7,\"output_tokens\":4}}") });
        var response = await new AnthropicProvider(new LlmProviderProfile("anthropic", LlmProviderKind.Anthropic, "claude", "https://provider.test/v1", "secret"), new HttpClient(handler)).CompleteAsync(new LlmRequest("review", "hello"));
        Assert.Equal(11, response.Usage.TotalTokens);
    }

    [Fact]
    public async Task Gemini_adapter_parses_usage()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]}}],\"usageMetadata\":{\"promptTokenCount\":6,\"candidatesTokenCount\":3}}") });
        var response = await new GeminiProvider(new LlmProviderProfile("gemini", LlmProviderKind.Gemini, "gemini", "https://provider.test/v1beta", "secret"), new HttpClient(handler)).CompleteAsync(new LlmRequest("review", "hello"));
        Assert.Equal(9, response.Usage.TotalTokens);
    }

    [Fact]
    public async Task Router_falls_back_and_calculates_estimated_cost()
    {
        var failing = new StubProvider("first", true);
        var succeeding = new StubProvider("second", false);
        var profile = new LlmProviderProfile("second", LlmProviderKind.OpenAI, "model", null, null, true, 2m, 4m);
        var result = await new LlmRouter(new[] { (failing.Provider, failing.Profile), (succeeding.Provider, profile) }).CompleteAsync(new LlmRequest("summary", "hello"));
        Assert.Equal("done", result.Response.Text);
        Assert.Equal(new[] { "first", "second" }, result.AttemptedProviders);
        Assert.Equal(0.000014m, result.EstimatedCost);
    }

    [Fact]
    public async Task Router_prefers_task_selected_provider()
    {
        var first = new StubProvider("first", false); var preferred = new StubProvider("preferred", false);
        var result = await new LlmRouter(new[] { (first.Provider, first.Profile), (preferred.Provider, preferred.Profile) }).CompleteAsync(new LlmRequest("review", "hello"), "preferred");
        Assert.Equal("preferred", result.Response.ProviderId);
        Assert.Equal("preferred", result.AttemptedProviders[0]);
    }

    [Fact]
    public async Task Orchestrator_persists_llm_response_as_canonical_evidence()
    {
        var root = Directory.CreateTempSubdirectory("arifce-llm-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, ".git"));
            var provider = new StubProvider("local", false);
            var router = new LlmRouter(new[] { (provider.Provider, provider.Profile) });
            var orchestrator = new LlmOrchestrator(router, new CanonicalStore(), new JournalStore(), new GitInspector());
            var result = await orchestrator.ExecuteAsync(root.FullName, new LlmRequest("review", "hello"), "CLAIM-0001");
            Assert.Equal("llm-response", result.Evidence.Kind);
            Assert.True(File.Exists(Path.Combine(root.FullName, ".arifce", "evidence", result.Evidence.Id.ToLowerInvariant() + ".json")));
            Assert.Contains("local", result.Evidence.Summary);
            Assert.True(File.Exists(Path.Combine(root.FullName, ".arifce", "journal", "events.jsonl")));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Orchestrator_blocks_secret_prompts_before_provider_call()
    {
        var root = Directory.CreateTempSubdirectory("arifce-llm-secret-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, ".git"));
            var provider = new StubProvider("local", false);
            var orchestrator = new LlmOrchestrator(new LlmRouter(new[] { (provider.Provider, provider.Profile) }), new CanonicalStore(), new JournalStore(), new GitInspector());
            await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ExecuteAsync(root.FullName, new LlmRequest("review", "password=hunter2"), "CLAIM-0001"));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Local_policy_blocks_unapproved_provider_and_cost()
    {
        var engine = new LocalPolicyEngine(new[] { new ApprovalPolicy("default", "safe", true, 10, new[] { "local" }) });
        Assert.False(engine.Evaluate("cloud", 0, true).Allowed);
        Assert.False(engine.Evaluate("local", 0.11m, true).Allowed);
        Assert.True(engine.Evaluate("local", 0.01m, true).Allowed);
    }

    [Fact]
    public async Task Local_embedding_is_deterministic_and_selectable()
    {
        var selector = new EmbeddingProviderSelector(new[] { (IEmbeddingProvider)new DeterministicHashEmbeddingProvider(new EmbeddingProfile("local", "local")) });
        var provider = selector.Select("local");
        Assert.Equal(await provider.EmbedAsync("same"), await provider.EmbedAsync("same"), (IEqualityComparer<float>)EqualityComparer<float>.Default);
        Assert.Equal(128, provider.Dimensions);
    }

    [Fact]
    public async Task Local_a2a_passes_context_between_agents()
    {
        var flow = new LocalA2AOrchestrator(new[]
        {
            new A2AAgent("planner", "planner", (input, _) => Task.FromResult(input + " -> planned")),
            new A2AAgent("reviewer", "reviewer", (input, _) => Task.FromResult(input + " -> reviewed"))
        });
        var turns = await flow.RunAsync("task");
        Assert.Equal(2, turns.Count);
        Assert.EndsWith("reviewed", turns[^1].Output);
    }

    [Fact]
    public async Task Reviewer_workflow_requires_explicit_approval()
    {
        var root = Directory.CreateTempSubdirectory("arifce-review-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, ".git"));
            var provider = new StubProvider("local", false);
            var orchestrator = new LlmOrchestrator(new LlmRouter(new[] { (provider.Provider, provider.Profile) }), new CanonicalStore(), new JournalStore(), new GitInspector());
            var workflow = new LlmReviewerWorkflow(orchestrator, new CanonicalStore());
            await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.RunAsync(root.FullName, "CLAIM-0001", "reviewer", "rationale", "prompt", false));
            var result = await workflow.RunAsync(root.FullName, "CLAIM-0001", "reviewer", "rationale", "prompt", true);
            Assert.Equal("reviewer", result.Approval.Reviewer);
            Assert.Single(Directory.EnumerateFiles(Path.Combine(root.FullName, ".arifce", "reviews"), "*.json"));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public void Runtime_selector_switches_between_local_and_cloud_profiles()
    {
        var profiles = new[] { new LlmProviderProfile("local", LlmProviderKind.Ollama, "llama", null, null), new LlmProviderProfile("cloud", LlmProviderKind.OpenAI, "gpt", null, null) };
        Assert.Single(LlmRuntimeSelector.Select(profiles, LlmRuntimeMode.Local));
        Assert.Equal("cloud", LlmRuntimeSelector.Select(profiles, LlmRuntimeMode.Cloud)[0].Id);
    }

    [Fact]
    public async Task Benchmark_reports_similarity_latency_tokens_and_cost()
    {
        var provider = new StubProvider("local", false);
        var router = new LlmRouter(new[] { (provider.Provider, provider.Profile) });
        var results = await LlmBenchmark.RunAsync(new[] { new BenchmarkCase("case-1", "prompt", "done") }, async _ => await router.CompleteAsync(new LlmRequest("benchmark", "prompt")));
        Assert.Single(results);
        Assert.True(results[0].Passed);
        Assert.True(results[0].Tokens >= 0);
    }

    [Fact]
    public async Task Context_composer_enforces_budget_and_returns_sources()
    {
        var root = Directory.CreateTempSubdirectory("arifce-context-");
        try
        {
            Directory.CreateDirectory(Path.Combine(root.FullName, ".arifce", "decisions"));
            var decision = new DecisionRecord(1, "ADR-0001", "Database migration", "Use deterministic migration context", "Schema compatibility", "ACTIVE", "USER_CONFIRMED", null, DateTimeOffset.UnixEpoch);
            await File.WriteAllTextAsync(Path.Combine(root.FullName, ".arifce", "decisions", "adr-0001.json"), JsonSerializer.Serialize(decision, JsonDefaults.Options));
            var index = new IndexStore(); await index.RebuildAsync(root.FullName);
            var context = await new LlmContextComposer(index).ComposeAsync(root.FullName, "database migration", 250);
            Assert.Contains("adr-0001.json", context.Sources[0]); Assert.True(context.EstimatedTokens <= 250);
            var selected = Assert.Single(context.Items, item => item.Included);
            Assert.Equal("CURRENT", selected.Freshness);
            Assert.Contains("fits token budget", selected.Reason);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Context_assembly_explains_trust_and_budget_rejections_deterministically()
    {
        var root = Directory.CreateTempSubdirectory("arifce-context-explain-");
        try
        {
            var canonical = new CanonicalStore();
            var snapshot = new GitSnapshot("abc", "main", false, [], "digest");
            await canonical.WriteAsync(root.FullName, "decisions", "ADR-0001", new DecisionRecord(1, "ADR-0001", "Token migration", "Use the current token migration", "Current architecture", "ACTIVE", "USER_CONFIRMED", null, DateTimeOffset.UnixEpoch));
            await canonical.WriteAsync(root.FullName, "decisions", "ADR-0002", new DecisionRecord(1, "ADR-0002", "Legacy token migration", "Use the legacy token migration", "Historical", "SUPERSEDED", "USER_CONFIRMED", "ADR-0001", DateTimeOffset.UnixEpoch));
            await canonical.WriteAsync(root.FullName, "claims", "CLAIM-0001", new ClaimRecord(1, "CLAIM-0001", "Token migration remains safe", ClaimStatus.Stale, RiskLevel.High, snapshot, [], DateTimeOffset.UnixEpoch));
            var index = new IndexStore();
            await index.RebuildAsync(root.FullName);
            var composer = new LlmContextComposer(index);

            var first = await composer.ComposeAsync(root.FullName, "token migration", 1000);
            var second = await composer.ComposeAsync(root.FullName, "token migration", 1000);

            Assert.Equal(3, first.Telemetry.CandidateRecords);
            Assert.Equal(1, first.Telemetry.SelectedRecords);
            Assert.Equal(1, first.Telemetry.StaleRejected);
            Assert.Equal(1, first.Telemetry.SupersededRejected);
            Assert.Contains(first.Items, item => item.Path.Contains("adr-0001", StringComparison.Ordinal) && item.Included);
            Assert.Contains(first.Items, item => item.Freshness == "STALE" && !item.Included && item.Reason.Contains("re-verification", StringComparison.Ordinal));
            Assert.Contains(first.Items, item => item.Freshness == "SUPERSEDED" && !item.Included && item.Reason.Contains("ADR-0001", StringComparison.Ordinal));
            Assert.Equal(
                first.Items.Select(item => (item.Path, item.Included, item.Freshness, item.Priority, item.Reason)),
                second.Items.Select(item => (item.Path, item.Included, item.Freshness, item.Priority, item.Reason)));

            var tiny = await composer.ComposeAsync(root.FullName, "token migration", 1);
            Assert.Equal(0, tiny.Telemetry.SelectedRecords);
            Assert.Equal(1, tiny.Telemetry.BudgetRejected);
            Assert.Empty(tiny.Content);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Context_assembly_rejects_redundant_decisions_and_labels_blocking_conflicts()
    {
        var root = Directory.CreateTempSubdirectory("arifce-context-conflict-");
        try
        {
            var canonical = new CanonicalStore();
            await canonical.WriteAsync(root.FullName, "decisions", "ADR-0001", new DecisionRecord(1, "ADR-0001", "Cache policy", "Use bounded local cache", "Primary", "ACTIVE", "USER_CONFIRMED", null, DateTimeOffset.UnixEpoch));
            await canonical.WriteAsync(root.FullName, "decisions", "ADR-0002", new DecisionRecord(1, "ADR-0002", "Cache policy", "Use bounded local cache", "Duplicate", "ACTIVE", "USER_CONFIRMED", null, DateTimeOffset.UnixEpoch));
            await canonical.WriteAsync(root.FullName, "decisions", "ADR-0003", new DecisionRecord(1, "ADR-0003", "Cache policy", "Disable caching", "Conflict", "ACTIVE", "USER_CONFIRMED", null, DateTimeOffset.UnixEpoch));
            var index = new IndexStore(); await index.RebuildAsync(root.FullName);
            var context = await new LlmContextComposer(index).ComposeAsync(root.FullName, "cache policy", 2000);

            Assert.Contains(context.Items, item => item.Path.Contains("adr-0002", StringComparison.Ordinal) && !item.Included && item.Freshness == "DUPLICATE" && item.Reason.Contains("ADR-0001", StringComparison.Ordinal));
            Assert.Contains(context.Items, item => item.Path.Contains("adr-0001", StringComparison.Ordinal) && item.Included && item.Freshness == "CONFLICT");
            Assert.Contains(context.Items, item => item.Path.Contains("adr-0003", StringComparison.Ordinal) && item.Included && item.Freshness == "CONFLICT");
            Assert.Contains("blocking canonical conflict", context.Content, StringComparison.Ordinal);
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task Token_embedding_is_deterministic_and_shared_terms_are_comparable()
    {
        var provider = new TokenEmbeddingProvider(new EmbeddingProfile("tokens", "offline", 128));
        var a = await provider.EmbedAsync("database migration plan");
        var b = await provider.EmbedAsync("database migration checklist");
        var c = await provider.EmbedAsync("unrelated weather forecast");
        static double Cos(float[] x, float[] y) => x.Zip(y, (a, b) => (double)a * b).Sum();
        Assert.Equal(a, await provider.EmbedAsync("database migration plan"));
        Assert.True(Cos(a, b) > Cos(a, c));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { LastRequest = request; return Task.FromResult(responder(request)); }
    }

    private sealed class StubProvider(string id, bool fail)
    {
        public LlmProviderProfile Profile { get; } = new(id, LlmProviderKind.OpenAI, "model", null, null);
        public ILlmProvider Provider { get; } = new Impl(id, fail);
        private sealed class Impl(string id, bool fail) : ILlmProvider
        {
            public string ProviderId => id; public LlmProviderKind Kind => LlmProviderKind.OpenAI;
            public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default) => fail ? throw new HttpRequestException("offline") : Task.FromResult(new LlmResponse(id, "model", "done", new LlmUsage(3, 2), TimeSpan.Zero));
            public Task<LlmConnectionResult> TestConnectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(new LlmConnectionResult(id, !fail, fail ? "offline" : "ok", TimeSpan.Zero));
        }
    }
}
