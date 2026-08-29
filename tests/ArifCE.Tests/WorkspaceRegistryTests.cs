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
            Assert.Equal(projectRoot, await registry.SetActiveAsync(projectRoot));
            Assert.Equal(projectRoot, await registry.GetActiveAsync());
            Assert.Equal("Demo", added.Name);
            Assert.Single(await registry.ListAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => registry.AddAsync("Duplicate", projectRoot));
            await Assert.ThrowsAsync<InvalidOperationException>(() => registry.AddAsync("Duplicate normalized", projectRoot + Path.DirectorySeparatorChar));
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

    [Fact]
    public async Task AddRejectsBlankNameAndRemoveMissingRootIsSafe()
    {
        var root = Directory.CreateTempSubdirectory("arifce-workspace-");
        try
        {
            var registry = new WorkspaceRegistry(Path.Combine(root.FullName, "workspace.json"));
            await Assert.ThrowsAsync<ArgumentException>(() => registry.AddAsync(" ", root.FullName));
            await Assert.ThrowsAsync<InvalidOperationException>(() => registry.SetActiveAsync(root.FullName));
            await registry.RemoveAsync(Path.Combine(root.FullName, "not-registered"));
            Assert.Empty(await registry.ListAsync());
        }
        finally { root.Delete(true); }
    }

    [Fact]
    public async Task RemovingActiveProjectClearsSelectionAndRegistriesRemainIsolated()
    {
        var root = Directory.CreateTempSubdirectory("arifce-workspace-");
        try
        {
            var projectA = Directory.CreateDirectory(Path.Combine(root.FullName, "a")).FullName;
            var projectB = Directory.CreateDirectory(Path.Combine(root.FullName, "b")).FullName;
            var registry = new WorkspaceRegistry(Path.Combine(root.FullName, "workspace.json"));
            await registry.AddAsync("A", projectA);
            await registry.AddAsync("B", projectB);
            await registry.SetActiveAsync(projectA);
            await registry.RemoveAsync(projectA);
            Assert.Null(await registry.GetActiveAsync());
            Assert.Single(await registry.ListAsync());
            Assert.Equal(projectB, (await registry.ListAsync())[0].Root);
            Assert.True(Directory.Exists(projectA));
            Assert.True(Directory.Exists(projectB));
        }
        finally { root.Delete(true); }
    }
}
