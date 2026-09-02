using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArifCE.Core;
using Microsoft.Data.Sqlite;

namespace ArifCE.Infrastructure;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new FlexibleEnumConverterFactory()); return options;
    }
}

public sealed class ProjectLocator
{
    public string FindRoot(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || Directory.Exists(Path.Combine(directory.FullName, ".arifce"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("No Git or ArifCE project was found in this directory or its parents.");
    }
}

public sealed class GitInspector
{
    public async Task<GitSnapshot> CaptureAsync(string root, CancellationToken cancellationToken = default)
    {
        var status = await RunAsync(root, "status --porcelain=v1 -b", cancellationToken);
        if (status.ExitCode != 0)
        {
            throw new InvalidOperationException("Unable to capture repository state because git status failed.");
        }
        var lines = status.Output.Replace("\r", "", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var branchLine = lines.FirstOrDefault(x => x.StartsWith("## ", StringComparison.Ordinal));
        var branch = branchLine?[3..].Split("...", StringSplitOptions.None)[0];
        if (branch?.StartsWith("No commits yet on ", StringComparison.Ordinal) == true) branch = branch[18..];
        var changed = lines.Where(x => !x.StartsWith("## ", StringComparison.Ordinal)).Select(x => x.Length > 3 ? x[3..].Trim() : x.Trim()).Where(path => !IsInternalArifcePath(path)).Order(StringComparer.Ordinal).ToArray();
        var head = await RunAsync(root, "rev-parse HEAD", cancellationToken);
        var commit = head.ExitCode == 0 ? head.Output.Trim() : null;
        // A path-only fingerprint misses edits made in a dirty worktree. Include the
        // bytes of every changed path (and an explicit missing marker) so evidence
        // cannot remain current after an in-place edit, delete, or rename.
        var content = changed.SelectMany(path => SnapshotPath(root, path)).Order(StringComparer.Ordinal).ToArray();
        var normalized = $"{commit}\n{branch}\n{string.Join('\n', content)}";
        return new GitSnapshot(commit, branch, changed.Length > 0, changed, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant());
    }

    private static IEnumerable<string> SnapshotPath(string root, string statusPath)
    {
        var paths = statusPath.Contains(" -> ", StringComparison.Ordinal)
            ? statusPath.Split(" -> ", 2, StringSplitOptions.None)
            : [statusPath];
        foreach (var path in paths)
        {
            var relative = path.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(relative)) continue;
            var full = Path.GetFullPath(Path.Combine(root, relative));
            if (!File.Exists(full))
            {
                yield return $"{relative}\n<MISSING>";
                continue;
            }

            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant();
            yield return $"{relative}\n{hash}";
        }
    }

    private static bool IsInternalArifcePath(string statusPath) => statusPath.Split(" -> ", StringSplitOptions.None)
        .All(path => path.Trim().Trim('"').Replace('\\', '/').StartsWith(".arifce/", StringComparison.OrdinalIgnoreCase));

    private static async Task<(int ExitCode, string Output)> RunAsync(string root, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo("git", arguments) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, output);
    }
}

public sealed class JournalStore
{
    private static readonly JsonSerializerOptions JournalOptions = new(JsonDefaults.Options) { WriteIndented = false };

    public async Task AppendAsync(string root, JournalEvent value, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var line = JsonSerializer.Serialize(value, JournalOptions) + Environment.NewLine;
        await using var mutationLock = await FileMutationLock.AcquireAsync(root, "journal", "events", cancellationToken);
        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        var bytes = Encoding.UTF8.GetBytes(line);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public async IAsyncEnumerable<JournalEvent> ReadAsync(string root, bool recovery = false, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        if (!File.Exists(path)) yield break;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            JournalEvent? item;
            try { item = JsonSerializer.Deserialize<JournalEvent>(line, JournalOptions); }
            catch (JsonException) when (recovery || reader.Peek() == -1) { continue; }
            catch (JsonException exception) { throw new InvalidDataException($"Journal line {lineNumber} is corrupt.", exception); }
            if (item is not null) yield return item;
        }
    }

