using System.Diagnostics;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

public sealed class BenchmarkFreshnessTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-freshness-evaluator-" + Guid.NewGuid().ToString("N"));
    private readonly GitInspector git = new();

    [Fact]
    public async Task Freshness_tracks_nested_untracked_bytes()
    {
        Initialize();
        Directory.CreateDirectory(Path.Combine(root, "new-code", "nested"));
        var path = Path.Combine(root, "new-code", "nested", "implementation.txt");
        await File.WriteAllTextAsync(path, "first implementation");
        var before = await git.CaptureAsync(root);
        await File.WriteAllTextAsync(path, "different implementation");
        var after = await git.CaptureAsync(root);
        AssertStale(before, after);
        Assert.Contains("new-code/nested/implementation.txt", after.ChangedFiles);
    }

    [Fact]
    public async Task Freshness_tracks_literal_paths_and_renames()
    {
        Initialize();
        var names = new List<string> { "café-evidence.txt", "two words.txt" };
        if (!OperatingSystem.IsWindows()) names.AddRange(new[] { "quote\"name.txt", "line\nbreak.txt", "arrow -> name.txt", " trailing-space " });
        foreach (var name in names)
        {
            var path = Path.Combine(root, name);
            await File.WriteAllTextAsync(path, "first content");
            var before = await git.CaptureAsync(root);
            await File.WriteAllTextAsync(path, "other content");
            var after = await git.CaptureAsync(root);
            AssertStale(before, after);
            Assert.Contains(name, after.ChangedFiles);
        }
        var priorRename = await git.CaptureAsync(root);
        RunGit("mv", "baseline.txt", "renamed file.txt");
        var renamed = await git.CaptureAsync(root);
        AssertStale(priorRename, renamed);
        Assert.Contains("baseline.txt", renamed.ChangedFiles);
        Assert.Contains("renamed file.txt", renamed.ChangedFiles);
        File.Delete(Path.Combine(root, "renamed file.txt"));
        AssertStale(renamed, await git.CaptureAsync(root));
    }

    [Fact]
    public async Task Freshness_distinguishes_current_stale_unknown_and_git_failure()
    {
        Initialize();
        var clean = await git.CaptureAsync(root);
        Assert.False(clean.IsDirty);
        Assert.Equal(EvidenceFreshness.Current, EvidenceEvaluator.Evaluate(clean, await git.CaptureAsync(root)));
        Directory.CreateDirectory(Path.Combine(root, ".arifce"));
        await File.WriteAllTextAsync(Path.Combine(root, ".arifce", "CURRENT.md"), "Internal metadata");
        Assert.Equal(clean.Digest, (await git.CaptureAsync(root)).Digest);
        await File.WriteAllTextAsync(Path.Combine(root, "baseline.txt"), "changed tracked bytes");
        var changed = await git.CaptureAsync(root);
        AssertStale(clean, changed);
        Assert.Equal(EvidenceFreshness.Current, EvidenceEvaluator.Evaluate(changed, await git.CaptureAsync(root)));
        await File.WriteAllTextAsync(Path.Combine(root, "baseline.txt"), "changed tracked bytes again");
        var changedAgain = await git.CaptureAsync(root);
        AssertStale(changed, changedAgain);
        File.Delete(Path.Combine(root, "baseline.txt"));
        var deleted = await git.CaptureAsync(root);
        AssertStale(changedAgain, deleted);
        RunGit("switch", "-c", "other");
        var branch = await git.CaptureAsync(root);
        AssertStale(deleted, branch);
        RunGit("checkout", "--detach", "HEAD");
        AssertStale(branch, await git.CaptureAsync(root));
        Assert.Equal(EvidenceFreshness.Unknown, EvidenceEvaluator.Evaluate(clean with { Digest = "" }, changed));
        Assert.Equal(EvidenceFreshness.Unknown, EvidenceEvaluator.Evaluate(clean, changed with { Digest = "" }));
        var outside = Path.Combine(root, "not-a-repository");
        Directory.CreateDirectory(outside);
        // Moving only this disposable fixture's Git metadata prevents ancestor-repository discovery.
        Directory.Move(Path.Combine(root, ".git"), Path.Combine(root, "git-metadata-disabled"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => git.CaptureAsync(outside));
    }

    [Fact]
    public async Task Freshness_rejects_unexpanded_git_directories()
    {
        Initialize();
        Directory.CreateDirectory(Path.Combine(root, "embedded"));
        RunGit("-C", "embedded", "init", "-b", "main");
        RunGit("-C", "embedded", "config", "user.name", "Fixture");
        RunGit("-C", "embedded", "config", "user.email", "fixture@example.invalid");
        RunGit("-C", "embedded", "commit", "--allow-empty", "-m", "Embedded fixture");
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => git.CaptureAsync(root));
        Assert.Contains("directory", failure.Message, StringComparison.Ordinal);
    }

    private static void AssertStale(GitSnapshot before, GitSnapshot after)
    {
        Assert.NotEqual(before.Digest, after.Digest);
        Assert.Equal(EvidenceFreshness.Stale, EvidenceEvaluator.Evaluate(before, after));
    }

    private void Initialize()
    {
        Directory.CreateDirectory(root);
        RunGit("init", "-b", "main");
        RunGit("config", "user.name", "Freshness fixture");
        RunGit("config", "user.email", "fixture@example.invalid");
        // Ensure Unicode quoting is enabled so a human-readable parser cannot accidentally pass.
        RunGit("config", "core.quotepath", "true");
        File.WriteAllText(Path.Combine(root, "baseline.txt"), "baseline");
        RunGit("add", "baseline.txt");
        RunGit("commit", "-m", "Fixture baseline");
    }

    private void RunGit(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
        Assert.True(process.ExitCode == 0, stderr.Result);
    }

    public void Dispose()
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }
}
