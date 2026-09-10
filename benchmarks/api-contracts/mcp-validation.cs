using ArifCE.Core;
using ArifCE.Infrastructure;

public static class BenchmarkApiContract
{
    public static async Task Probe(string root)
    {
        var store = new CanonicalStore();
        TaskRecord? task = await store.ReadAsync<TaskRecord>(root, "tasks", "TASK-0001");
        AttemptRecord? attempt = await store.ReadAsync<AttemptRecord>(root, "attempts", "ATTEMPT-0001");
        _ = (task, attempt);
    }
}
