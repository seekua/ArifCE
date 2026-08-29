using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed record LlmTaskRoute(string TaskKind, string ProviderId);

public sealed class LlmTaskRouter
{
    private readonly IReadOnlyDictionary<string, string> _routes;
    public LlmTaskRouter(IEnumerable<LlmTaskRoute> routes) => _routes = routes.ToDictionary(x => x.TaskKind, x => x.ProviderId, StringComparer.OrdinalIgnoreCase);
    public string? Select(string taskKind) => _routes.TryGetValue(taskKind, out var provider) ? provider : null;
}

public sealed record LlmExecutionResult(LlmRouteResult Route, EvidenceRecord Evidence);

public sealed class LlmOrchestrator
{
    private readonly LlmRouter _router;
    private readonly LlmTaskRouter? _taskRouter;
    private readonly CanonicalStore _canonical;
    private readonly JournalStore _journal;
    private readonly GitInspector _git;
    public LlmOrchestrator(LlmRouter router, CanonicalStore canonical, JournalStore journal, GitInspector git, LlmTaskRouter? taskRouter = null)
    { _router = router; _canonical = canonical; _journal = journal; _git = git; _taskRouter = taskRouter; }

    public async Task<LlmExecutionResult> ExecuteAsync(string root, LlmRequest request, string claimId, CancellationToken cancellationToken = default)
    {
        var route = await _router.CompleteAsync(request, cancellationToken);
        var snapshot = await _git.CaptureAsync(root, cancellationToken);
        var evidenceId = _canonical.NextId(root, "evidence", "EVIDENCE");
        var summary = $"Provider {route.Response.ProviderId} / model {route.Response.Model}: {route.Response.Text.Replace("\r", " ").Replace("\n", " ").Trim()}";
        var evidence = new EvidenceRecord(1, evidenceId, claimId, "llm-response", $"llm:{route.Response.ProviderId}/{route.Response.Model}", 0, summary, snapshot, DateTimeOffset.UtcNow, new EvidenceMetrics(route.Response.Usage.TotalTokens, null, null, null));
        await _canonical.WriteAsync(root, "evidence", evidenceId, evidence, cancellationToken);
        await _journal.AppendAsync(root, new JournalEvent(1, Guid.NewGuid().ToString("N"), "llm.completed", evidence.CreatedAtUtc, evidenceId, new { provider = route.Response.ProviderId, model = route.Response.Model, claimId, tokens = route.Response.Usage.TotalTokens, estimatedCost = route.EstimatedCost }), cancellationToken);
        return new(route, evidence);
    }
}
