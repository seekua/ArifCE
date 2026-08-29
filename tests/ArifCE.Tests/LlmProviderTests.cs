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

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { LastRequest = request; return Task.FromResult(responder(request)); }
    }
}