    public async Task<IReadOnlyList<string>> InspectAsync(string root, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        if (!File.Exists(path)) return [];
        var lines = await File.ReadAllLinesAsync(path, cancellationToken); var issues = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            try { using var _ = JsonDocument.Parse(lines[i]); }
            catch (JsonException) { issues.Add(i == lines.Length - 1 ? $"Journal line {i + 1} is a partial or corrupt final line." : $"Journal line {i + 1} is corrupt."); }
        }
        return issues;
    }

    public async Task<(string BackupPath, int Kept, int Removed)?> RepairAsync(string root, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        if (!File.Exists(path)) return null;
        var lines = await File.ReadAllLinesAsync(path, cancellationToken); var valid = new List<string>(); var removed = 0;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try { using var _ = JsonDocument.Parse(line); valid.Add(line); }
            catch (JsonException) { removed++; }
        }
        if (removed == 0) return null;
        var backupDirectory = Path.Combine(root, ".arifce", "backups", "journal"); Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, $"events-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.jsonl.bak");
        File.Copy(path, backup, false);
        var temporary = path + ".repair.tmp";
        await File.WriteAllTextAsync(temporary, valid.Count == 0 ? "" : string.Join(Environment.NewLine, valid) + Environment.NewLine, new UTF8Encoding(false), cancellationToken);
        File.Move(temporary, path, true);
        return (backup, valid.Count, removed);
    }

    public async Task<string?> RotateAsync(string root, long maxBytes = 5_000_000, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        if (!File.Exists(path) || new FileInfo(path).Length <= maxBytes) return null;
        var backupDirectory = Path.Combine(root, ".arifce", "backups", "journal"); Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, $"events-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.jsonl.archive");
        File.Move(path, backup, false);
        await File.WriteAllTextAsync(path, string.Empty, new UTF8Encoding(false), cancellationToken);
        return backup;
    }
}

public sealed class CanonicalStore
{
    public static readonly string[] EntityDirectories = ["decisions", "tasks", "attempts", "checkpoints", "claims", "acceptances", "evidence", "reviews", "findings", "refactors", "contracts", "handoffs", "runs", "threads"];

    public async Task WriteAsync<T>(string root, string directory, string id, T value, CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await FileMutationLock.AcquireAsync(root, directory, id, cancellationToken);
        await WriteCoreAsync(root, directory, id, value, cancellationToken);
    }

    public async Task<T> UpdateAsync<T>(string root, string directory, string id, Func<T, T> update, CancellationToken cancellationToken = default)
    {
        await using var mutationLock = await FileMutationLock.AcquireAsync(root, directory, id, cancellationToken);
        var current = await ReadAsync<T>(root, directory, id, cancellationToken) ?? throw new InvalidOperationException($"{id} was not found.");
        var updated = update(current);
        await WriteCoreAsync(root, directory, id, updated, cancellationToken);
        return updated;
    }

    public async Task<T?> ReadAsync<T>(string root, string directory, string id, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, ".arifce", directory, id.ToLowerInvariant() + ".json");
        if (!File.Exists(path)) return default;
        return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options);
    }

    public string NextId(string root, string directory, string prefix)
    {
        var folder = Path.Combine(root, ".arifce", directory);
        Directory.CreateDirectory(folder);
        var prefixPattern = prefix.ToLowerInvariant() + "-";
        var maximum = Directory.EnumerateFiles(folder, $"{prefixPattern}*.json*")
            .Select(path => Path.GetFileName(path)?.Split('.')[0])
            .Select(x => int.TryParse(x?[(prefix.Length + 1)..], out var n) ? n : 0)
            .DefaultIfEmpty().Max();
        for (var number = maximum + 1; ; number++)
        {
            var id = $"{prefix}-{number:0000}";
            var reservation = Path.Combine(folder, id.ToLowerInvariant() + ".json.reserve");
            try { using var _ = new FileStream(reservation, FileMode.CreateNew, FileAccess.Write, FileShare.None); return id; }
            catch (IOException) { }
        }
    }

    private static async Task WriteCoreAsync<T>(string root, string directory, string id, T value, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(root, ".arifce", directory);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, id.ToLowerInvariant() + ".json");
        var reservation = target + ".reserve";
        var temporary = target + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, JsonDefaults.Options), new UTF8Encoding(false), cancellationToken);
        try { File.Move(temporary, target, true); if (File.Exists(reservation)) File.Delete(reservation); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal static class FileMutationLock
{
    public static async Task<FileStream> AcquireAsync(string root, string category, string id, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(root, ".arifce", "cache", "locks", category);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, id.ToLowerInvariant() + ".lock");
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, useAsync: true); }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline) { await Task.Delay(25, cancellationToken); }
            catch (UnauthorizedAccessException) when (DateTimeOffset.UtcNow < deadline) { await Task.Delay(25, cancellationToken); }
            if (DateTimeOffset.UtcNow >= deadline) throw new IOException($"Timed out waiting for the canonical mutation lock '{category}/{id}'.");
        }
    }
}

