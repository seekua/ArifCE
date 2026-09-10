using ArifCE.Core;
using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root, ILlmProvider provider, LlmProviderProfile profile)
    {
        var store = new CanonicalStore();
        var orchestrator = new LlmOrchestrator(new LlmRouter([(provider, profile)]), store, new JournalStore(), new GitInspector());
        var result = await orchestrator.ExecuteAsync(root, new LlmRequest("review", "safe prompt"), "CLAIM-0001");
        _ = (result.Route, result.Route.Response, result.Route.Response.RawResponse, result.Evidence);
    }
}
