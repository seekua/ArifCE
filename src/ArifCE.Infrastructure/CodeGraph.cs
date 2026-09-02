using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ArifCE.Infrastructure;

public sealed record CodeGraphNode(string Id, string Kind, string Name, string Path, int? Line, string Confidence);
public sealed record CodeGraphEdge(string From, string To, string Kind, string Confidence);
public sealed record CodeGraphDocument(int SchemaVersion, DateTimeOffset GeneratedAtUtc, IReadOnlyList<CodeGraphNode> Nodes, IReadOnlyList<CodeGraphEdge> Edges, string? SourceDigest = null, int GeneratorVersion = 0);
public sealed record CodeGraphQueryResult(IReadOnlyList<CodeGraphNode> Matches, IReadOnlyList<CodeGraphNode> RelatedNodes, IReadOnlyList<CodeGraphEdge> Edges);
public sealed record TrustedCodeGraphClosure(string Target, IReadOnlyList<string> Paths, string Digest);

public sealed partial class CodeGraphStore
{
    private const int CurrentGeneratorVersion = 2;
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase) { ".git", ".arifce", "bin", "obj", "artifacts", "node_modules" };

    public Task<CodeGraphDocument> BuildAsync(string root, CancellationToken cancellationToken = default) => BuildStableAsync(root, 0, cancellationToken);

    private async Task<CodeGraphDocument> BuildStableAsync(string root, int attempt, CancellationToken cancellationToken)
    {
        var fullRoot = Path.GetFullPath(root);
        var sourceDigest = await ComputeSourceDigestAsync(fullRoot, cancellationToken);
        var nodes = new List<CodeGraphNode>();
        var edges = new List<CodeGraphEdge>();
        var files = Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories).Where(path => !IsExcluded(fullRoot, path)).Order(StringComparer.Ordinal).ToArray();
        foreach (var file in files)
        {
            var relative = Relative(fullRoot, file);
            var fileId = $"file:{relative}";
            nodes.Add(new CodeGraphNode(fileId, IsTestFile(relative) ? "TEST_FILE" : "FILE", Path.GetFileName(relative), relative, null, "EXACT"));
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (Match match in TypeDeclaration().Matches(lines[index]))
                {
                    var name = match.Groups[1].Value;
                    var id = $"type:{relative}:{index + 1}:{name}";
                    nodes.Add(new CodeGraphNode(id, "TYPE", name, relative, index + 1, "STRUCTURAL"));
                    edges.Add(new CodeGraphEdge(fileId, id, "DECLARES", "STRUCTURAL"));
                }
                var method = MethodDeclaration().Match(lines[index]);
                if (method.Success)
                {
                    var name = method.Groups[1].Value;
                    var id = $"method:{relative}:{index + 1}:{name}";
                    nodes.Add(new CodeGraphNode(id, IsTestMethod(lines, index) ? "TEST" : "METHOD", name, relative, index + 1, "STRUCTURAL"));
                    edges.Add(new CodeGraphEdge(fileId, id, "DECLARES", "STRUCTURAL"));
                }
            }
        }

        var symbols = nodes.Where(node => node.Kind is "TYPE" or "METHOD" or "TEST").GroupBy(node => node.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var file in files)
        {
            var relative = Relative(fullRoot, file);
            var fileId = $"file:{relative}";
            var text = await File.ReadAllTextAsync(file, cancellationToken);
            foreach (var pair in symbols)
            {
                if (!IdentifierOccurrence(pair.Key).IsMatch(text)) continue;
                foreach (var target in pair.Value.Where(node => !node.Path.Equals(relative, StringComparison.OrdinalIgnoreCase)))
                {
                    var kind = IsTestFile(relative) ? "RELATED_TEST" : "REFERENCES";
                    edges.Add(new CodeGraphEdge(fileId, target.Id, kind, "HEURISTIC"));
                }
            }
        }

        foreach (var project in Directory.EnumerateFiles(fullRoot, "*.csproj", SearchOption.AllDirectories).Where(path => !IsExcluded(fullRoot, path)).Order(StringComparer.Ordinal))
        {
            var relative = Relative(fullRoot, project);
            var projectId = $"project:{relative}";
            nodes.Add(new CodeGraphNode(projectId, "PROJECT", Path.GetFileNameWithoutExtension(project), relative, null, "EXACT"));
            var document = XDocument.Load(project);
            foreach (var reference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference").Select(element => element.Attribute("Include")?.Value).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var targetPath = Relative(fullRoot, Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference!)));
                var targetId = $"project:{targetPath}";
                if (!nodes.Any(node => node.Id == targetId)) nodes.Add(new CodeGraphNode(targetId, "PROJECT", Path.GetFileNameWithoutExtension(targetPath), targetPath, null, "EXACT"));
                edges.Add(new CodeGraphEdge(projectId, targetId, "PROJECT_REFERENCE", "EXACT"));
            }
        }

        var finalDigest = await ComputeSourceDigestAsync(fullRoot, cancellationToken);
        if (!string.Equals(sourceDigest, finalDigest, StringComparison.Ordinal))
        {
            if (attempt >= 2) throw new IOException("Source files kept changing while the deterministic code graph was being built.");
            return await BuildStableAsync(fullRoot, attempt + 1, cancellationToken);
        }
        var graph = new CodeGraphDocument(1, DateTimeOffset.UtcNow, nodes.DistinctBy(node => node.Id).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(), edges.Distinct().OrderBy(edge => edge.From, StringComparer.Ordinal).ThenBy(edge => edge.To, StringComparer.Ordinal).ToArray(), sourceDigest, CurrentGeneratorVersion);
        var path = GraphPath(fullRoot); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(graph, JsonDefaults.Options), cancellationToken);
        File.Move(temporary, path, true);
        return graph;
    }

    public async Task<CodeGraphDocument> ReadAsync(string root, CancellationToken cancellationToken = default)
    {
        var path = GraphPath(root);
        if (!File.Exists(path)) return await BuildAsync(root, cancellationToken);
        try
        {
            var graph = JsonSerializer.Deserialize<CodeGraphDocument>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options);
            if (graph is null || graph.SchemaVersion != 1 || graph.GeneratorVersion != CurrentGeneratorVersion || graph.Nodes is null || graph.Edges is null || string.IsNullOrWhiteSpace(graph.SourceDigest)) return await BuildAsync(root, cancellationToken);
            var currentDigest = await ComputeSourceDigestAsync(root, cancellationToken);
            return string.Equals(graph.SourceDigest, currentDigest, StringComparison.Ordinal) ? graph : await BuildAsync(root, cancellationToken);
        }
        catch (JsonException)
        {
            return await BuildAsync(root, cancellationToken);
        }
    }

    public async Task<CodeGraphQueryResult> QueryAsync(string root, string symbol, CancellationToken cancellationToken = default, bool exactMatch = false)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("A symbol name is required.", nameof(symbol));
        var graph = await ReadAsync(root, cancellationToken);
        var matches = MatchNodes(graph.Nodes, symbol, exactMatch).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
        var ids = matches.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges.Where(edge => ids.Contains(edge.From) || ids.Contains(edge.To)).ToArray();
        var relatedIds = edges.SelectMany(edge => new[] { edge.From, edge.To }).Where(id => !ids.Contains(id)).ToHashSet(StringComparer.Ordinal);
        return new CodeGraphQueryResult(matches, graph.Nodes.Where(node => relatedIds.Contains(node.Id)).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(), edges);
    }

    public async Task<TrustedCodeGraphClosure> TrustedClosureAsync(string root, string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("A symbol name is required.", nameof(symbol));
        var graph = await ReadAsync(root, cancellationToken);
        var matches = MatchNodes(graph.Nodes, symbol, exactMatch: true).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
        if (matches.Length == 0) throw new InvalidOperationException($"No exact code-graph symbol matches '{symbol}'.");

        var visited = matches.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var pending = new Queue<string>(visited.Order(StringComparer.Ordinal));
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            foreach (var edge in graph.Edges)
            {
                string? related = edge switch
                {
                    { Confidence: "STRUCTURAL", Kind: "DECLARES" } when edge.From == id => edge.To,
                    { Confidence: "STRUCTURAL", Kind: "DECLARES" } when edge.To == id => edge.From,
                    { Confidence: "EXACT", Kind: "PROJECT_REFERENCE" } when edge.To == id => edge.From,
                    _ => null
                };
                if (related is not null && visited.Add(related)) pending.Enqueue(related);
            }
        }

        var paths = graph.Nodes.Where(node => visited.Contains(node.Id)).Select(node => node.Path).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        var digestInput = $"TRUSTED_CODE_GRAPH_CLOSURE_V1\n{symbol}\n{string.Join('\n', paths)}";
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(digestInput))).ToLowerInvariant();
        return new TrustedCodeGraphClosure(symbol, paths, digest);
    }

    private static string GraphPath(string root) => Path.Combine(Path.GetFullPath(root), ".arifce", "index", "code-graph.json");
    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static IEnumerable<CodeGraphNode> MatchNodes(IEnumerable<CodeGraphNode> nodes, string target, bool exactMatch)
    {
        var selector = ParseQualifiedTarget(target);
        if (selector is not null)
        {
            return nodes.Where(node => node.Path.Equals(selector.Value.Path, StringComparison.OrdinalIgnoreCase) && node.Name.Equals(selector.Value.Symbol, StringComparison.Ordinal));
        }

        return nodes.Where(node => exactMatch
            ? node.Name.Equals(target, StringComparison.Ordinal)
            : node.Name.Equals(target, StringComparison.Ordinal) || node.Name.Contains(target, StringComparison.OrdinalIgnoreCase));
    }

    private static (string Path, string Symbol)? ParseQualifiedTarget(string target)
    {
        const string separator = "::";
        var separatorIndex = target.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex <= 0 || separatorIndex != target.LastIndexOf(separator, StringComparison.Ordinal)) return null;

        var path = target[..separatorIndex].Trim().Replace('\\', '/');
        var symbol = target[(separatorIndex + separator.Length)..].Trim();
        return path.Length > 0 && symbol.Length > 0 ? (path, symbol) : null;
    }
    private static bool IsTestFile(string path) => path.Contains("test", StringComparison.OrdinalIgnoreCase) || path.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);
    private static bool IsExcluded(string root, string path) => Relative(root, path).Split('/').Any(ExcludedDirectories.Contains);
    private static bool IsTestMethod(string[] lines, int index) => lines.Skip(Math.Max(0, index - 3)).Take(Math.Min(4, index + 1)).Any(line => line.Contains("[Fact", StringComparison.Ordinal) || line.Contains("[Theory", StringComparison.Ordinal) || line.Contains("[Test", StringComparison.Ordinal));
    private static Regex IdentifierOccurrence(string name) => new($@"\b{Regex.Escape(name)}\b", RegexOptions.CultureInvariant);

    private static async Task<string> ComputeSourceDigestAsync(string root, CancellationToken cancellationToken)
    {
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) && !IsExcluded(root, path))
            .OrderBy(path => Relative(root, path), StringComparer.Ordinal)
            .ToArray();
        using var aggregate = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            aggregate.AppendData(Encoding.UTF8.GetBytes(Relative(root, file) + "\n"));
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            aggregate.AppendData(await SHA256.HashDataAsync(stream, cancellationToken));
        }
        return Convert.ToHexString(aggregate.GetHashAndReset()).ToLowerInvariant();
    }

    [GeneratedRegex(@"\b(?:class|interface|record|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TypeDeclaration();
    [GeneratedRegex(@"(?:^|[{};])\s*(?:\[[^\]]+\]\s*)*(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|async|extern|unsafe|new|partial|readonly)\s+)*(?:[A-Za-z_][A-Za-z0-9_?.]*(?:\s*<[^;{}()]+>)?(?:\[\])?)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^;{}()]+>)?\s*\([^;{}]*\)\s*(?:=>|\{|$)", RegexOptions.CultureInvariant)]
    private static partial Regex MethodDeclaration();
}