public sealed partial class SecretRedactor
{
    [GeneratedRegex(@"(?i)(password|pwd|api[_-]?key|secret)\s*[=:]\s*([^\s;]+)")]
    private static partial Regex AssignmentPattern();
    [GeneratedRegex(@"(?i)bearer\s+[a-z0-9._~+/=-]+")]
    private static partial Regex BearerPattern();
    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----")]
    private static partial Regex PrivateKeyPattern();
    [GeneratedRegex(@"(?i)\b(?:AKIA|ASIA)[A-Z0-9]{16}\b|\bgh[pousr]_[A-Za-z0-9_]{20,}\b|\bsk-(?:proj-)?[A-Za-z0-9_-]{16,}\b|\bxox[baprs]-[A-Za-z0-9-]{16,}\b|\bglpat-[A-Za-z0-9_-]{16,}\b|\bAIza[A-Za-z0-9_-]{20,}\b")]
    private static partial Regex ProviderTokenPattern();
    [GeneratedRegex(@"(?i)\b(?:access[_-]?token|refresh[_-]?token|credential|auth|passwd|private[_-]?key)\s*[=:]\s*([^\s;]+)")]
    private static partial Regex CredentialAssignmentPattern();
    [GeneratedRegex(@"\b[A-Za-z][A-Za-z0-9+.-]*://[^\s:/]+:[^\s@/]+@[^\s]+")]
    private static partial Regex ConnectionStringPattern();

    public (string Text, int Count) Redact(string input)
    {
        var count = 0;
        string Replace(Match match, string replacement) { count++; return replacement; }
        var text = AssignmentPattern().Replace(input, m => Replace(m, $"{m.Groups[1].Value}=[REDACTED]"));
        text = BearerPattern().Replace(text, m => Replace(m, "Bearer [REDACTED]"));
        text = PrivateKeyPattern().Replace(text, m => Replace(m, "[REDACTED PRIVATE KEY]"));
        text = CredentialAssignmentPattern().Replace(text, m => Replace(m, $"{m.Value[..m.Value.IndexOfAny(['=', ':'])]}=[REDACTED]"));
        text = ProviderTokenPattern().Replace(text, m => Replace(m, "[REDACTED TOKEN]"));
        text = ConnectionStringPattern().Replace(text, m => Replace(m, "[REDACTED CONNECTION STRING]"));
        return (text, count);
    }
}

