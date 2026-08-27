namespace ArifCE.Infrastructure;

public sealed record ArchitectureBoundaryScanResult(int FilesScanned, int ViolatingFiles, IReadOnlyList<string> Violations);

public static class ArchitectureBoundaryScanner
{
    private static readonly HashSet<string> SourceExtensions = new(StringComparer.OrdinalIgnoreCase) { ".cs", ".csproj", ".props", ".targets" };
    private static readonly string[] ExcludedDirectoryNames = [".git", ".arifce", "bin", "obj", "artifacts"];

    public static async Task<ArchitectureBoundaryScanResult> ScanAsync(string root, IReadOnlyList<string> forbiddenReferences, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var files = paths.Select(path => ResolveWithinRoot(normalizedRoot, path)).SelectMany(EnumerateSourceFiles).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        var violations = new List<string>();
        var violatingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            for (var index = 0; index < lines.Length; index++)
            {
                foreach (var forbidden in forbiddenReferences.Order(StringComparer.Ordinal))
                {
                    if (lines[index].Contains(forbidden, StringComparison.Ordinal))
                    {
                        var relative = Path.GetRelativePath(normalizedRoot, file).Replace(Path.DirectorySeparatorChar, '/');
                        violations.Add($"{relative}:{index + 1}: forbidden reference '{forbidden}'.");
                        violatingFiles.Add(file);
                    }
                }
            }
        }
        return new ArchitectureBoundaryScanResult(files.Length, violatingFiles.Count, violations.Order(StringComparer.Ordinal).ToArray());
    }

    private static string ResolveWithinRoot(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A source path must not be blank.");
        var resolved = Path.GetFullPath(Path.Combine(root, path));
        var relative = Path.GetRelativePath(root, resolved);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new ArgumentException($"Source path '{path}' is outside the repository root.");
        if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(ExcludedDirectoryNames.Contains)) throw new ArgumentException($"Source path '{path}' is excluded from architecture scanning.");
        if (!File.Exists(resolved) && !Directory.Exists(resolved)) throw new ArgumentException($"Source path '{path}' does not exist.");
        return resolved;
    }

    private static IEnumerable<string> EnumerateSourceFiles(string path)
    {
        if (File.Exists(path)) return SourceExtensions.Contains(Path.GetExtension(path)) ? [path] : [];
        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Where(file => SourceExtensions.Contains(Path.GetExtension(file)))
            .Where(file => !Path.GetRelativePath(path, file).Split(Path.DirectorySeparatorChar).Any(ExcludedDirectoryNames.Contains));
    }
}
