using System.Text.Json;

namespace ArifCE.Infrastructure;

public sealed record ApprovalPolicy(string Id, string Description, bool RequiresHumanApproval = true, int MaxCostCents = 0, IReadOnlyList<string>? AllowedProviders = null);
public sealed record PolicyDecision(bool Allowed, string Reason, DateTimeOffset EvaluatedAtUtc);

public sealed class LocalPolicyEngine
{
    private readonly IReadOnlyList<ApprovalPolicy> _policies;
    public LocalPolicyEngine(IEnumerable<ApprovalPolicy> policies) => _policies = policies.ToArray();
    public PolicyDecision Evaluate(string providerId, decimal estimatedCost, bool humanApproved)
    {
        foreach (var policy in _policies)
        {
            if (policy.AllowedProviders is { Count: > 0 } allowed && !allowed.Contains(providerId, StringComparer.OrdinalIgnoreCase)) return new(false, $"Provider '{providerId}' is not allowed by policy '{policy.Id}'.", DateTimeOffset.UtcNow);
            if (policy.MaxCostCents > 0 && estimatedCost * 100 > policy.MaxCostCents) return new(false, $"Estimated cost exceeds policy '{policy.Id}'.", DateTimeOffset.UtcNow);
            if (policy.RequiresHumanApproval && !humanApproved) return new(false, $"Human approval is required by policy '{policy.Id}'.", DateTimeOffset.UtcNow);
        }
        return new(true, "Allowed by local policy.", DateTimeOffset.UtcNow);
    }
}

public sealed record BenchmarkCase(string Id, string Prompt, string Expected, string? ProviderId = null);
/// <summary>A smoke-test result. TokenRecall is expected-token coverage, not semantic quality.</summary>
public sealed record BenchmarkResult(string CaseId, string ProviderId, double TokenRecall, TimeSpan Latency, int Tokens, decimal EstimatedCost)
{
    public bool Passed => TokenRecall >= .8;
    public double Similarity => TokenRecall;
}

public static class LlmBenchmark
{
    public static async Task<IReadOnlyList<BenchmarkResult>> RunAsync(IEnumerable<BenchmarkCase> cases, Func<BenchmarkCase, Task<LlmRouteResult>> execute)
    {
        var results = new List<BenchmarkResult>();
        foreach (var test in cases)
        {
            var started = DateTimeOffset.UtcNow;
            var route = await execute(test);
            var similarity = Similarity(route.Response.Text, test.Expected);
            results.Add(new(test.Id, route.Response.ProviderId, similarity, DateTimeOffset.UtcNow - started, route.Response.Usage.TotalTokens, route.EstimatedCost));
        }
        return results;
    }
    private static double Similarity(string actual, string expected)
    {
        var a = actual.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return b.Count == 0 ? (a.Count == 0 ? 1 : 0) : (double)a.Intersect(b).Count() / b.Count;
    }
}
