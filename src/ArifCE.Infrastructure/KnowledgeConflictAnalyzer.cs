using System.Text.Json;
using System.Text.RegularExpressions;
using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed record KnowledgeIssue(string Code, string Kind, string Severity, IReadOnlyList<string> EntityIds, string Summary, string Recommendation);
public sealed record KnowledgeAuditResult(IReadOnlyList<KnowledgeIssue> Issues)
{
    public int Blocking => Issues.Count(issue => issue.Severity == "BLOCKING");
    public int Warnings => Issues.Count(issue => issue.Severity == "WARNING");
}

public static partial class KnowledgeConflictAnalyzer
{
    public static async Task<KnowledgeAuditResult> AuditAsync(string root, CancellationToken cancellationToken = default)
    {
        var issues = new List<(string Kind, string Severity, IReadOnlyList<string> Ids, string Summary, string Recommendation)>();
        var decisions = await ReadAsync<DecisionRecord>(root, "decisions", issues, cancellationToken);
        var claims = await ReadAsync<ClaimRecord>(root, "claims", issues, cancellationToken);

        foreach (var group in decisions.Where(IsActive).GroupBy(decision => NormalizeText(decision.Title)).Where(group => group.Key.Length > 0 && group.Count() > 1).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(decision => decision.Id, StringComparer.Ordinal).ToArray();
            for (var left = 0; left < ordered.Length - 1; left++)
            for (var right = left + 1; right < ordered.Length; right++)
            {
                var ids = new[] { ordered[left].Id, ordered[right].Id };
                if (NormalizeText(ordered[left].Decision) == NormalizeText(ordered[right].Decision))
                    issues.Add(("DUPLICATE_DECISION", "WARNING", ids, $"Active decisions {ids[0]} and {ids[1]} repeat the same title and decision.", $"Review provenance, then supersede {ids[1]} by {ids[0]} if they are equivalent."));
                else
                    issues.Add(("CONFLICTING_DECISION", "BLOCKING", ids, $"Active decisions {ids[0]} and {ids[1]} share a title but prescribe different outcomes.", "Resolve the conflict explicitly and supersede the rejected decision; do not choose silently."));
            }
        }

        var decisionById = decisions.ToDictionary(decision => decision.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var decision in decisions.OrderBy(decision => decision.Id, StringComparer.Ordinal))
        {
            if (string.Equals(decision.Status, "SUPERSEDED", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(decision.SupersededBy))
                issues.Add(("BROKEN_SUPERSESSION", "BLOCKING", [decision.Id], $"Decision {decision.Id} is superseded but has no replacement ID.", "Record the active replacement decision ID."));
            if (!string.IsNullOrWhiteSpace(decision.SupersededBy) && (!decisionById.TryGetValue(decision.SupersededBy, out var replacement) || !IsActive(replacement)))
                issues.Add(("BROKEN_SUPERSESSION", "BLOCKING", [decision.Id, decision.SupersededBy], $"Decision {decision.Id} points to a missing or inactive replacement {decision.SupersededBy}.", "Point supersededBy to an existing active decision."));
        }

        foreach (var group in claims.GroupBy(claim => NormalizeText(claim.Statement)).Where(group => group.Key.Length > 0 && group.Count() > 1).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(claim => claim.Id, StringComparer.Ordinal).ToArray();
            var ids = ordered.Select(claim => claim.Id).ToArray();
            var hasPositive = ordered.Any(claim => claim.Status is ClaimStatus.Supported or ClaimStatus.PartiallyVerified or ClaimStatus.Verified);
            var hasNegative = ordered.Any(claim => claim.Status == ClaimStatus.Contradicted);
            issues.Add(hasPositive && hasNegative
                ? ("CONFLICTING_CLAIM", "BLOCKING", ids, $"Equivalent claims {string.Join(", ", ids)} have both supported and contradicted states.", "Inspect current evidence and reconcile the claims without deleting history.")
                : ("DUPLICATE_CLAIM", "WARNING", ids, $"Equivalent claim statements are recorded more than once: {string.Join(", ", ids)}.", "Consolidate future work around one claim and preserve the others as history."));
        }

        var result = issues
            .OrderByDescending(issue => issue.Severity == "BLOCKING")
            .ThenBy(issue => issue.Kind, StringComparer.Ordinal)
            .ThenBy(issue => string.Join('|', issue.Ids), StringComparer.Ordinal)
            .Select((issue, index) => new KnowledgeIssue($"KNOWLEDGE-{index + 1:000}", issue.Kind, issue.Severity, issue.Ids, issue.Summary, issue.Recommendation))
            .ToArray();
        return new KnowledgeAuditResult(result);
    }

    private static bool IsActive(DecisionRecord decision) => string.Equals(decision.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(decision.SupersededBy);

    private static async Task<IReadOnlyList<T>> ReadAsync<T>(string root, string directory, List<(string Kind, string Severity, IReadOnlyList<string> Ids, string Summary, string Recommendation)> issues, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, ".arifce", directory);
        if (!Directory.Exists(path)) return [];
        var records = new List<T>();
        foreach (var file in Directory.EnumerateFiles(path, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var record = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file, cancellationToken), JsonDefaults.Options);
                if (record is null) throw new JsonException("Empty record.");
                records.Add(record);
            }
            catch (JsonException)
            {
                var id = Path.GetFileNameWithoutExtension(file).ToUpperInvariant();
                issues.Add(("MALFORMED_RECORD", "BLOCKING", [id], $"Canonical {directory} record {Path.GetFileName(file)} is malformed.", "Repair the canonical record from repository history before trusting it."));
            }
        }
        return records;
    }

    public static string NormalizeText(string value) => string.Join(' ', Word().Matches(value ?? string.Empty).Select(match => match.Value.ToLowerInvariant()));

    [GeneratedRegex(@"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex Word();
}
