using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ArifCE.Infrastructure;

public sealed record CodeGraphNode(string Id, string Kind, string Name, string Path, int? Line, string Confidence);
public sealed record CodeGraphEdge(string From, string To, string Kind, string Confidence);
public sealed record CodeGraphDocument(int SchemaVersion, DateTimeOffset GeneratedAtUtc, IReadOnlyList<CodeGraphNode> Nodes, IReadOnlyList<CodeGraphEdge> Edges);
public sealed record CodeGraphQueryResult(IReadOnlyList<CodeGraphNode> Matches, IReadOnlyList<CodeGraphNode> RelatedNodes, IReadOnlyList<CodeGraphEdge> Edges);

public sealed partial class CodeGraphStore
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase) { ".git", ".arifce", "bin", "obj", "artifacts", "node_modules" };
    private static readonly HashSet<string> ControlWords = new(StringComparer.Ordinal) { "if", "for", "foreach", "while", "switch", "catch", "using", "lock", "return", "new" };

    public async Task<CodeGraphDocument> BuildAsync(string root, CancellationToken cancellationToken = default)
    {
        var fullRoot = Path.GetFullPath(root);
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
                if (method.Success && !ControlWords.Contains(method.Groups[1].Value))
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

        var graph = new CodeGraphDocument(1, DateTimeOffset.UtcNow, nodes.DistinctBy(node => node.Id).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(), edges.Distinct().OrderBy(edge => edge.From, StringComparer.Ordinal).ThenBy(edge => edge.To, StringComparer.Ordinal).ToArray());
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
        return JsonSerializer.Deserialize<CodeGraphDocument>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options) ?? throw new InvalidDataException("The derived code graph is invalid.");
    }

    public async Task<CodeGraphQueryResult> QueryAsync(string root, string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("A symbol name is required.", nameof(symbol));
        var graph = await ReadAsync(root, cancellationToken);
        var matches = graph.Nodes.Where(node => node.Name.Equals(symbol, StringComparison.Ordinal) || node.Name.Contains(symbol, StringComparison.OrdinalIgnoreCase)).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
        var ids = matches.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges.Where(edge => ids.Contains(edge.From) || ids.Contains(edge.To)).ToArray();
        var relatedIds = edges.SelectMany(edge => new[] { edge.From, edge.To }).Where(id => !ids.Contains(id)).ToHashSet(StringComparer.Ordinal);
        return new CodeGraphQueryResult(matches, graph.Nodes.Where(node => relatedIds.Contains(node.Id)).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(), edges);
    }

    private static string GraphPath(string root) => Path.Combine(Path.GetFullPath(root), ".arifce", "index", "code-graph.json");
    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static bool IsTestFile(string path) => path.Contains("test", StringComparison.OrdinalIgnoreCase) || path.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase);
    private static bool IsExcluded(string root, string path) => Relative(root, path).Split('/').Any(ExcludedDirectories.Contains);
    private static bool IsTestMethod(string[] lines, int index) => lines.Skip(Math.Max(0, index - 3)).Take(Math.Min(4, index + 1)).Any(line => line.Contains("[Fact", StringComparison.Ordinal) || line.Contains("[Theory", StringComparison.Ordinal) || line.Contains("[Test", StringComparison.Ordinal));
    private static Regex IdentifierOccurrence(string name) => new($@"\b{Regex.Escape(name)}\b", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\b(?:class|interface|record|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex TypeDeclaration();
    [GeneratedRegex(@"\b([A-Za-z_][A-Za-z0-9_]*)\s*\([^;{}]*\)\s*(?:=>|\{|$)", RegexOptions.CultureInvariant)]
    private static partial Regex MethodDeclaration();
}
