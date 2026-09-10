using ArifCE.Core;
using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root, EvidenceRecord evidence)
    {
        var service = new ProjectService(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());
        EvidenceScope? scope = await EvidenceScopeTracker.CaptureAsync(root, ["path.cs"]);
        EvidenceFreshness freshness = await EvidenceScopeTracker.EvaluateAsync(root, evidence with { Scope = scope }, await new GitInspector().CaptureAsync(root));
        TrustRefreshResult refresh = await service.RefreshTrustAsync(root);
        _ = (scope!.Dependencies, scope.ContractId, freshness, refresh.ClaimsStaled, refresh.AcceptancesFlagged, refresh.Warnings, AcceptanceStatus.NeedsReview);
    }
}
