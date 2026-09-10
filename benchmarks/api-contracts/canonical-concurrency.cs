using ArifCE.Core;
using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root, string claimId)
    {
        var store = new CanonicalStore();
        ClaimRecord updated = await store.UpdateAsync<ClaimRecord>(root, "claims", claimId, value => value with { Evidence = [.. value.Evidence, "EVIDENCE-0001"] });
        await store.WriteAsync(root, "claims", updated.Id, updated);
        _ = store.NextId(root, "tasks", "TASK");
        await new IndexStore().RebuildAsync(root);
    }
}
