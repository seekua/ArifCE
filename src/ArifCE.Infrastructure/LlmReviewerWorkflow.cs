using ArifCE.Core;

namespace ArifCE.Infrastructure;

public sealed record ReviewerApproval(string ClaimId, string Reviewer, string Rationale, DateTimeOffset ApprovedAtUtc);
public sealed record LlmReviewExecution(LlmExecutionResult Execution, ReviewerApproval Approval);

/// <summary>Runs an LLM reviewer only after an explicit local approval is supplied.</summary>
public sealed class LlmReviewerWorkflow
{
    private readonly LlmOrchestrator _orchestrator;
    private readonly CanonicalStore _canonical;
    public LlmReviewerWorkflow(LlmOrchestrator orchestrator, CanonicalStore canonical) { _orchestrator = orchestrator; _canonical = canonical; }

    public async Task<LlmReviewExecution> RunAsync(string root, string claimId, string reviewer, string rationale, string prompt, bool approved, CancellationToken cancellationToken = default)
    {
        if (!approved) throw new InvalidOperationException("Reviewer execution requires explicit local approval.");
        if (string.IsNullOrWhiteSpace(reviewer) || string.IsNullOrWhiteSpace(rationale)) throw new ArgumentException("Reviewer and approval rationale are required.");
        var approval = new ReviewerApproval(claimId, reviewer, rationale, DateTimeOffset.UtcNow);
        var execution = await _orchestrator.ExecuteAsync(root, new LlmRequest("review", prompt), claimId, cancellationToken);
        var id = _canonical.NextId(root, "reviews", "REVIEW");
        var review = new ReviewRecord(1, id, claimId, reviewer, ReviewVerdict.Inconclusive, execution.Evidence.Summary, [], execution.Evidence.Snapshot, approval.ApprovedAtUtc);
        await _canonical.WriteAsync(root, "reviews", id, review, cancellationToken);
        return new(execution, approval);
    }
}
