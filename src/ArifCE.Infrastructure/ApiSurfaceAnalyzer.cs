using System.Reflection;
using System.Text.Json;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed record ApiSurfaceDiff(IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed)
{
    public bool IsCompatible => Removed.Count == 0 && Changed.Count == 0;
}

public static class ApiSurfaceAnalyzer
{
    public static IReadOnlyList<string> Read(string assemblyPath)
    {
        if (!File.Exists(assemblyPath)) throw new ArgumentException($"Assembly '{assemblyPath}' does not exist.");
        var assembly = Assembly.LoadFrom(assemblyPath);
        return assembly.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal).SelectMany(TypeEntries).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    public static ApiSurfaceDiff Compare(IReadOnlyList<string> baseline, IReadOnlyList<string> current)
    {
        var before = baseline.ToHashSet(StringComparer.Ordinal); var after = current.ToHashSet(StringComparer.Ordinal);
        return new(after.Except(before).Order(StringComparer.Ordinal).ToArray(), before.Except(after).Order(StringComparer.Ordinal).ToArray(), []);
    }

    public static async Task WriteBaselineAsync(string path, IReadOnlyList<string> entries, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(entries, JsonDefaults.Options), ct);
        File.Move(temp, path, true);
    }

    public static async Task<IReadOnlyList<string>> ReadBaselineAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) throw new ArgumentException($"API baseline '{path}' does not exist.");
        return await JsonSerializer.DeserializeAsync<string[]>(File.OpenRead(path), JsonDefaults.Options, ct) ?? [];
    }

    private static IEnumerable<string> TypeEntries(Type type)
    {
        yield return $"type {type.FullName}";
        foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => m.MemberType is MemberTypes.Method or MemberTypes.Property or MemberTypes.Field or MemberTypes.Event or MemberTypes.Constructor).OrderBy(m => m.ToString(), StringComparer.Ordinal))
            yield return $"{type.FullName}::{member}";
    }
}
