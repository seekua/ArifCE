using System.Diagnostics;
using ArifCE.Core;
using ArifCE.Infrastructure;
using Xunit;

namespace ArifCE.Tests;

// Synthetic evidence exercises lifecycle rules, not provenance of an actual build or review.
public sealed class BenchmarkPropagationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "arifce-propagation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Current_metadata_and_scoped_changes_preserve_only_valid_trust()
    {
        foreach (var status in new[] { ClaimStatus.Supported, ClaimStatus.PartiallyVerified, ClaimStatus.Verified })
        {
            var fixture = await Seed(status.ToString(), status);
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, ".arifce", "CURRENT.md"), "Metadata only");
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "unrelated.txt"), "Outside explicit scope");
            Assert.Equal(0, (await fixture.Service.RefreshTrustAsync(fixture.Root)).ClaimsStaled);
            Assert.Equal(status, (await fixture.Service.GetClaimAsync(fixture.Root, fixture.Claim.Id))!.Status);
            Assert.Equal(AcceptanceStatus.Accepted, (await fixture.Service.GetAcceptanceAsync(fixture.Root, fixture.Acceptance.Id))!.Status);
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "source.txt"), "Changed dependency");
            var refresh = await fixture.Service.RefreshTrustAsync(fixture.Root);
            Assert.Equal(1, refresh.ClaimsStaled);
            Assert.Equal(1, refresh.AcceptancesFlagged);
            Assert.Equal(ClaimStatus.Stale, (await fixture.Service.GetClaimAsync(fixture.Root, fixture.Claim.Id))!.Status);
            Assert.Equal(AcceptanceStatus.NeedsReview, (await fixture.Service.GetAcceptanceAsync(fixture.Root, fixture.Acceptance.Id))!.Status);
            var again = await fixture.Service.RefreshTrustAsync(fixture.Root);
            Assert.Equal(0, again.ClaimsStaled);
            Assert.Equal(0, again.AcceptancesFlagged);
        }
    }

    [Fact]
    public async Task Acceptance_keeps_its_original_evidence_basis_after_new_evidence()
    {
        var fixture = await Seed("mixed", ClaimStatus.Supported);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "source.txt"), "Changed after acceptance");
        var fresh = fixture.Evidence with
        {
            Id = "EVIDENCE-0002",
            Snapshot = await new GitInspector().CaptureAsync(fixture.Root),
            Scope = await EvidenceScopeTracker.CaptureAsync(fixture.Root, new[] { "source.txt" })
        };
        await fixture.Store.WriteAsync(fixture.Root, "evidence", fresh.Id, fresh);
        await fixture.Store.UpdateAsync<ClaimRecord>(fixture.Root, "claims", fixture.Claim.Id, claim => claim with { Evidence = claim.Evidence.Append(fresh.Id).ToArray() });
        var refresh = await fixture.Service.RefreshTrustAsync(fixture.Root);
        Assert.Equal(0, refresh.ClaimsStaled); // The claim has new current support, but the old approval does not.
        Assert.Equal(1, refresh.AcceptancesFlagged);
        Assert.Equal(ClaimStatus.Supported, (await fixture.Service.GetClaimAsync(fixture.Root, fixture.Claim.Id))!.Status);
        Assert.Equal(AcceptanceStatus.NeedsReview, (await fixture.Service.GetAcceptanceAsync(fixture.Root, fixture.Acceptance.Id))!.Status);
        var replacement = await fixture.Service.CreateAcceptanceAsync(fixture.Root, fixture.Claim.Id, "fixture-owner", "Explicit renewed approval");
        Assert.Equal(new[] { fresh.Id }, replacement.EvidenceIds);
        var revoked = await fixture.Service.CreateAcceptanceAsync(fixture.Root, fixture.Claim.Id, "fixture-owner", "Will revoke");
        await fixture.Service.RevokeAcceptanceAsync(fixture.Root, revoked.Id);
        await fixture.Service.RefreshTrustAsync(fixture.Root);
        Assert.Equal(AcceptanceStatus.Accepted, (await fixture.Service.GetAcceptanceAsync(fixture.Root, replacement.Id))!.Status);
        Assert.Equal(AcceptanceStatus.Revoked, (await fixture.Service.GetAcceptanceAsync(fixture.Root, revoked.Id))!.Status);
        var handoff = await fixture.Service.HandoffAsync(fixture.Root);
        Assert.Contains(fixture.Acceptance.Id, TrustSection(handoff.Markdown));
        Assert.Equal(AcceptanceStatus.NeedsReview, (await fixture.Service.GetAcceptanceAsync(fixture.Root, fixture.Acceptance.Id))!.Status);
    }

    [Fact]
    public async Task Broken_evidence_or_claim_cannot_leave_acceptance_current()
    {
        foreach (var scenario in new[] { "missing-evidence", "malformed-evidence", "foreign-evidence", "unknown-snapshot", "missing-claim", "malformed-claim", "contradicted-claim" })
        {
            var fixture = await Seed(scenario, ClaimStatus.Supported);
            var evidencePath = Path.Combine(fixture.Root, ".arifce", "evidence", "evidence-0001.json");
            var claimPath = Path.Combine(fixture.Root, ".arifce", "claims", fixture.Claim.Id.ToLowerInvariant() + ".json");
            switch (scenario)
            {
                case "missing-evidence": File.Delete(evidencePath); break;
                case "malformed-evidence": await File.WriteAllTextAsync(evidencePath, "{broken"); break;
                case "foreign-evidence": await fixture.Store.WriteAsync(fixture.Root, "evidence", fixture.Evidence.Id, fixture.Evidence with { ClaimId = "CLAIM-9999" }); break;
                case "unknown-snapshot": await fixture.Store.WriteAsync(fixture.Root, "evidence", fixture.Evidence.Id, fixture.Evidence with { Scope = null, Snapshot = fixture.Evidence.Snapshot with { Digest = "" } }); break;
                case "missing-claim": File.Delete(claimPath); break;
                case "malformed-claim": await File.WriteAllTextAsync(claimPath, "{broken"); break;
                case "contradicted-claim": await fixture.Store.UpdateAsync<ClaimRecord>(fixture.Root, "claims", fixture.Claim.Id, claim => claim with { Status = ClaimStatus.Contradicted }); break;
            }
            var refresh = await fixture.Service.RefreshTrustAsync(fixture.Root);
            Assert.Equal(1, refresh.AcceptancesFlagged);
            Assert.Equal(AcceptanceStatus.NeedsReview, (await fixture.Service.GetAcceptanceAsync(fixture.Root, fixture.Acceptance.Id))!.Status);
            Assert.Contains(fixture.Acceptance.Id, TrustSection((await fixture.Service.HandoffAsync(fixture.Root)).Markdown));
        }
    }

    [Fact]
    public async Task Handoff_refreshes_and_repeats_warnings_without_promoting_stale_claims()
    {
        var fixture = await Seed("handoff", ClaimStatus.Verified, scoped: false);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, ".arifce", "CURRENT.md"), "Metadata only");
        var unchanged = await fixture.Service.HandoffAsync(fixture.Root);
        Assert.Contains("No stale trust relationships", TrustSection(unchanged.Markdown));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "source.txt"), "Source changed");
        var changed = await fixture.Service.HandoffAsync(fixture.Root); // No manual refresh prerequisite.
        Assert.Contains(fixture.Claim.Id, TrustSection(changed.Markdown));
        Assert.Contains(fixture.Acceptance.Id, TrustSection(changed.Markdown));
        Assert.Equal(ClaimStatus.Stale, (await fixture.Service.GetClaimAsync(fixture.Root, fixture.Claim.Id))!.Status);
        // Restoring bytes or adding fresh evidence alone must not silently re-approve history.
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "source.txt"), "Original source");
        var again = await fixture.Service.HandoffAsync(fixture.Root);
        Assert.Contains(fixture.Acceptance.Id, TrustSection(again.Markdown));
        Assert.Equal(ClaimStatus.Stale, (await fixture.Service.GetClaimAsync(fixture.Root, fixture.Claim.Id))!.Status);
        Assert.Equal(AcceptanceStatus.NeedsReview, (await fixture.Service.GetAcceptanceAsync(fixture.Root, fixture.Acceptance.Id))!.Status);
        Assert.Equal(again.Markdown, (await fixture.Store.ReadAsync<HandoffRecord>(fixture.Root, "handoffs", again.Id))!.Markdown);
    }

    [Fact]
    public async Task New_acceptance_rejects_foreign_evidence_and_disputed_claim()
    {
        foreach (var scenario in new[] { "foreign", "disputed" })
        {
            var fixture = await Seed("new-" + scenario, ClaimStatus.Supported);
            if (scenario == "foreign")
                await fixture.Store.WriteAsync(fixture.Root, "evidence", fixture.Evidence.Id, fixture.Evidence with { ClaimId = "CLAIM-9999" });
            else
                await fixture.Store.UpdateAsync<ClaimRecord>(fixture.Root, "claims", fixture.Claim.Id, claim => claim with { Status = ClaimStatus.Disputed });
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.CreateAcceptanceAsync(fixture.Root, fixture.Claim.Id, "fixture-owner", "Must not accept"));
            Assert.Single(Directory.GetFiles(Path.Combine(fixture.Root, ".arifce", "acceptances"), "*.json"));
        }
    }

    private static string TrustSection(string markdown) => markdown.Split("## Trust Warnings", StringSplitOptions.None)[1].Split("## Knowledge Warnings", StringSplitOptions.None)[0];

    private async Task<Fixture> Seed(string name, ClaimStatus status, bool scoped = true)
    {
        var directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        using var git = Process.Start(new ProcessStartInfo("git", "init") { WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true });
        git!.WaitForExit();
        Assert.Equal(0, git.ExitCode);
        await File.WriteAllTextAsync(Path.Combine(directory, "source.txt"), "Original source");
        var store = new CanonicalStore();
        var service = new ProjectService(store, new JournalStore(), new IndexStore(), new GitInspector());
        await service.InitializeAsync(directory, false);
        var claim = await service.CreateClaimAsync(directory, "Synthetic propagation fixture", RiskLevel.Low);
        var evidence = new EvidenceRecord(1, "EVIDENCE-0001", claim.Id, "FIXTURE", null, 0, "Synthetic support, not a build run", await new GitInspector().CaptureAsync(directory), DateTimeOffset.UtcNow,
            Scope: scoped ? await EvidenceScopeTracker.CaptureAsync(directory, new[] { "source.txt" }) : null);
        await store.WriteAsync(directory, "evidence", evidence.Id, evidence);
        claim = await store.UpdateAsync<ClaimRecord>(directory, "claims", claim.Id, value => value with { Status = status, Evidence = new[] { evidence.Id } });
        var acceptance = await service.CreateAcceptanceAsync(directory, claim.Id, "fixture-owner", "Synthetic policy fixture");
        return new(directory, store, service, claim, evidence, acceptance);
    }

    private sealed record Fixture(string Root, CanonicalStore Store, ProjectService Service, ClaimRecord Claim, EvidenceRecord Evidence, AcceptanceRecord Acceptance);
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
}
