using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed record ContextAssemblyItem(
    string Path,
    string Kind,
    string Snippet,
    double Score,
    int Priority,
    string Freshness,
    int EstimatedTokens,
    bool Included,
    string Reason);

public sealed record ContextAssemblyTelemetry(
    int CandidateRecords,
    int SelectedRecords,
    int RejectedRecords,
    int CandidateTokens,
    int SelectedTokens,
    int BudgetRejected,
    int StaleRejected,
    int SupersededRejected,
    int InvalidRejected,
    long AssemblyMilliseconds);

public sealed record LlmContext(
    string Task,
    string Content,
    int EstimatedTokens,
    IReadOnlyList<string> Sources,
    IReadOnlyList<ContextAssemblyItem> Items,
    ContextAssemblyTelemetry Telemetry);

public sealed class LlmContextComposer(IndexStore index, GitInspector? git = null)
{
    private readonly GitInspector git = git ?? new GitInspector();

    public async Task<LlmContext> ComposeAsync(string root, string task, int budget = 4000, CancellationToken cancellationToken = default)
    {
        if (budget <= 0) throw new ArgumentOutOfRangeException(nameof(budget));
        var terms = Regex.Matches(task ?? string.Empty, "[A-Za-z0-9_]+", RegexOptions.CultureInvariant)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();
        if (terms.Length == 0) throw new ArgumentException("Context task must contain searchable terms.", nameof(task));

        var timer = Stopwatch.StartNew();
        await index.UpdateIncrementalAsync(root, cancellationToken);
        var snippetTokens = Math.Clamp(budget / Math.Max(terms.Length, 1), 20, 120);
        var hits = await index.SearchAsync(root, string.Join(" OR ", terms.Select(term => $"\"{term}\"")), 50, cancellationToken, snippetTokens);
        GitSnapshot? currentSnapshot = null;
        var candidates = new List<Candidate>(hits.Count);
        foreach (var hit in hits)
        {
            var kind = KindOf(hit.Path);
            if (kind is "CLAIM" or "EVIDENCE" or "ACCEPTANCE" && currentSnapshot is null) currentSnapshot = await git.CaptureAsync(root, cancellationToken);
            var trust = await AssessTrustAsync(root, hit.Path, kind, currentSnapshot, cancellationToken);
            candidates.Add(new Candidate(hit.Path, kind, hit.Snippet, hit.Score, PriorityOf(hit.Path, kind), trust));
        }

        var ordered = candidates
            .OrderByDescending(candidate => candidate.Priority)
            .ThenBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path, StringComparer.Ordinal)
            .ToArray();
        var items = new List<ContextAssemblyItem>(ordered.Length);
        var content = new StringBuilder();
        var selectedTokens = 0;
        var candidateTokens = 0;
        var budgetRejected = 0;
        var staleRejected = 0;
        var supersededRejected = 0;
        var invalidRejected = 0;

        foreach (var candidate in ordered)
        {
            var trustReason = candidate.Trust.Freshness == "CURRENT" ? "current trust" : candidate.Trust.Reason;
            var inclusionReason = $"lexical match; priority {candidate.Priority}; {trustReason}; fits token budget";
            var block = Render(candidate, inclusionReason);
            var estimate = EstimateTokens((content.Length == 0 ? string.Empty : Environment.NewLine + Environment.NewLine) + block);
            candidateTokens += estimate;

            if (!candidate.Trust.Include)
            {
                items.Add(ToItem(candidate, estimate, false, candidate.Trust.Reason));
                switch (candidate.Trust.Freshness)
                {
                    case "STALE": staleRejected++; break;
                    case "SUPERSEDED": supersededRejected++; break;
                    default: invalidRejected++; break;
                }
                continue;
            }

            if (selectedTokens + estimate > budget)
            {
                budgetRejected++;
                items.Add(ToItem(candidate, estimate, false, $"token budget exhausted: selecting this item would exceed {budget} tokens"));
                continue;
            }

            if (content.Length > 0) content.AppendLine().AppendLine();
            content.Append(block);
            selectedTokens += estimate;
            items.Add(ToItem(candidate, estimate, true, inclusionReason));
        }

