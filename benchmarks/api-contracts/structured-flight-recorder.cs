using ArifCE.Core;
using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root)
    {
        var service = new ProjectService(new CanonicalStore(), new JournalStore(), new IndexStore(), new GitInspector());
        AgentRunRecord run = await service.StartAgentRunAsync(root, "provider", "agent", "goal", "TASK-0001");
        run = await service.RecordAgentRunStepAsync(root, run.Id, AgentStepKind.Investigation, "summary", "outcome", 0, ["TASK-0001"]);
        run = await service.FinishAgentRunAsync(root, run.Id, "done", true);
        run = (await service.GetAgentRunAsync(root, run.Id))!;
        _ = (run.Provider, run.Agent, run.Goal, run.TaskId, run.Status, run.Steps, run.StartedAt, run.CompletedAtUtc);
    }
}
