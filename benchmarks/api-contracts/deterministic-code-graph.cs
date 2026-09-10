using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root)
    {
        var store = new CodeGraphStore();
        var node = new CodeGraphNode("node", "METHOD", "Symbol", "path.cs", 1, "STRUCTURAL");
        var edge = new CodeGraphEdge("from", "to", "CALLS", "HEURISTIC");
        var document = new CodeGraphDocument(1, DateTimeOffset.UtcNow, new[] { node }, new[] { edge }, "digest", 1);
        var queryResult = new CodeGraphQueryResult(new[] { node }, new[] { node }, new[] { edge });
        var trustedClosure = new TrustedCodeGraphClosure("path.cs::Symbol", new[] { "path.cs" }, "digest");

        _ = (node.Id, node.Kind, node.Name, node.Path, node.Line, node.Confidence);
        _ = (edge.From, edge.To, edge.Kind, edge.Confidence);
        _ = (document.SchemaVersion, document.GeneratedAtUtc, document.Nodes, document.Edges, document.SourceDigest, document.GeneratorVersion);
        _ = (queryResult.Matches, queryResult.RelatedNodes, queryResult.Edges);
        _ = (trustedClosure.Target, trustedClosure.Paths, trustedClosure.Digest);
        document = document with { SourceDigest = null, GeneratorVersion = -1 };

        CodeGraphDocument graph = await store.BuildAsync(root, CancellationToken.None);
        graph = await store.ReadAsync(root, CancellationToken.None);
        CodeGraphQueryResult query = await store.QueryAsync(root, "path.cs::Symbol", CancellationToken.None, exactMatch: true);
        TrustedCodeGraphClosure closure = await store.TrustedClosureAsync(root, "path.cs::Symbol", CancellationToken.None);
        _ = (graph, query, closure, document);
    }
}
