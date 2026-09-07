using System.Diagnostics;
using System.Security.Cryptography;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

public sealed class BenchmarkVerificationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-verification-benchmark-" + Guid.NewGuid().ToString("N"));
    private readonly ProjectService service = new(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());

    public BenchmarkVerificationTests()
    {
        Directory.CreateDirectory(root);
        using var process = Process.Start(new ProcessStartInfo("git", "init") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true });
        process!.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("Unable to initialize evaluator Git repository.");
    }

    [Fact]
    public async Task Named_help_command_never_becomes_verified_test_evidence()
    {
        await service.InitializeAsync(root, false);
        var claim = await service.CreateClaimAsync(root, "Tests passed", RiskLevel.Low);
        var result = await service.VerifyAsync(root, claim.Id, "dotnet test --help");
        Assert.Equal(0, result.Evidence.ExitCode);
        Assert.Equal("UNVERIFIED_COMMAND", result.Evidence.Kind);
        Assert.Equal(ClaimStatus.Supported, result.Claim.Status);
        var persisted = await new CanonicalStore().ReadAsync<ClaimRecord>(root, "claims", claim.Id);
        Assert.Equal(ClaimStatus.Supported, persisted!.Status);
    }

    [Fact]
    public async Task Unsafe_or_secret_commands_cannot_create_verified_or_partial_canonical_state()
    {
        await service.InitializeAsync(root, false);
        var claim = await service.CreateClaimAsync(root, "Safe command boundary", RiskLevel.Low);
        var before = Hashes();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync(root, claim.Id, "echo password=hunter2", true));
        Assert.Equal(before, Hashes());

        var unsafeSuccess = await service.VerifyAsync(root, claim.Id, OperatingSystem.IsWindows() ? "ver" : "true", true);
        Assert.Equal(0, unsafeSuccess.Evidence.ExitCode);
        Assert.Equal("UNSAFE_COMMAND", unsafeSuccess.Evidence.Kind);
        Assert.Equal(ClaimStatus.Supported, unsafeSuccess.Claim.Status);
        var persistedEvidence = await new CanonicalStore().ReadAsync<EvidenceRecord>(root, "evidence", unsafeSuccess.Evidence.Id);
        var persistedClaim = await new CanonicalStore().ReadAsync<ClaimRecord>(root, "claims", claim.Id);
        Assert.Equal("UNSAFE_COMMAND", persistedEvidence!.Kind);
        Assert.Equal(ClaimStatus.Supported, persistedClaim!.Status);
    }

    private Dictionary<string, string> Hashes() => Directory.EnumerateFiles(Path.Combine(root, ".arifce"), "*", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}index{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(path => Path.GetRelativePath(root, path), path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.OrdinalIgnoreCase);

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
