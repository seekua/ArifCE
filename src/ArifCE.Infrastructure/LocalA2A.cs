namespace ArifCE.Infrastructure;

public sealed record A2AAgent(string Id, string Role, Func<string, CancellationToken, Task<string>> HandleAsync);
public sealed record A2ATurn(string AgentId, string Input, string Output, DateTimeOffset CreatedAtUtc);

/// <summary>Deterministic, local-only agent handoff loop. It does not create worktrees or call a cloud broker.</summary>
public sealed class LocalA2AOrchestrator(IEnumerable<A2AAgent> agents)
{
    private readonly IReadOnlyList<A2AAgent> _agents = agents.ToArray();
    public async Task<IReadOnlyList<A2ATurn>> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        var turns = new List<A2ATurn>(); var current = input;
        foreach (var agent in _agents)
        {
            var output = await agent.HandleAsync(current, cancellationToken);
            turns.Add(new(agent.Id, current, output, DateTimeOffset.UtcNow)); current = output;
        }
        return turns;
    }
}
