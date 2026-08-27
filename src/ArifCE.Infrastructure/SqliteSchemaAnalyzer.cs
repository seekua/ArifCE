using Microsoft.Data.Sqlite;

namespace ArifCE.Infrastructure;

public sealed record SqliteSchemaDiff(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed)
{
    public bool IsCompatible => Removed.Count == 0 && Changed.Count == 0;
}

public static class SqliteSchemaAnalyzer
{
    public static async Task<IReadOnlyList<string>> ReadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) throw new ArgumentException($"Database '{path}' does not exist.");
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync(ct); var entries = new List<string>();
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT type,name,sql FROM sqlite_master WHERE sql IS NOT NULL ORDER BY type,name";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) entries.Add($"{reader.GetString(0)}|{reader.GetString(1)}|{reader.GetString(2).Replace("\r", "", StringComparison.Ordinal).Replace("\n", " ").Trim()}");
        return entries;
    }

    public static SqliteSchemaDiff Compare(IReadOnlyList<string> baseline, IReadOnlyList<string> current)
    {
        var before = baseline.ToHashSet(StringComparer.Ordinal); var after = current.ToHashSet(StringComparer.Ordinal);
        return new(after.Except(before).Order(StringComparer.Ordinal).ToArray(), before.Except(after).Order(StringComparer.Ordinal).ToArray(), []);
    }
}
