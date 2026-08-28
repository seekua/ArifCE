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
        var lines = status.Output.Replace("\r", "", StringComparison.Ordinal).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var branchLine = lines.FirstOrDefault(x => x.StartsWith("## ", StringComparison.Ordinal));
        var branch = branchLine?[3..].Split("...", StringSplitOptions.None)[0];
        if (branch?.StartsWith("No commits yet on ", StringComparison.Ordinal) == true) branch = branch[18..];
        var changed = lines.Where(x => !x.StartsWith("## ", StringComparison.Ordinal)).Select(x => x.Length > 3 ? x[3..].Trim() : x.Trim()).Order(StringComparer.Ordinal).ToArray();
        var head = await RunAsync(root, "rev-parse HEAD", cancellationToken);
        var commit = head.ExitCode == 0 ? head.Output.Trim() : null;
        var normalized = $"{commit}\n{branch}\n{string.Join('\n', changed)}";
        return new GitSnapshot(commit, branch, changed.Length > 0, changed, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant());
    }

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
        await File.AppendAllTextAsync(path, line, new UTF8Encoding(false), cancellationToken);
    }

    public async IAsyncEnumerable<JournalEvent> ReadAsync(string root, bool recovery = false, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(root, ".arifce", "journal", "events.jsonl");
        if (!File.Exists(path)) yield break;
        var lines = await File.ReadAllLinesAsync(path, cancellationToken);
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            JournalEvent? item;
            try { item = JsonSerializer.Deserialize<JournalEvent>(lines[i], JournalOptions); }
            catch (JsonException) when (recovery || i == lines.Length - 1) { continue; }
            catch (JsonException exception) { throw new InvalidDataException($"Journal line {i + 1} is corrupt.", exception); }
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
}

public sealed class CanonicalStore
{
    public static readonly string[] EntityDirectories = ["decisions", "tasks", "attempts", "checkpoints", "claims", "acceptances", "evidence", "reviews", "findings", "refactors", "handoffs", "runs", "threads"];

    public async Task WriteAsync<T>(string root, string directory, string id, T value, CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(root, ".arifce", directory);
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, id.ToLowerInvariant() + ".json");
        var temporary = target + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, JsonDefaults.Options), new UTF8Encoding(false), cancellationToken);
        File.Move(temporary, target, true);
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
        if (!Directory.Exists(folder)) return $"{prefix}-0001";
        var maximum = Directory.EnumerateFiles(folder, $"{prefix.ToLowerInvariant()}-*.json").Select(Path.GetFileNameWithoutExtension).Select(x => int.TryParse(x?[(prefix.Length + 1)..], out var n) ? n : 0).DefaultIfEmpty().Max();
        return $"{prefix}-{maximum + 1:0000}";
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

    public (string Text, int Count) Redact(string input)
    {
        var count = 0;
        string Replace(Match match, string replacement) { count++; return replacement; }
        var text = AssignmentPattern().Replace(input, m => Replace(m, $"{m.Groups[1].Value}=[REDACTED]"));
        text = BearerPattern().Replace(text, m => Replace(m, "Bearer [REDACTED]"));
        text = PrivateKeyPattern().Replace(text, m => Replace(m, "[REDACTED PRIVATE KEY]"));
        return (text, count);
    }
}

public sealed class IndexStore
{
    private static string ConnectionString(string root) => new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, ".arifce", "index", "arifce.db"), Pooling = false }.ToString();

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
    }

    public async Task<IReadOnlyList<(string Path, string Snippet, double Score)>> SearchAsync(string root, string query, int limit = 20, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString(root));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, snippet(search,3,'[',']',' … ',20), bm25(search) FROM search WHERE search MATCH $query ORDER BY bm25(search), path LIMIT $limit";
        command.Parameters.AddWithValue("$query", query); command.Parameters.AddWithValue("$limit", limit);
        var results = new List<(string, string, double)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) results.Add((reader.GetString(0), reader.GetString(1), reader.GetDouble(2)));
        return results;
    }

    private static bool IsCanonical(string path) => !path.Contains($"{Path.DirectorySeparatorChar}index{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && !path.Contains($"{Path.DirectorySeparatorChar}cache{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && !path.Contains($"{Path.DirectorySeparatorChar}raw{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) && (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(cancellationToken); }
}
