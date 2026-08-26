using System.Text.Json.Serialization;

namespace ArifCE.Core;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ClaimStatus { Unverified, Supported, PartiallyVerified, Verified, Disputed, Contradicted, Stale }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EvidenceFreshness { Current, Stale, Unknown }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorkStatus { Open, InProgress, Blocked, Completed, Abandoned }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RiskLevel { Low, Medium, High, Critical }

public sealed record GitSnapshot(string? Commit, string? Branch, bool IsDirty, IReadOnlyList<string> ChangedFiles, string Digest);
public sealed record TaskRecord(int SchemaVersion, string Id, string Title, string? Description, WorkStatus Status, RiskLevel Risk, DateTimeOffset CreatedAtUtc);
public sealed record ClaimRecord(int SchemaVersion, string Id, string Statement, ClaimStatus Status, RiskLevel Risk, GitSnapshot Snapshot, IReadOnlyList<string> Evidence, DateTimeOffset CreatedAtUtc);
public sealed record EvidenceMetrics(int? Total, int? Passed, int? Failed, int? Skipped, int? Warnings = null, int? Errors = null);
public sealed record EvidenceRecord(int SchemaVersion, string Id, string ClaimId, string Kind, string? Command, int? ExitCode, string Summary, GitSnapshot Snapshot, DateTimeOffset CreatedAtUtc, EvidenceMetrics? Metrics = null);
public sealed record CheckpointRecord(int SchemaVersion, string Id, string Summary, GitSnapshot Snapshot, DateTimeOffset CreatedAtUtc);
public sealed record HandoffRecord(int SchemaVersion, string Id, string Markdown, GitSnapshot Snapshot, DateTimeOffset CreatedAtUtc);
public sealed record RefactorGuard(string Kind, string Value, bool Blocking);
public sealed record RefactorCampaign(int SchemaVersion, string Id, string Title, string Objective, WorkStatus Status, IReadOnlyList<string> Invariants, IReadOnlyList<string> Inventory, IReadOnlyList<RefactorGuard> Guards, DateTimeOffset CreatedAtUtc);
public sealed record JournalEvent(int SchemaVersion, string EventId, string Type, DateTimeOffset OccurredAtUtc, string EntityId, object Data);

public static class ClaimTransitions
{
    public static bool IsAllowed(ClaimStatus from, ClaimStatus to) => from == to || (from, to) switch
    {
        (_, ClaimStatus.Contradicted) => true,
        (ClaimStatus.Unverified, ClaimStatus.Supported or ClaimStatus.PartiallyVerified or ClaimStatus.Verified or ClaimStatus.Disputed) => true,
        (ClaimStatus.Supported or ClaimStatus.PartiallyVerified or ClaimStatus.Verified, ClaimStatus.Stale or ClaimStatus.Disputed) => true,
        (ClaimStatus.Stale or ClaimStatus.Disputed or ClaimStatus.Contradicted, ClaimStatus.Unverified or ClaimStatus.Supported or ClaimStatus.PartiallyVerified or ClaimStatus.Verified) => true,
        _ => false
    };
}

public static class EvidenceEvaluator
{
    public static EvidenceFreshness Evaluate(GitSnapshot recorded, GitSnapshot current) =>
        string.IsNullOrWhiteSpace(recorded.Digest) || string.IsNullOrWhiteSpace(current.Digest)
            ? EvidenceFreshness.Unknown
            : string.Equals(recorded.Digest, current.Digest, StringComparison.Ordinal)
                ? EvidenceFreshness.Current
                : EvidenceFreshness.Stale;
}
