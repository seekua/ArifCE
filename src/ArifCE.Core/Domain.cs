namespace ArifCE.Core;

public enum ClaimStatus { Unverified, Supported, PartiallyVerified, Verified, Disputed, Contradicted, Stale }
public enum EvidenceFreshness { Current, Stale, Unknown }
public enum WorkStatus { Open, InProgress, Blocked, Completed, Abandoned }
public enum RiskLevel { Low, Medium, High, Critical }
public enum ReviewPhase { IndependentInspection, Reconciliation }
public enum ReviewVerdict { Agree, PartiallyAgree, Disagree, Inconclusive }
public enum AcceptanceStatus { Pending, Accepted, Rejected, Revoked, NeedsReview }

public sealed record GitSnapshot(string? Commit, string? Branch, bool IsDirty, IReadOnlyList<string> ChangedFiles, string Digest);
public sealed record TaskRecord(int SchemaVersion, string Id, string Title, string? Description, WorkStatus Status, RiskLevel Risk, DateTimeOffset CreatedAtUtc);
public sealed record DecisionRecord(int SchemaVersion, string Id, string Title, string Decision, string HistoricalRationale, string Status, string Provenance, string? SupersededBy, DateTimeOffset CreatedAtUtc);
public sealed record AttemptRecord(int SchemaVersion, string Id, string TaskId, string Approach, string Result, string Reason, IReadOnlyList<string> EvidenceIds, DateTimeOffset CreatedAtUtc);
public sealed record ClaimRecord(int SchemaVersion, string Id, string Statement, ClaimStatus Status, RiskLevel Risk, GitSnapshot Snapshot, IReadOnlyList<string> Evidence, DateTimeOffset CreatedAtUtc);
public sealed record AcceptanceRecord(int SchemaVersion, string Id, string ClaimId, string Actor, AcceptanceStatus Status, string Rationale, GitSnapshot Snapshot, IReadOnlyList<string> EvidenceIds, DateTimeOffset CreatedAtUtc, DateTimeOffset? RevokedAtUtc = null);
public sealed record EvidenceMetrics(int? Total, int? Passed, int? Failed, int? Skipped, int? Warnings = null, int? Errors = null);
public sealed record EvidenceRecord(int SchemaVersion, string Id, string ClaimId, string Kind, string? Command, int? ExitCode, string Summary, GitSnapshot Snapshot, DateTimeOffset CreatedAtUtc, EvidenceMetrics? Metrics = null);
public sealed record CheckpointRecord(int SchemaVersion, string Id, string Summary, GitSnapshot Snapshot, DateTimeOffset CreatedAtUtc);
public sealed record HandoffRecord(int SchemaVersion, string Id, string Markdown, GitSnapshot Snapshot, DateTimeOffset CreatedAtUtc);
public sealed record RefactorGuard(string Kind, string Value, bool Blocking);
public sealed record RefactorWorkstream(string Name, string Owner, IReadOnlyList<string> Paths, WorkStatus Status);
public sealed record RefactorSafePoint(string Name, GitSnapshot Snapshot, string? Notes, DateTimeOffset CreatedAtUtc);
public sealed record RefactorCampaign(int SchemaVersion, string Id, string Title, string Objective, WorkStatus Status, IReadOnlyList<string> Invariants, IReadOnlyList<string> Inventory, IReadOnlyList<RefactorGuard> Guards, DateTimeOffset CreatedAtUtc, IReadOnlyList<RefactorWorkstream>? Workstreams = null, IReadOnlyList<RefactorSafePoint>? SafePoints = null);
public sealed record BlindReviewRequest(int SchemaVersion, string Id, ReviewPhase Phase, string Task, IReadOnlyList<string> AcceptanceCriteria, GitSnapshot Snapshot, string RelevantDiff, IReadOnlyList<string> EvidenceIds, IReadOnlyList<string> Constraints, string? BuilderClaim);
public sealed record SemanticReviewResult(int SchemaVersion, string Id, string RequestId, string Reviewer, ReviewVerdict Verdict, string Summary, IReadOnlyList<string> FindingIds, DateTimeOffset CreatedAtUtc);
public sealed record FindingRecord(int SchemaVersion, string Id, string Title, string Description, RiskLevel Severity, WorkStatus Status, string? TaskId, string? Path, DateTimeOffset CreatedAtUtc);
public sealed record ReviewRecord(int SchemaVersion, string Id, string ClaimId, string Reviewer, ReviewVerdict Verdict, string Summary, IReadOnlyList<string> FindingIds, GitSnapshot Snapshot, DateTimeOffset CreatedAtUtc);
public sealed record VerificationRequirements(bool Build, bool Tests, bool IndependentReview, bool HumanApproval);
public sealed record JournalEvent(int SchemaVersion, string EventId, string Type, DateTimeOffset OccurredAtUtc, string EntityId, object Data);
public sealed record TrustRefreshResult(int ClaimsStaled, int AcceptancesFlagged, IReadOnlyList<string> Warnings);

public interface ISemanticReviewAdapter
{
    string AdapterId { get; }
    Task<SemanticReviewResult> ReviewAsync(BlindReviewRequest request, CancellationToken cancellationToken = default);
}

public static class BlindReviewProtocol
{
    public static IReadOnlyList<string> Validate(BlindReviewRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Task)) errors.Add("Task is required.");
        if (request.AcceptanceCriteria.Count == 0) errors.Add("At least one acceptance criterion is required.");
        if (request.Phase == ReviewPhase.IndependentInspection && !string.IsNullOrWhiteSpace(request.BuilderClaim)) errors.Add("Independent inspection must not include the builder claim.");
        if (request.Phase == ReviewPhase.Reconciliation && string.IsNullOrWhiteSpace(request.BuilderClaim)) errors.Add("Reconciliation requires the builder claim.");
        return errors;
    }
}

public static class VerificationPolicy
{
    public static VerificationRequirements For(RiskLevel risk) => risk switch
    {
        RiskLevel.Low => new(false, false, false, false),
        RiskLevel.Medium => new(true, true, false, false),
        RiskLevel.High => new(true, true, true, false),
        RiskLevel.Critical => new(true, true, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(risk), risk, null)
    };
}

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
