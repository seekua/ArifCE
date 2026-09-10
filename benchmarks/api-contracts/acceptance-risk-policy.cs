using ArifCE.Core;
using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root, string claimId)
    {
        var service = new ProjectService(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());
        AcceptanceRecord acceptance = await service.CreateAcceptanceAsync(root, claimId, "actor", "rationale");
        acceptance = (await service.GetAcceptanceAsync(root, acceptance.Id))!;
        _ = (acceptance.Id, acceptance.ClaimId, acceptance.Status, acceptance.EvidenceIds);
    }
}
