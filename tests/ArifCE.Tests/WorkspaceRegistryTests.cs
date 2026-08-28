using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

public sealed class WorkspaceRegistryTests
{
    [Fact]
    public async Task AddListAndRemoveAreLocalAndIdempotenceIsGuarded()
    {
        var root = Directory.CreateTempSubdirectory("arifce-workspace-");
        var registryPath = Path.Combine(root.FullName, "workspace.json");
        var projectRoot = Directory.CreateDirectory(Path.Combine(root.FullName, "project")).FullName;
        try
        {
            var registry = new WorkspaceRegistry(registryPath);
            var added = await registry.AddAsync("Demo", projectRoot);
            Assert.Equal("Demo", added.Name);
            Assert.Single(await registry.ListAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => registry.AddAsync("Duplicate", projectRoot));
            await registry.RemoveAsync(projectRoot);
            Assert.Empty(await registry.ListAsync());
            Assert.True(Directory.Exists(projectRoot));
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task AddRejectsMissingRoot()
    {
        var root = Directory.CreateTempSubdirectory("arifce-workspace-");
        try
        {
            var registry = new WorkspaceRegistry(Path.Combine(root.FullName, "workspace.json"));
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() => registry.AddAsync("Missing", Path.Combine(root.FullName, "missing")));
        }
        finally { root.Delete(true); }
    }
}
