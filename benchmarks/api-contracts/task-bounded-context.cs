using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root)
    {
        var composer = new LlmContextComposer(new IndexStore());
        LlmContext context = await composer.ComposeForTaskAsync(root, "TASK-0001", 4000, CancellationToken.None);
        ContextAssemblyTelemetry telemetry = context.Telemetry;
        ContextAssemblyItem? first = context.Items.FirstOrDefault();
        _ = (context.Task, context.Content, context.EstimatedTokens, context.Sources, context.Items, telemetry.AssemblyMilliseconds, first?.Reason, first?.Freshness, first?.Included);
    }
}