public sealed class IndexStore
{
    private static string ConnectionString(string root) => new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, ".arifce", "index", "arifce.db"), Pooling = false }.ToString();
    private sealed record ManifestEntry(string Path, string Hash);

    public async Task RebuildAsync(string root, CancellationToken cancellationToken = default)
    {
        var indexDirectory = Path.Combine(root, ".arifce", "index");
        Directory.CreateDirectory(indexDirectory);
        var database = Path.Combine(indexDirectory, "arifce.db");
        if (File.Exists(database)) File.Delete(database);
        await using var connection = new SqliteConnection(ConnectionString(root));
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "CREATE TABLE entities(id TEXT PRIMARY KEY, kind TEXT NOT NULL, path TEXT NOT NULL, content TEXT NOT NULL); CREATE VIRTUAL TABLE search USING fts5(id UNINDEXED, kind, path, content);", cancellationToken);
        var store = Path.Combine(root, ".arifce");
        foreach (var path in Directory.EnumerateFiles(store, "*", SearchOption.AllDirectories).Where(IsCanonical).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(store, path).Replace('\\', '/');
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            var kind = relative.Split('/')[0];
            var id = Path.GetFileNameWithoutExtension(path);
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO entities VALUES ($id,$kind,$path,$content); INSERT INTO search VALUES ($id,$kind,$path,$content);";
            command.Parameters.AddWithValue("$id", id); command.Parameters.AddWithValue("$kind", kind); command.Parameters.AddWithValue("$path", relative); command.Parameters.AddWithValue("$content", content);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await WriteManifestAsync(root, cancellationToken);
    }

    public async Task UpdateIncrementalAsync(string root, CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(root, ".arifce", "index", "manifest.json");
        var database = Path.Combine(root, ".arifce", "index", "arifce.db");
        if (!File.Exists(database) || !File.Exists(manifestPath)) { await RebuildAsync(root, cancellationToken); return; }
        List<ManifestEntry> previous;
        try { previous = JsonSerializer.Deserialize<List<ManifestEntry>>(await File.ReadAllTextAsync(manifestPath, cancellationToken), JsonDefaults.Options) ?? []; }
        catch (JsonException) { await RebuildAsync(root, cancellationToken); return; }
        var current = CanonicalFiles(root).ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
        var old = previous.ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqliteConnection(ConnectionString(root)); await connection.OpenAsync(cancellationToken);
        var changed = old.Count != current.Count || old.Any(pair => !current.TryGetValue(pair.Key, out var entry) || entry.Hash != pair.Value.Hash);
        foreach (var removed in old.Keys.Except(current.Keys, StringComparer.OrdinalIgnoreCase))
        {
            await using var command = connection.CreateCommand(); command.CommandText = "DELETE FROM search WHERE path=$path"; command.Parameters.AddWithValue("$path", removed); await command.ExecuteNonQueryAsync(cancellationToken);
            await using var entityCommand = connection.CreateCommand(); entityCommand.CommandText = "DELETE FROM entities WHERE path=$path"; entityCommand.Parameters.AddWithValue("$path", removed); await entityCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (var entry in current.Values)
        {
            if (old.TryGetValue(entry.Path, out var prior) && prior.Hash == entry.Hash) continue;
            var content = await File.ReadAllTextAsync(Path.Combine(root, ".arifce", entry.Path), cancellationToken); var id = Path.GetFileNameWithoutExtension(entry.Path); var kind = entry.Path.Split('/')[0];
            await using var deleteSearch = connection.CreateCommand(); deleteSearch.CommandText = "DELETE FROM search WHERE path=$path"; deleteSearch.Parameters.AddWithValue("$path", entry.Path); await deleteSearch.ExecuteNonQueryAsync(cancellationToken);
            await using var deleteEntity = connection.CreateCommand(); deleteEntity.CommandText = "DELETE FROM entities WHERE path=$path"; deleteEntity.Parameters.AddWithValue("$path", entry.Path); await deleteEntity.ExecuteNonQueryAsync(cancellationToken);
            await using var insertEntity = connection.CreateCommand(); insertEntity.CommandText = "INSERT INTO entities VALUES ($id,$kind,$path,$content)"; insertEntity.Parameters.AddWithValue("$id", id); insertEntity.Parameters.AddWithValue("$kind", kind); insertEntity.Parameters.AddWithValue("$path", entry.Path); insertEntity.Parameters.AddWithValue("$content", content); await insertEntity.ExecuteNonQueryAsync(cancellationToken);
            await using var insertSearch = connection.CreateCommand(); insertSearch.CommandText = "INSERT INTO search VALUES ($id,$kind,$path,$content)"; insertSearch.Parameters.AddWithValue("$id", id); insertSearch.Parameters.AddWithValue("$kind", kind); insertSearch.Parameters.AddWithValue("$path", entry.Path); insertSearch.Parameters.AddWithValue("$content", content); await insertSearch.ExecuteNonQueryAsync(cancellationToken);
        }
        // SQLite FTS5 maintains its own index; rebuild only the FTS projection when the manifest changed.
        // Canonical entity rows above remain delta-updated.
        if (changed)
        {
            await using var clearSearch = connection.CreateCommand(); clearSearch.CommandText = "DROP TABLE search; CREATE VIRTUAL TABLE search USING fts5(id UNINDEXED, kind, path, content)"; await clearSearch.ExecuteNonQueryAsync(cancellationToken);
            foreach (var entry in current.Values)
            {
                var content = await File.ReadAllTextAsync(Path.Combine(root, ".arifce", entry.Path), cancellationToken);
                await using var insert = connection.CreateCommand(); insert.CommandText = "INSERT INTO search VALUES ($id,$kind,$path,$content)"; insert.Parameters.AddWithValue("$id", Path.GetFileNameWithoutExtension(entry.Path)); insert.Parameters.AddWithValue("$kind", entry.Path.Split('/')[0]); insert.Parameters.AddWithValue("$path", entry.Path); insert.Parameters.AddWithValue("$content", content); await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await WriteManifestAsync(root, cancellationToken);
    }

    public async Task<IReadOnlyList<(string Path, string Snippet, double Score)>> SearchAsync(string root, string query, int limit = 20, CancellationToken cancellationToken = default, int snippetTokens = 20)
    {
        await using var connection = new SqliteConnection(ConnectionString(root));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, snippet(search,3,'[',']',' … ',$snippetTokens), bm25(search) FROM search WHERE search MATCH $query ORDER BY bm25(search), path LIMIT $limit";
        command.Parameters.AddWithValue("$query", ToSafeQuery(query)); command.Parameters.AddWithValue("$limit", limit); command.Parameters.AddWithValue("$snippetTokens", Math.Clamp(snippetTokens, 8, 160));
        var results = new List<(string, string, double)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add((reader.GetString(0), reader.GetString(1), reader.GetDouble(2)));
        return results;
    }

    private static string ToSafeQuery(string query)
    {
        var stopWords = new HashSet<string>(["a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "in", "is", "it", "of", "on", "or", "the", "to", "with"], StringComparer.OrdinalIgnoreCase);
        var terms = System.Text.RegularExpressions.Regex.Matches(query ?? string.Empty, "[A-Za-z0-9_]+", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .Where(value => value.Length > 0)
            .Where(value => !stopWords.Contains(value) || value.Length > 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .Select(value => $"\"{value.Replace("\"", "\"\"")}\"")
            .ToArray();
        return terms.Length == 0 ? "\"\"" : string.Join(" OR ", terms);
    }

    private static bool IsCanonical(string path) => !path.Contains($"{Path.DirectorySeparatorChar}index{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && !path.Contains($"{Path.DirectorySeparatorChar}cache{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && !path.Contains($"{Path.DirectorySeparatorChar}raw{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    private static IEnumerable<ManifestEntry> CanonicalFiles(string root) => Directory.EnumerateFiles(Path.Combine(root, ".arifce"), "*", SearchOption.AllDirectories).Where(IsCanonical).Select(path => new ManifestEntry(Path.GetRelativePath(Path.Combine(root, ".arifce"), path).Replace('\\', '/'), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))))).OrderBy(x => x.Path, StringComparer.Ordinal);
    private async Task WriteManifestAsync(string root, CancellationToken ct) => await File.WriteAllTextAsync(Path.Combine(root, ".arifce", "index", "manifest.json"), JsonSerializer.Serialize(CanonicalFiles(root), JsonDefaults.Options), new UTF8Encoding(false), ct);
    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(cancellationToken); }
}
