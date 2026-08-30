using System.Net;
using System.Net.Http;
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
        var selector = new EmbeddingProviderSelector(new[] { (IEmbeddingProvider)new LocalEmbeddingProvider(new EmbeddingProfile("local", "local")) });
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
            await File.WriteAllTextAsync(Path.Combine(root.FullName, ".arifce", "decisions", "adr.json"), "database migration decision context");
            var index = new IndexStore(); await index.RebuildAsync(root.FullName);
            var context = await new LlmContextComposer(index).ComposeAsync(root.FullName, "database migration", 100);
            Assert.Contains("adr.json", context.Sources[0]); Assert.True(context.EstimatedTokens <= 100);
        }
        finally { root.Delete(true); }
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
