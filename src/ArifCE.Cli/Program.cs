using ArifCE.Core;
using ArifCE.Infrastructure;

return await Cli.RunAsync(args);

internal static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "-h" or "--help" or "help") { Help(); return 0; }
            var locator = new ProjectLocator(); var canonical = new CanonicalStore(); var journal = new JournalStore(); var index = new IndexStore(); var git = new GitInspector(); var service = new ProjectService(canonical, journal, index, git);
            var root = locator.FindRoot(Environment.CurrentDirectory); var command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "init": case "adopt": Console.WriteLine($"Created {string.Join(Environment.NewLine + "Created ", await service.InitializeAsync(root, command == "adopt"))}"); break;
                case "status": Console.WriteLine(await service.StatusAsync(root)); break;
                case "doctor": Console.WriteLine(await service.DoctorAsync(root)); break;
                case "rebuild": await index.RebuildAsync(root); Console.WriteLine("Index rebuilt from canonical project intelligence."); break;
                case "search": Require(args, 2, "search <query>"); await Search(index, root, string.Join(' ', args[1..])); break;
                case "context": Require(args, 2, "context <task> [--budget N]"); await Context(index, root, args); break;
                case "task": Require(args, 3, "task create <title>"); if (args[1] != "create") throw new ArgumentException("V0.1 usage: task create <title>"); Console.WriteLine((await service.CreateTaskAsync(root, string.Join(' ', args[2..]), RiskLevel.Medium)).Id); break;
                case "checkpoint": Console.WriteLine((await service.CheckpointAsync(root, Option(args, "--summary") ?? string.Join(' ', args[1..]))).Id); break;
                case "claim": Require(args, 3, "claim create <statement>"); if (args[1] != "create") throw new ArgumentException("V0.1 usage: claim create <statement>"); Console.WriteLine((await service.CreateClaimAsync(root, string.Join(' ', args[2..]), RiskLevel.Medium)).Id); break;
                case "verify": Require(args, 2, "verify <claim-id> --command <command>"); var verified = await service.VerifyAsync(root, args[1], Option(args, "--command") ?? throw new ArgumentException("--command is required.")); Console.WriteLine($"{verified.Claim.Id}: {verified.Claim.Status} ({verified.Evidence.Id})"); break;
                case "handoff": var handoff = await service.HandoffAsync(root); Console.WriteLine(handoff.Markdown); Console.WriteLine($"Saved {handoff.Id}"); break;
                case "why": Require(args, 2, "why <path-or-id>"); await Why(index, root, args[1]); break;
                case "refactor": await Refactor(service, canonical, root, args); break;
                default: throw new ArgumentException($"Unknown command '{args[0]}'. Run 'arifce help'.");
            }
            return 0;
        }
        catch (ArgumentException exception) { Console.Error.WriteLine($"Usage error: {exception.Message}"); return 2; }
        catch (Exception exception) { Console.Error.WriteLine($"Error: {exception.Message}"); return 1; }
    }

    private static async Task Search(IndexStore index, string root, string query) { foreach (var item in await index.SearchAsync(root, query)) Console.WriteLine($"{item.Path}\t{item.Score:F3}\t{item.Snippet.Replace(Environment.NewLine, " ")}"); }
    private static async Task Context(IndexStore index, string root, string[] args) { var marker = Array.IndexOf(args, "--budget"); var budget = marker >= 0 && marker + 1 < args.Length && int.TryParse(args[marker + 1], out var n) && n > 0 ? n : 8000; var task = string.Join(' ', args.Skip(1).Take(marker < 0 ? args.Length - 1 : marker - 1)); var terms = System.Text.RegularExpressions.Regex.Matches(task, "[A-Za-z0-9_]+", System.Text.RegularExpressions.RegexOptions.CultureInvariant).Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray(); if (terms.Length == 0) throw new ArgumentException("Context task must contain searchable terms."); var query = string.Join(" OR ", terms.Select(x => $"\"{x}\"")); var used = 0; Console.WriteLine($"Context Budget: {budget}\n\nIncluded"); foreach (var item in await index.SearchAsync(root, query, 50)) { var estimate = (int)Math.Ceiling(item.Snippet.Length / 4d); if (used + estimate > budget) continue; used += estimate; Console.WriteLine($"{item.Path}\t{estimate} tokens\tlexical term match, score {item.Score:F3}\n{item.Snippet}"); } Console.WriteLine($"\nEstimated total: {used}"); }
    private static async Task Why(IndexStore index, string root, string query) { var hits = await index.SearchAsync(root, $"\"{query.Replace("\"", "\"\"")}\"", 10); if (hits.Count == 0) Console.WriteLine("No recorded provenance or rationale was found. Historical rationale: unknown."); else foreach (var hit in hits) Console.WriteLine($"{hit.Path}: {hit.Snippet}"); }
    private static async Task Refactor(ProjectService service, CanonicalStore store, string root, string[] args) { Require(args, 2, "refactor start|status|checkpoint|verify|finish"); switch (args[1]) { case "start": Require(args, 4, "refactor start <title> <objective>"); Console.WriteLine((await service.StartRefactorAsync(root, args[2], string.Join(' ', args[3..]))).Id); break; case "status": Require(args, 3, "refactor status <id>"); Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await store.ReadAsync<RefactorCampaign>(root, "refactors", args[2]) ?? throw new InvalidOperationException("Refactor not found."), JsonDefaults.Options)); break; case "checkpoint": Require(args, 4, "refactor checkpoint <id> <summary>"); Console.WriteLine((await service.CheckpointAsync(root, $"{args[2]}: {string.Join(' ', args[3..])}")).Id); break; case "verify": Require(args, 3, "refactor verify <id>"); var failures = await service.VerifyRefactorAsync(root, args[2]); Console.WriteLine(failures.Count == 0 ? "All configured deterministic refactor guards pass." : "Refactor verification failed:\n- " + string.Join("\n- ", failures)); if (failures.Count > 0) throw new InvalidOperationException("Refactor has blocking verification failures."); break; case "finish": Require(args, 3, "refactor finish <id>"); Console.WriteLine((await service.FinishRefactorAsync(root, args[2])).Status); break; default: throw new ArgumentException("Unknown refactor action."); } }
    private static string? Option(string[] args, string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    private static void Require(string[] args, int length, string usage) { if (args.Length < length) throw new ArgumentException(usage); }
    private static void Help() => Console.WriteLine("ArifCE 0.1\n\nCommands: init, adopt, status, doctor, rebuild, search, context, checkpoint, handoff, task, claim, verify, why, refactor start|status|checkpoint|verify|finish");
}
