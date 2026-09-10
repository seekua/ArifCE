using ArifCE.Core;
using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root)
    {
        var service = new ProjectService(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());
        ChangeContractRecord contract = await service.CreateChangeContractAsync(root, "path.cs::Symbol", RiskLevel.High, ["Public API unchanged"]);
        contract = (await service.GetChangeContractAsync(root, contract.Id))!;
        var verified = await service.VerifyAsync(root, contract.ClaimId, "dotnet test --no-restore", false, ["path.cs"], contractId: contract.Id);
        TrustRefreshResult refresh = await service.RefreshTrustAsync(root);
        EvidenceFreshness freshness = await EvidenceScopeTracker.EvaluateAsync(root, verified.Evidence, await new GitInspector().CaptureAsync(root));
        _ = (contract.Id, contract.ClaimId, contract.PotentialImpact, contract.RelatedTests, contract.HistoricalRecords, contract.Invariants, contract.RequiredVerification, contract.Status, contract.Risk);
        _ = (verified.Claim, verified.Evidence, refresh.ClaimsStaled, refresh.AcceptancesFlagged, refresh.Warnings, freshness);
    }
}
