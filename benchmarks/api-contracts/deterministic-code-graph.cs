using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root)
    {
        var store = new CodeGraphStore();
        CodeGraphDocument graph = await store.BuildAsync(root);
        graph = await store.ReadAsync(root);
        CodeGraphQueryResult query = await store.QueryAsync(root, "path.cs::Symbol", exactMatch: true);
        TrustedCodeGraphClosure closure = await store.TrustedClosureAsync(root, "path.cs::Symbol");
        _ = (graph.SchemaVersion, graph.SourceDigest, graph.Nodes, graph.Edges, graph.GeneratorVersion);
        _ = (query.Matches, query.RelatedNodes, query.Edges);
        _ = (closure.Target, closure.Paths, closure.Digest);
    }
}
