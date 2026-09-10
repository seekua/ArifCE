using ArifCE.Core;
using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root)
    {
        GitSnapshot snapshot = await new GitInspector().CaptureAsync(root);
        EvidenceFreshness freshness = EvidenceEvaluator.Evaluate(snapshot, snapshot);
        _ = (snapshot.Commit, snapshot.Branch, snapshot.IsDirty, snapshot.ChangedFiles, snapshot.Digest, freshness);
    }
}
