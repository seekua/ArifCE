using System.Security.Cryptography;
using System.Text.Json;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

// Deterministic source fixtures, not compiler-bound semantic resolution or a model benchmark.
public sealed class BenchmarkGraphTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-graph-benchmark-" + Guid.NewGuid().ToString("N"));
    private readonly CodeGraphStore store = new();

    [Fact]
    public async Task Graph_preserves_declarations_and_relationship_confidence()
    {
        await Seed();
        var graph = await store.BuildAsync(root);
        Assert.Equal(graph.Nodes.Count, graph.Nodes.Select(node => node.Id).Distinct().Count());
        var ids = graph.Nodes.Select(node => node.Id).ToHashSet();
        Assert.All(graph.Edges, edge => { Assert.Contains(edge.From, ids); Assert.Contains(edge.To, ids); });
        Assert.All(graph.Nodes.Where(node => node.Kind is "FILE" or "TEST_FILE" or "PROJECT"), node => Assert.Equal("EXACT", node.Confidence));
        Assert.All(graph.Nodes.Where(node => node.Kind is "TYPE" or "METHOD" or "TEST" or "CONSTRUCTOR"), node => Assert.Equal("STRUCTURAL", node.Confidence));
        var calculate = graph.Nodes.Where(node => node.Path == "src/Core/Payment.cs" && node.Kind == "METHOD" && node.Name == "Calculate").ToArray();
        Assert.Equal(2, calculate.Length); // Same-line overloads must retain distinct identities.
        var owner = Assert.Single(graph.Nodes, node => node.Path == "src/Core/Payment.cs" && node.Kind == "TYPE" && node.Name == "Payment");
        Assert.All(calculate, method => Assert.Contains(graph.Edges, edge => edge.From == owner.Id && edge.To == method.Id && edge.Kind == "CONTAINS" && edge.Confidence == "STRUCTURAL"));
        Assert.Contains(graph.Nodes, node => node.Kind == "CONSTRUCTOR" && node.Name == "Payment");
        Assert.Contains(graph.Nodes, node => node.Kind == "TEST" && node.Name == "Payment_result");
        Assert.DoesNotContain(graph.Nodes, node => node.Kind == "METHOD" && node.Name == "NotADeclaration");
        foreach (var kind in new[] { "REFERENCES", "RELATED_TEST", "CALLS" })
        {
            var relationships = graph.Edges.Where(edge => edge.Kind == kind).ToArray();
            Assert.NotEmpty(relationships);
            Assert.All(relationships, edge => Assert.Equal("HEURISTIC", edge.Confidence));
        }
        var consumer = Assert.Single(graph.Nodes, node => node.Path == "src/App/Consumer.cs" && node.Name == "Run");
        Assert.Contains(graph.Edges, edge => edge.From == consumer.Id && calculate.Any(target => target.Id == edge.To) && edge.Kind == "CALLS");
        Assert.Contains(graph.Edges, edge => edge.From == "project:src/App/App.csproj" && edge.To == "project:src/Core/Core.csproj" && edge.Kind == "PROJECT_REFERENCE" && edge.Confidence == "EXACT");
        Assert.Equal(2, (await store.QueryAsync(root, "src/Core/Payment.cs::Calculate")).Matches.Count);
        Assert.Single((await store.QueryAsync(root, "src/Other/Other.cs::Calculate")).Matches);
        Assert.Empty((await store.QueryAsync(root, "Calcul", exactMatch: true)).Matches);
        Assert.Empty((await store.QueryAsync(root, "NoSuchSymbol", exactMatch: true)).Matches);
        await Assert.ThrowsAsync<ArgumentException>(() => store.QueryAsync(root, " "));
    }

    [Fact]
    public async Task Graph_tracks_source_lifecycle_and_ignores_derived_noise()
    {
        await Seed();
        var initial = await store.BuildAsync(root);
        Assert.False(string.IsNullOrWhiteSpace(initial.SourceDigest));
        await Write(".arifce/CURRENT.md", "Metadata changed");
        await Write("obj/Noise.cs", "class IgnoredNoise {}");
        await Write("node_modules/Noise.cs", "class IgnoredNoise {}");
        var unchanged = await store.ReadAsync(root);
        Assert.Equal(initial.SourceDigest, unchanged.SourceDigest);
        Assert.Equal(Semantics(initial), Semantics(unchanged));
        Assert.Empty((await store.QueryAsync(root, "IgnoredNoise")).Matches);
        await Write("src/Core/New.cs", "class AddedType { public void AddedMethod() {} }");
        Assert.Single((await store.QueryAsync(root, "AddedMethod", exactMatch: true)).Matches);
        Assert.NotEqual(initial.SourceDigest, (await store.ReadAsync(root)).SourceDigest);
        await Write("src/Core/New.cs", "class AddedType { public void EditedMethod() {} }");
        Assert.Empty((await store.QueryAsync(root, "AddedMethod", exactMatch: true)).Matches);
        Assert.Single((await store.QueryAsync(root, "EditedMethod", exactMatch: true)).Matches);
        File.Move(Path.Combine(root, "src/Core/New.cs"), Path.Combine(root, "src/Core/Renamed.cs"));
        Assert.Equal("src/Core/Renamed.cs", Assert.Single((await store.QueryAsync(root, "EditedMethod", exactMatch: true)).Matches).Path);
        File.Delete(Path.Combine(root, "src/Core/Renamed.cs"));
        Assert.Empty((await store.QueryAsync(root, "EditedMethod", exactMatch: true)).Matches);
        await Write("src/App/App.csproj", "<Project />");
        Assert.DoesNotContain((await store.ReadAsync(root)).Edges, edge => edge.From == "project:src/App/App.csproj" && edge.Kind == "PROJECT_REFERENCE");
    }

    [Fact]
    public async Task Graph_rebuild_is_equivalent_and_preserves_canonical_bytes()
    {
        await Seed();
        var before = CanonicalHashes();
        var original = await store.BuildAsync(root);
        var expected = Semantics(original);
        var query = JsonSerializer.Serialize(await store.QueryAsync(root, "Calculate"));
        var cache = Path.Combine(root, ".arifce/index/code-graph.json");
        File.Delete(cache);
        Assert.Equal(expected, Semantics(await store.ReadAsync(root)));
        Assert.Equal(query, JsonSerializer.Serialize(await store.QueryAsync(root, "Calculate")));
        await File.WriteAllTextAsync(cache, "{broken");
        Assert.Equal(expected, Semantics(await store.ReadAsync(root)));
        await File.WriteAllTextAsync(cache, JsonSerializer.Serialize(original with { GeneratorVersion = -1, SourceDigest = null }));
        Assert.Equal(expected, Semantics(await store.ReadAsync(root)));
        Assert.Equal(expected, Semantics(await store.BuildAsync(root)));
        Assert.Equal(before, CanonicalHashes());
    }

    [Fact]
    public async Task Graph_trusted_closure_excludes_heuristics_and_follows_project_dependents()
    {
        await Seed();
        var closure = await store.TrustedClosureAsync(root, "src/Core/Payment.cs::Calculate");
        Assert.Equal(new[] { "src/Core/Payment.cs" }, closure.Paths);
        Assert.Equal(closure, (await store.TrustedClosureAsync(root, "src/Core/Payment.cs::Calculate")) with { Paths = closure.Paths });
        var project = await store.TrustedClosureAsync(root, "src/Core/Core.csproj::Core");
        Assert.Equal(new[] { "src/App/App.csproj", "src/Core/Core.csproj", "src/Host/Host.csproj" }, project.Paths);
        Assert.DoesNotContain("src/Other/Other.csproj", project.Paths);
        Assert.DoesNotContain("src/App/Consumer.cs", closure.Paths);
        Assert.DoesNotContain("tests/PaymentTests.cs", closure.Paths);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.TrustedClosureAsync(root, "NoSuchSymbol"));
        await Write("src/App/App.csproj", "<Project />");
        var changed = await store.TrustedClosureAsync(root, "src/Core/Core.csproj::Core");
        Assert.Equal(new[] { "src/Core/Core.csproj" }, changed.Paths);
        Assert.NotEqual(project.Digest, changed.Digest);
    }

    private async Task Seed()
    {
        await Write("src/Core/Payment.cs", "public class Payment { public Payment() {} public decimal Calculate(decimal value) => value; public decimal Calculate(decimal value, int count) => value * count; } // NotADeclaration()");
        await Write("src/App/Consumer.cs", "class Consumer { public decimal Run() => new Payment().Calculate(10); }");
        await Write("src/Other/Other.cs", "class Other { public int Calculate() => 3; }");
        await Write("tests/PaymentTests.cs", "class PaymentTests { [Fact] public void Payment_result() { new Payment().Calculate(10); } }");
        await Write("src/Core/Core.csproj", "<Project />");
        await Write("src/App/App.csproj", "<Project><ItemGroup><ProjectReference Include=\"../Core/Core.csproj\" /></ItemGroup></Project>");
        await Write("src/Host/Host.csproj", "<Project><ItemGroup><ProjectReference Include=\"../App/App.csproj\" /></ItemGroup></Project>");
        await Write("src/Other/Other.csproj", "<Project />");
        await Write(".arifce/CURRENT.md", "# Canonical context\nPreserve exact bytes.");
        await Write(".arifce/decisions/decision-0001.md", "# Historical decision\nDo not infer semantic certainty from a name match.");
    }

    private async Task Write(string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private string[] CanonicalHashes() => Directory.EnumerateFiles(Path.Combine(root, ".arifce"), "*", SearchOption.AllDirectories)
        .Where(path => !Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith(".arifce/index/", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();

    private static string Semantics(CodeGraphDocument graph) => JsonSerializer.Serialize(new { graph.SchemaVersion, graph.SourceDigest, graph.Nodes, graph.Edges });
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
