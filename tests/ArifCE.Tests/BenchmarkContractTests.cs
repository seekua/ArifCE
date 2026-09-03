using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

// Explicitly approved echo probes exercise scope plumbing, not build/test or invariant correctness.
public sealed class BenchmarkContractTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-contract-benchmark-" + Guid.NewGuid().ToString("N"));
    private readonly ProjectService service = new(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());
    private const string Target = "src/Core/Payment.cs::Calculate";
    private const string Probe = "echo contract-probe > contract-probe.txt";

    [Fact]
    public async Task Contract_persists_linkage_confidence_history_and_risk_requirements()
    {
        await Seed();
        var decision = await service.CreateDecisionAsync(root, "Calculate rounding", "Calculate preserves rounding", "Invoice compatibility");
        foreach (var risk in new[] { RiskLevel.Low, RiskLevel.Medium, RiskLevel.High, RiskLevel.Critical })
        {
            var prior = Count("claims");
            var contract = await service.CreateChangeContractAsync(root, "Calculate", risk, ["Rounding unchanged"]);
            Assert.Equal(prior + 1, Count("claims"));
            Assert.Equal(JsonSerializer.Serialize(contract), JsonSerializer.Serialize(await service.GetChangeContractAsync(root, contract.Id)));
            var claim = (await service.GetClaimAsync(root, contract.ClaimId))!;
            Assert.StartsWith("CONTRACT-", contract.Id);
            Assert.StartsWith("CLAIM-", claim.Id);
            Assert.Equal(risk, claim.Risk);
            Assert.Equal(risk, contract.Risk);
            Assert.Equal(ClaimStatus.Unverified, claim.Status);
            Assert.Empty(claim.Evidence);
            Assert.Equal(WorkStatus.Open, contract.Status);
            Assert.Equal(new[] { "Rounding unchanged" }, contract.Invariants);
            Assert.Contains(contract.HistoricalRecords, path => path.Contains(decision.Id, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(contract.PotentialImpact, item => item.Path == "src/App/Consumer.cs" && item.Confidence == "HEURISTIC");
            Assert.Contains(contract.PotentialImpact, item => item.Path == "src/Core/Payment.cs" && item.Kind == "FILE" && item.Confidence == "STRUCTURAL");
            Assert.NotEmpty(contract.RelatedTests);
            Assert.All(contract.RelatedTests, item => Assert.Equal("HEURISTIC", item.Confidence));
            Assert.Equal(risk != RiskLevel.Low, contract.RequiredVerification.Any(text => text.Contains("BUILD", StringComparison.Ordinal)));
            Assert.Equal(risk != RiskLevel.Low, contract.RequiredVerification.Any(text => text.Contains("TEST_RUN", StringComparison.Ordinal)));
            Assert.Equal(risk is RiskLevel.High or RiskLevel.Critical, contract.RequiredVerification.Any(text => text.Contains("independent review", StringComparison.Ordinal)));
            Assert.Equal(risk == RiskLevel.Critical, contract.RequiredVerification.Any(text => text.Contains("human acceptance", StringComparison.Ordinal)));
        }
        Assert.Equal(4, Count("contracts"));
        Assert.Equal(0, Count("evidence"));
        Assert.Equal(0, Count("acceptances"));
    }

    [Fact]
    public async Task Contract_rejects_invalid_target_or_link_before_execution_and_writes()
    {
        await Seed();
        var contract = await service.CreateChangeContractAsync(root, Target, RiskLevel.Low);
        var other = await service.CreateClaimAsync(root, "Unrelated claim", RiskLevel.Low);
        var before = CanonicalHashes();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateChangeContractAsync(root, " ", RiskLevel.Low));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateChangeContractAsync(root, "Calc", RiskLevel.Low));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateChangeContractAsync(root, "MissingSymbol", RiskLevel.Low));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync(root, other.Id, Probe, true, contractId: contract.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.VerifyAsync(root, contract.ClaimId, Probe, true, contractId: "CONTRACT-9999"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.VerifyAsync(root, contract.ClaimId, Probe, true, scopePaths: ["../outside.txt"], contractId: contract.Id));
        Assert.False(File.Exists(Path.Combine(root, "contract-probe.txt")));
        Assert.Equal(before, CanonicalHashes());
    }

    [Fact]
    public async Task Contract_evidence_uses_qualified_scope_and_existing_acceptance_lifecycle()
    {
        await Seed();
        var contract = await service.CreateChangeContractAsync(root, Target, RiskLevel.Low);
        var contractPath = Path.Combine(root, ".arifce/contracts", contract.Id.ToLowerInvariant() + ".json");
        var originalContract = await File.ReadAllBytesAsync(contractPath);
        var verified = await service.VerifyAsync(root, contract.ClaimId, Probe, true, scopePaths: ["settings.txt"], contractId: contract.Id);
        Assert.True(File.Exists(Path.Combine(root, "contract-probe.txt")));
        Assert.Equal(0, verified.Evidence.ExitCode);
        Assert.Equal(ClaimStatus.Supported, (await service.GetClaimAsync(root, contract.ClaimId))!.Status);
        Assert.Equal(contract.Id, verified.Evidence.Scope!.ContractId);
        Assert.Equal(new[] { "settings.txt", "src/Core/Payment.cs" }, verified.Evidence.Scope.Dependencies.Where(item => item.Mode == "CONTENT").Select(item => item.Path));
        Assert.Contains(verified.Evidence.Scope.Dependencies, item => item.Mode == "CODE_GRAPH_CLOSURE" && item.Path == "symbol:" + Target);
        Assert.Contains(verified.Evidence.Id, (await service.GetClaimAsync(root, contract.ClaimId))!.Evidence);
        var persisted = await new CanonicalStore().ReadAsync<EvidenceRecord>(root, "evidence", verified.Evidence.Id);
        Assert.Equal(JsonSerializer.Serialize(verified.Evidence), JsonSerializer.Serialize(persisted));
        var accepted = await service.CreateAcceptanceAsync(root, contract.ClaimId, "fixture-owner", "Probe support only, not real build proof");
        Assert.Equal(new[] { verified.Evidence.Id }, accepted.EvidenceIds);
        await Write(".arifce/CURRENT.md", "Metadata only");
        await Write("src/Other/Other.cs", "class Other { public int Calculate() => 42; }");
        await Write("src/App/Consumer.cs", "// changed heuristic caller\nclass Consumer { public decimal Run() => new Payment().Calculate(20); }");
        File.Delete(Path.Combine(root, ".arifce/index/code-graph.json"));
        Assert.Equal(EvidenceFreshness.Current, await Freshness(verified.Evidence));
        Assert.Equal(0, (await service.RefreshTrustAsync(root)).AcceptancesFlagged);
        Assert.Equal(AcceptanceStatus.Accepted, (await service.GetAcceptanceAsync(root, accepted.Id))!.Status);
        await Write("settings.txt", "Changed explicit additional scope");
        Assert.Equal(EvidenceFreshness.Stale, await Freshness(verified.Evidence));
        await Write("settings.txt", "Original settings");
        await Write("src/Core/Payment.cs", "class Payment { public decimal Calculate(decimal value) => value + 1; }");
        Assert.Equal(EvidenceFreshness.Stale, await Freshness(verified.Evidence));
        var refresh = await service.RefreshTrustAsync(root);
        Assert.Equal(1, refresh.ClaimsStaled);
        Assert.Equal(1, refresh.AcceptancesFlagged);
        Assert.Equal(ClaimStatus.Stale, (await service.GetClaimAsync(root, contract.ClaimId))!.Status);
        Assert.Equal(AcceptanceStatus.NeedsReview, (await service.GetAcceptanceAsync(root, accepted.Id))!.Status);
        Assert.Equal(originalContract, await File.ReadAllBytesAsync(contractPath));
        Assert.Equal(1, Count("claims"));
        Assert.Equal(1, Count("contracts"));
    }

    [Fact]
    public async Task Contract_project_scope_tracks_exact_transitive_dependents()
    {
        await Seed();
        var contract = await service.CreateChangeContractAsync(root, "src/Core/Core.csproj::Core", RiskLevel.Low);
        Assert.Contains(contract.PotentialImpact, item => item.Path == "src/App/App.csproj" && item.Confidence == "EXACT");
        var verified = await service.VerifyAsync(root, contract.ClaimId, Probe, true, contractId: contract.Id);
        Assert.Equal(new[] { "src/App/App.csproj", "src/Core/Core.csproj", "src/Host/Host.csproj" }, verified.Evidence.Scope!.Dependencies.Where(item => item.Mode == "CONTENT").Select(item => item.Path));
        await Write("src/Other/Other.csproj", "<Project><!-- Unrelated edit --></Project>");
        Assert.Equal(EvidenceFreshness.Current, await Freshness(verified.Evidence));
        await Write("src/Host/Host.csproj", "<Project><ProjectReference Include=\"../App/App.csproj\" /><!-- Dependent edit --></Project>");
        Assert.Equal(EvidenceFreshness.Stale, await Freshness(verified.Evidence));
        var renewed = await service.VerifyAsync(root, contract.ClaimId, Probe, true, contractId: contract.Id);
        Assert.NotEqual(verified.Evidence.Id, renewed.Evidence.Id);
        Assert.Equal(EvidenceFreshness.Current, await Freshness(renewed.Evidence));
        await Write("src/New/New.csproj", "<Project><ProjectReference Include=\"../Core/Core.csproj\" /></Project>");
        Assert.Equal(EvidenceFreshness.Stale, await Freshness(renewed.Evidence));
        Assert.Equal(2, (await service.GetClaimAsync(root, contract.ClaimId))!.Evidence.Count);
    }

    private async Task<EvidenceFreshness> Freshness(EvidenceRecord evidence) => await EvidenceScopeTracker.EvaluateAsync(root, evidence, await new GitInspector().CaptureAsync(root));

    private async Task Seed()
    {
        Directory.CreateDirectory(root);
        using var git = Process.Start(new ProcessStartInfo("git", "init") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true });
        git!.WaitForExit();
        Assert.Equal(0, git.ExitCode);
        await service.InitializeAsync(root, false);
        await Write("src/Core/Payment.cs", "class Payment { public decimal Calculate(decimal value) => value; }");
        await Write("src/App/Consumer.cs", "class Consumer { public decimal Run() => new Payment().Calculate(10); }");
        await Write("src/Other/Other.cs", "class Other { public int Calculate() => 1; }");
        await Write("tests/PaymentTests.cs", "class PaymentTests { [Fact] public void Result() { new Payment().Calculate(10); } }");
        await Write("src/Core/Core.csproj", "<Project />");
        await Write("src/App/App.csproj", "<Project><ProjectReference Include=\"../Core/Core.csproj\" /></Project>");
        await Write("src/Host/Host.csproj", "<Project><ProjectReference Include=\"../App/App.csproj\" /></Project>");
        await Write("src/Other/Other.csproj", "<Project />");
        await Write("settings.txt", "Original settings");
    }

    private async Task Write(string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }
    private int Count(string kind) => Directory.Exists(Path.Combine(root, ".arifce", kind)) ? Directory.GetFiles(Path.Combine(root, ".arifce", kind), "*.json").Length : 0;
    private string[] CanonicalHashes() => Directory.EnumerateFiles(Path.Combine(root, ".arifce"), "*", SearchOption.AllDirectories)
        .Where(path => { var relative = Path.GetRelativePath(root, path).Replace('\\', '/'); return !relative.StartsWith(".arifce/index/", StringComparison.Ordinal) && !relative.StartsWith(".arifce/cache/", StringComparison.Ordinal); })
        .Order(StringComparer.Ordinal).Select(path => Path.GetRelativePath(root, path) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
