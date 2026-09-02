using System.Security.Cryptography;
using System.Text;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

public static class EvidenceScopeTracker
{
    public static async Task<EvidenceScope?> CaptureAsync(string root, IReadOnlyList<string>? paths, CancellationToken cancellationToken = default)
    {
        if (paths is null || paths.Count == 0) return null;
        var dependencies = new List<EvidenceDependency>();
        foreach (var path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => Normalize(root, path)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal))
            dependencies.Add(new EvidenceDependency(path, await DigestAsync(root, path, "CONTENT", cancellationToken)));
        if (dependencies.Count == 0) throw new ArgumentException("At least one non-empty evidence scope path is required.", nameof(paths));
        return new EvidenceScope(dependencies);
    }

    public static async Task<EvidenceScope> CaptureForContractAsync(string root, ChangeContractRecord contract, IReadOnlyList<string>? additionalPaths = null, CancellationToken cancellationToken = default)
    {
        var closure = await new CodeGraphStore().TrustedClosureAsync(root, contract.Target, cancellationToken);
        var dependencies = new List<EvidenceDependency> { new($"symbol:{contract.Target}", closure.Digest, "CODE_GRAPH_CLOSURE") };
        var paths = closure.Paths.Concat(additionalPaths ?? []).Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => Normalize(root, path)).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal);
        foreach (var path in paths) dependencies.Add(new EvidenceDependency(path, await DigestAsync(root, path, "CONTENT", cancellationToken)));
        return new EvidenceScope(dependencies, contract.Id);
    }

    public static async Task<EvidenceScope> CaptureApiSurfaceAsync(string root, string assemblyPath, string baselinePath, CancellationToken cancellationToken = default) =>
        new([
            await CaptureDependencyAsync(root, assemblyPath, "PUBLIC_API_SURFACE", cancellationToken),
            await CaptureDependencyAsync(root, baselinePath, "CONTENT", cancellationToken)
        ]);

    public static async Task<EvidenceScope> CaptureSqliteSchemaAsync(string root, string databasePath, string baselinePath, CancellationToken cancellationToken = default) =>
        new([
            await CaptureDependencyAsync(root, databasePath, "SQLITE_SCHEMA", cancellationToken),
            await CaptureDependencyAsync(root, baselinePath, "CONTENT", cancellationToken)
        ]);

    public static async Task<EvidenceFreshness> EvaluateAsync(string root, EvidenceRecord evidence, GitSnapshot current, CancellationToken cancellationToken = default)
    {
        if (evidence.Scope is not { Dependencies.Count: > 0 }) return EvidenceEvaluator.Evaluate(evidence.Snapshot, current);
        foreach (var dependency in evidence.Scope.Dependencies)
        {
            string digest;
            try { digest = await DigestAsync(root, dependency.Path, dependency.Mode, cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch { return EvidenceFreshness.Unknown; }
            if (!string.Equals(dependency.Digest, digest, StringComparison.Ordinal)) return EvidenceFreshness.Stale;
        }
        return EvidenceFreshness.Current;
    }

    private static string Normalize(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, path.Trim()));
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ArgumentException($"Evidence scope path '{path}' is outside the repository root.");
        var normalized = relative.Replace('\\', '/');
        if (normalized.Equals(".git", StringComparison.OrdinalIgnoreCase) || normalized.StartsWith(".git/", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Evidence scope cannot target Git's internal metadata.");
        return normalized;
    }

    private static async Task<EvidenceDependency> CaptureDependencyAsync(string root, string path, string mode, CancellationToken cancellationToken)
    {
        var normalized = Normalize(root, path);
        return new EvidenceDependency(normalized, await DigestAsync(root, normalized, mode, cancellationToken), mode);
    }

    private static async Task<string> DigestAsync(string root, string relativePath, string mode, CancellationToken cancellationToken)
    {
        if (mode == "CODE_GRAPH_CLOSURE")
        {
            if (!relativePath.StartsWith("symbol:", StringComparison.Ordinal)) throw new ArgumentException("Code-graph closure dependencies require a symbol target.");
            return (await new CodeGraphStore().TrustedClosureAsync(root, relativePath["symbol:".Length..], cancellationToken)).Digest;
        }
        var fullRoot = Path.GetFullPath(root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(fullRoot, fullPath);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            throw new ArgumentException($"Evidence scope path '{relativePath}' is outside the repository root.");
        if ((File.Exists(fullPath) || Directory.Exists(fullPath)) && (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new ArgumentException($"Evidence scope path '{relativePath}' cannot be a symbolic link or reparse point.");

        if (mode == "PUBLIC_API_SURFACE")
        {
            if (!File.Exists(fullPath)) return "MISSING";
            return HashText("PUBLIC_API_SURFACE\n" + string.Join('\n', ApiSurfaceAnalyzer.Read(fullPath).Order(StringComparer.Ordinal)));
        }
        if (mode == "SQLITE_SCHEMA")
        {
            if (!File.Exists(fullPath)) return "MISSING";
            var schema = await SqliteSchemaAnalyzer.ReadAsync(fullPath, cancellationToken);
            return HashText("SQLITE_SCHEMA\n" + string.Join('\n', schema));
        }
        if (!string.Equals(mode, "CONTENT", StringComparison.Ordinal)) throw new ArgumentException($"Unsupported evidence dependency mode '{mode}'.");

        if (File.Exists(fullPath))
        {
            return "FILE:" + await HashFileAsync(fullPath, cancellationToken);
        }
        if (!Directory.Exists(fullPath)) return "MISSING";

        var entries = new List<string>();
        var pending = new Queue<string>();
        pending.Enqueue(fullPath);
        var enumeration = new EnumerationOptions { RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = 0 };
        while (pending.Count > 0)
        {
            var directory = pending.Dequeue();
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", enumeration))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var repositoryRelative = Path.GetRelativePath(fullRoot, entry).Replace('\\', '/');
                if (repositoryRelative.Equals(".git", StringComparison.OrdinalIgnoreCase) || repositoryRelative.StartsWith(".git/", StringComparison.OrdinalIgnoreCase)) continue;
                if (relative == "." && (repositoryRelative.Equals(".arifce", StringComparison.OrdinalIgnoreCase) || repositoryRelative.StartsWith(".arifce/", StringComparison.OrdinalIgnoreCase))) continue;
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException($"Evidence scope contains symbolic link or reparse point '{repositoryRelative}'.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Enqueue(entry);
                else entries.Add(repositoryRelative + "\n" + await HashFileAsync(entry, cancellationToken));
            }
        }
        entries.Sort(StringComparer.Ordinal);
        var bytes = Encoding.UTF8.GetBytes("DIRECTORY\n" + string.Join('\n', entries));
        return "DIRECTORY:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
