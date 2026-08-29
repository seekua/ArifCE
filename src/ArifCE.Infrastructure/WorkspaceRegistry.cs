using System.Text.Json;

namespace ArifCE.Infrastructure;

public sealed record WorkspaceProject(string Name, string Root, DateTimeOffset LastSeenUtc);

/// <summary>Local-only registry of project roots. It never stores project records or secrets.</summary>
public sealed class WorkspaceRegistry
{
    private readonly string _path;
    private string ActivePath => Path.Combine(Path.GetDirectoryName(_path)!, "active-project.txt");

    public WorkspaceRegistry(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArifCE", "workspace.json");
    }

    public async Task<IReadOnlyList<WorkspaceProject>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return Array.Empty<WorkspaceProject>();
        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<List<WorkspaceProject>>(stream, JsonDefaults.Options, cancellationToken) ?? [];
    }

    public async Task<WorkspaceProject> AddAsync(string name, string root, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Project name is required.", nameof(name));
        var normalized = NormalizeRoot(root);
        if (!Directory.Exists(normalized)) throw new DirectoryNotFoundException(normalized);
        var projects = (await ListAsync(cancellationToken)).ToList();
        if (projects.Any(x => string.Equals(x.Root, normalized, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("That project root is already registered.");
        var project = new WorkspaceProject(name.Trim(), normalized, DateTimeOffset.UtcNow);
        projects.Add(project);
        await SaveAsync(projects, cancellationToken);
        return project;
    }

    public async Task RemoveAsync(string root, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRoot(root);
        var projects = (await ListAsync(cancellationToken)).ToList();
        projects.RemoveAll(x => string.Equals(x.Root, normalized, StringComparison.OrdinalIgnoreCase));
        await SaveAsync(projects, cancellationToken);
        if (string.Equals(await GetActiveAsync(cancellationToken), normalized, StringComparison.OrdinalIgnoreCase) && File.Exists(ActivePath))
            File.Delete(ActivePath);
    }

    public async Task<string?> GetActiveAsync(CancellationToken cancellationToken = default)
        => File.Exists(ActivePath) ? (await File.ReadAllTextAsync(ActivePath, cancellationToken)).Trim() : null;

    public async Task<string> SetActiveAsync(string root, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRoot(root);
        var projects = await ListAsync(cancellationToken);
        if (!projects.Any(x => string.Equals(x.Root, normalized, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("The project root must be registered before it can be active.");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(ActivePath, normalized, cancellationToken);
        return normalized;
    }

    private async Task SaveAsync(List<WorkspaceProject> projects, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, projects, JsonDefaults.Options, cancellationToken);
    }

    private static string NormalizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Project root is required.", nameof(root));
        return Path.GetFullPath(root.Trim()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