        timer.Stop();
        var selected = items.Where(item => item.Included).ToArray();
        var telemetry = new ContextAssemblyTelemetry(
            items.Count,
            selected.Length,
            items.Count - selected.Length,
            candidateTokens,
            selectedTokens,
            budgetRejected,
            staleRejected,
            supersededRejected,
            invalidRejected,
            timer.ElapsedMilliseconds);
        return new LlmContext(task ?? string.Empty, content.ToString(), selectedTokens, selected.Select(item => item.Path).ToArray(), items, telemetry);
    }

    private static ContextAssemblyItem ToItem(Candidate candidate, int tokens, bool included, string reason) =>
        new(candidate.Path, candidate.Kind, candidate.Snippet, candidate.Score, candidate.Priority, candidate.Trust.Freshness, tokens, included, reason);

    private static string Render(Candidate candidate, string reason) =>
        $"[{candidate.Path}]\nKind: {candidate.Kind}\nFreshness: {candidate.Trust.Freshness}\nReason: {reason}\n{candidate.Snippet}";

    private static int EstimateTokens(string value) => Math.Max(1, (int)Math.Ceiling(value.Length / 4d));

    private static string KindOf(string path)
    {
        var separator = path.IndexOf('/');
        var value = separator < 0 ? path : path[..separator];
        return value.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(value).ToUpperInvariant()
            : value.TrimEnd('s').ToUpperInvariant();
    }

    private static int PriorityOf(string path, string kind) => (path.ToUpperInvariant(), kind) switch
    {
        ("CURRENT.MD", _) => 100,
        (_, "CONTRACT") => 95,
        (_, "ATTEMPT") => 90,
        (_, "TASK") => 88,
        (_, "CLAIM") => 85,
        (_, "DECISION") => 82,
        (_, "REFACTOR") => 80,
        (_, "FINDING") => 78,
        (_, "EVIDENCE") => 75,
        ("PROTOCOL.MD", _) => 70,
        ("PROJECT.MD", _) => 65,
        (_, "MEMORY") => 60,
        _ => 50
    };

    private static async Task<TrustAssessment> AssessTrustAsync(string root, string path, string kind, GitSnapshot? currentSnapshot, CancellationToken cancellationToken)
    {
        if (kind is not ("CLAIM" or "DECISION" or "EVIDENCE" or "ACCEPTANCE")) return TrustAssessment.Current;
        var canonicalRoot = Path.GetFullPath(Path.Combine(root, ".arifce"));
        var fullPath = Path.GetFullPath(Path.Combine(canonicalRoot, path.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(canonicalRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            return new(false, "INVALID", "canonical source is missing or outside the project intelligence store");

        try
        {
            var json = await File.ReadAllTextAsync(fullPath, cancellationToken);
            return kind switch
            {
                "CLAIM" => await AssessClaimAsync(root, JsonSerializer.Deserialize<ClaimRecord>(json, JsonDefaults.Options), currentSnapshot, cancellationToken),
                "DECISION" => AssessDecision(JsonSerializer.Deserialize<DecisionRecord>(json, JsonDefaults.Options)),
                "EVIDENCE" => await AssessEvidenceAsync(root, JsonSerializer.Deserialize<EvidenceRecord>(json, JsonDefaults.Options), currentSnapshot, cancellationToken),
                "ACCEPTANCE" => await AssessAcceptanceAsync(root, JsonSerializer.Deserialize<AcceptanceRecord>(json, JsonDefaults.Options), currentSnapshot, cancellationToken),
                _ => TrustAssessment.Current
            };
        }
        catch (JsonException)
        {
            return new(false, "INVALID", "canonical record is malformed and cannot be trusted");
        }
    }

    private static async Task<TrustAssessment> AssessClaimAsync(string root, ClaimRecord? claim, GitSnapshot? currentSnapshot, CancellationToken cancellationToken)
    {
        if (claim is null) return new(false, "INVALID", "claim record is empty or invalid");
        if (claim.Status == ClaimStatus.Stale) return new(false, "STALE", "claim is stale and requires re-verification");
        if (claim.Status == ClaimStatus.Contradicted) return new(false, "INVALID", "claim is contradicted by recorded evidence");
        if (claim.Status == ClaimStatus.Disputed) return new(true, "DISPUTED", "claim is disputed; include only as an explicit warning");
        if (claim.Status == ClaimStatus.Unverified) return new(true, "UNVERIFIED", "claim is unverified; include only as an explicit warning");
        if (currentSnapshot is null || claim.Evidence.Count == 0) return new(false, "STALE", "supported claim has no current evidence");
        var hasCurrentEvidence = false;
        foreach (var evidenceId in claim.Evidence)
        {
            var path = Path.Combine(root, ".arifce", "evidence", evidenceId.ToLowerInvariant() + ".json");
            if (!File.Exists(path)) continue;
            try
            {
                var evidence = JsonSerializer.Deserialize<EvidenceRecord>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options);
                if (evidence is not null && await EvidenceScopeTracker.EvaluateAsync(root, evidence, currentSnapshot, cancellationToken) == EvidenceFreshness.Current) hasCurrentEvidence = true;
            }
            catch (JsonException) { }
        }
        return hasCurrentEvidence ? TrustAssessment.Current : new(false, "STALE", "claim has no evidence matching the current dependency or repository state");
    }

    private static TrustAssessment AssessDecision(DecisionRecord? decision)
    {
        if (decision is null) return new(false, "INVALID", "decision record is empty or invalid");
        return !string.IsNullOrWhiteSpace(decision.SupersededBy) || string.Equals(decision.Status, "SUPERSEDED", StringComparison.OrdinalIgnoreCase)
            ? new(false, "SUPERSEDED", $"decision was superseded{(string.IsNullOrWhiteSpace(decision.SupersededBy) ? string.Empty : $" by {decision.SupersededBy}")}")
            : string.Equals(decision.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                ? TrustAssessment.Current
                : new(false, "INVALID", $"decision status {decision.Status} is not active");
    }

    private static async Task<TrustAssessment> AssessEvidenceAsync(string root, EvidenceRecord? evidence, GitSnapshot? currentSnapshot, CancellationToken cancellationToken)
    {
        if (evidence is null || currentSnapshot is null) return new(false, "INVALID", "evidence record or current repository snapshot is unavailable");
        return await EvidenceScopeTracker.EvaluateAsync(root, evidence, currentSnapshot, cancellationToken) switch
        {
            EvidenceFreshness.Current => TrustAssessment.Current,
            EvidenceFreshness.Stale => new(false, "STALE", "evidence snapshot no longer matches the repository state"),
            _ => new(true, "UNKNOWN", "evidence freshness is unknown; include only as an explicit warning")
        };
    }

    private static async Task<TrustAssessment> AssessAcceptanceAsync(string root, AcceptanceRecord? acceptance, GitSnapshot? currentSnapshot, CancellationToken cancellationToken)
    {
        if (acceptance is null) return new(false, "INVALID", "acceptance record is empty or invalid");
        if (acceptance.Status == AcceptanceStatus.NeedsReview) return new(false, "STALE", "acceptance requires review because its trust chain changed");
        if (acceptance.Status != AcceptanceStatus.Accepted) return new(false, "INVALID", $"acceptance status {acceptance.Status} is not accepted");
        if (currentSnapshot is null || acceptance.EvidenceIds.Count == 0) return new(false, "STALE", "acceptance has no current evidence");
        foreach (var evidenceId in acceptance.EvidenceIds)
        {
            var path = Path.Combine(root, ".arifce", "evidence", evidenceId.ToLowerInvariant() + ".json");
            if (!File.Exists(path)) return new(false, "STALE", $"acceptance evidence {evidenceId} is missing");
            try
            {
                var evidence = JsonSerializer.Deserialize<EvidenceRecord>(await File.ReadAllTextAsync(path, cancellationToken), JsonDefaults.Options);
                if (evidence is null || await EvidenceScopeTracker.EvaluateAsync(root, evidence, currentSnapshot, cancellationToken) != EvidenceFreshness.Current)
                    return new(false, "STALE", $"acceptance evidence {evidenceId} is no longer current");
            }
            catch (JsonException) { return new(false, "STALE", $"acceptance evidence {evidenceId} is malformed"); }
        }
        return TrustAssessment.Current;
    }

    private sealed record Candidate(string Path, string Kind, string Snippet, double Score, int Priority, TrustAssessment Trust);
    private sealed record TrustAssessment(bool Include, string Freshness, string Reason)
    {
        public static readonly TrustAssessment Current = new(true, "CURRENT", "record is current");
    }
}
