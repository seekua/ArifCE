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
            var command = args[0].ToLowerInvariant();
            if (command == "workspace") { await WorkspaceCommand(new WorkspaceRegistry(), args); return 0; }
            var root = locator.FindRoot(Environment.CurrentDirectory);
            switch (command)
            {
                case "init": case "adopt": Console.WriteLine($"Created {string.Join(Environment.NewLine + "Created ", await service.InitializeAsync(root, command == "adopt"))}"); break;
                case "status": Console.WriteLine(await service.StatusAsync(root)); break;
                case "doctor": Console.WriteLine(await service.DoctorAsync(root, args.Contains("--repair", StringComparer.Ordinal))); break;
                case "rebuild": await index.RebuildAsync(root); Console.WriteLine("Index rebuilt from canonical project intelligence."); break;
                case "search": Require(args, 2, "search <query>"); await Search(index, root, string.Join(' ', args[1..])); break;
                case "context": Require(args, 2, "context <task> [--budget N]"); await Context(index, root, args); break;
                case "task": await TaskCommand(service, root, args); break;
                case "decision": await DecisionCommand(service, root, args); break;
                case "attempt": await AttemptCommand(service, root, args); break;
                case "finding": await FindingCommand(service, root, args); break;
                case "review": await ReviewCommand(service, root, args); break;
                case "checkpoint": Console.WriteLine((await service.CheckpointAsync(root, Option(args, "--summary") ?? string.Join(' ', args[1..]))).Id); break;
                case "claim": await ClaimCommand(service, root, args); break;
                case "acceptance": await AcceptanceCommand(service, root, args); break;
                case "verify": Require(args, 2, "verify <claim-id> --command <command>"); var verified = await service.VerifyAsync(root, args[1], Option(args, "--command") ?? throw new ArgumentException("--command is required.")); Console.WriteLine($"{verified.Claim.Id}: {verified.Claim.Status} ({verified.Evidence.Id})"); break;
                case "architecture": await ArchitectureCommand(service, root, args); break;
                case "api": await ApiCommand(service, root, args); break;
                case "schema": await SchemaCommand(service, root, args); break;
                case "llm": await LlmCommand(root, args); break;
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
    private static async Task TaskCommand(ProjectService service, string root, string[] args) { Require(args, 3, "task create <title> | task status <id> | task complete <id>"); switch (args[1]) { case "create": Console.WriteLine((await service.CreateTaskAsync(root, string.Join(' ', args[2..]), RiskLevel.Medium)).Id); break; case "status": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await service.GetTaskAsync(root, args[2]) ?? throw new InvalidOperationException("Task not found."), JsonDefaults.Options)); break; case "complete": Console.WriteLine((await service.CompleteTaskAsync(root, args[2])).Status); break; default: throw new ArgumentException("Unknown task action."); } }
    private static async Task ClaimCommand(ProjectService service, string root, string[] args) { Require(args, 3, "claim create <statement> | claim status <id>"); switch (args[1]) { case "create": Console.WriteLine((await service.CreateClaimAsync(root, string.Join(' ', args[2..]), RiskLevel.Medium)).Id); break; case "status": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await service.GetClaimAsync(root, args[2]) ?? throw new InvalidOperationException("Claim not found."), JsonDefaults.Options)); break; default: throw new ArgumentException("Unknown claim action."); } }
    private static async Task AcceptanceCommand(ProjectService service, string root, string[] args) { Require(args, 3, "acceptance create <claim-id> --actor <name> --rationale <text> | acceptance status <id> | acceptance revoke <id>"); switch (args[1]) { case "create": Console.WriteLine((await service.CreateAcceptanceAsync(root, args[2], Option(args, "--actor") ?? throw new ArgumentException("--actor is required."), Option(args, "--rationale") ?? throw new ArgumentException("--rationale is required."))).Id); break; case "status": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await service.GetAcceptanceAsync(root, args[2]) ?? throw new InvalidOperationException("Acceptance not found."), JsonDefaults.Options)); break; case "revoke": Console.WriteLine((await service.RevokeAcceptanceAsync(root, args[2])).Status); break; default: throw new ArgumentException("Unknown acceptance action."); } }
    private static async Task DecisionCommand(ProjectService service, string root, string[] args) { Require(args, 3, "decision create <title> --decision <text> [--rationale <text>] | decision status <id>"); switch (args[1]) { case "create": Console.WriteLine((await service.CreateDecisionAsync(root, args[2], Option(args, "--decision") ?? throw new ArgumentException("--decision is required."), Option(args, "--rationale"))).Id); break; case "status": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await service.GetDecisionAsync(root, args[2]) ?? throw new InvalidOperationException("Decision not found."), JsonDefaults.Options)); break; default: throw new ArgumentException("Unknown decision action."); } }
    private static async Task AttemptCommand(ProjectService service, string root, string[] args) { Require(args, 3, "attempt record <task-id> <approach> --result <result> --reason <text> [--evidence <id>] | attempt status <id>"); switch (args[1]) { case "record": Require(args, 4, "attempt record <task-id> <approach> --result <result> --reason <text> [--evidence <id>]"); Console.WriteLine((await service.RecordAttemptAsync(root, args[2], args[3], Option(args, "--result") ?? throw new ArgumentException("--result is required."), Option(args, "--reason") ?? throw new ArgumentException("--reason is required."), Options(args, "--evidence"))).Id); break; case "status": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await service.GetAttemptAsync(root, args[2]) ?? throw new InvalidOperationException("Attempt not found."), JsonDefaults.Options)); break; default: throw new ArgumentException("Unknown attempt action."); } }
    private static async Task FindingCommand(ProjectService service, string root, string[] args) { Require(args, 3, "finding create <title> --description <text> [--severity <level>] [--task <id>] [--path <path>] | finding status|resolve <id>"); switch (args[1]) { case "create": var severity = Enum.TryParse<RiskLevel>(Option(args, "--severity") ?? "Medium", true, out var parsed) ? parsed : throw new ArgumentException("--severity must be LOW, MEDIUM, HIGH, or CRITICAL."); Console.WriteLine((await service.CreateFindingAsync(root, args[2], Option(args, "--description") ?? throw new ArgumentException("--description is required."), severity, Option(args, "--task"), Option(args, "--path"))).Id); break; case "status": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await service.GetFindingAsync(root, args[2]) ?? throw new InvalidOperationException("Finding not found."), JsonDefaults.Options)); break; case "resolve": Console.WriteLine((await service.ResolveFindingAsync(root, args[2])).Status); break; default: throw new ArgumentException("Unknown finding action."); } }
    private static async Task ReviewCommand(ProjectService service, string root, string[] args) { Require(args, 3, "review record <claim-id> --reviewer <agent> --verdict <verdict> --summary <text> [--finding <id>] | review status <id>"); switch (args[1]) { case "record": var verdictText = (Option(args, "--verdict") ?? throw new ArgumentException("--verdict is required.")).Replace("_", "", StringComparison.Ordinal); var verdict = Enum.TryParse<ReviewVerdict>(verdictText, true, out var parsed) ? parsed : throw new ArgumentException("--verdict must be AGREE, PARTIALLY_AGREE, DISAGREE, or INCONCLUSIVE."); Console.WriteLine((await service.RecordReviewAsync(root, args[2], Option(args, "--reviewer") ?? throw new ArgumentException("--reviewer is required."), verdict, Option(args, "--summary") ?? throw new ArgumentException("--summary is required."), Options(args, "--finding"))).Id); break; case "status": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await service.GetReviewAsync(root, args[2]) ?? throw new InvalidOperationException("Review not found."), JsonDefaults.Options)); break; default: throw new ArgumentException("Unknown review action."); } }
    private static async Task ArchitectureCommand(ProjectService service, string root, string[] args) { Require(args, 3, "architecture check <claim-id> --forbid <reference> --path <source-path> [--forbid <reference> ...] [--path <source-path> ...]"); if (args[1] != "check") throw new ArgumentException("Unknown architecture action."); var result = await service.VerifyArchitectureBoundaryAsync(root, args[2], Options(args, "--forbid"), Options(args, "--path")); Console.WriteLine($"{result.Claim.Id}: {result.Claim.Status} ({result.Evidence.Id})"); }
    private static async Task ApiCommand(ProjectService service, string root, string[] args) { Require(args, 3, "api baseline|compare <assembly-path> --baseline <path> [--claim <claim-id>]"); var assembly = args[2]; var baseline = Option(args, "--baseline") ?? throw new ArgumentException("--baseline is required."); if (args[1] == "baseline") { await ApiSurfaceAnalyzer.WriteBaselineAsync(Path.GetFullPath(Path.Combine(root, baseline)), ApiSurfaceAnalyzer.Read(Path.GetFullPath(Path.Combine(root, assembly)))); Console.WriteLine($"API baseline written: {baseline}"); return; } if (args[1] != "compare") throw new ArgumentException("Unknown api action."); var claim = Option(args, "--claim") ?? throw new ArgumentException("--claim is required for api compare."); var result = await service.VerifyApiSurfaceAsync(root, claim, assembly, baseline); Console.WriteLine($"{result.Claim.Id}: {result.Claim.Status} ({result.Evidence.Id})"); }
    private static async Task SchemaCommand(ProjectService service, string root, string[] args) { Require(args, 3, "schema baseline|compare <database-path> --baseline <path> [--claim <claim-id>]"); var database = args[2]; var baseline = Option(args, "--baseline") ?? throw new ArgumentException("--baseline is required."); if (args[1] == "baseline") { await SqliteSchemaAnalyzer.WriteBaselineAsync(Path.GetFullPath(Path.Combine(root, baseline)), await SqliteSchemaAnalyzer.ReadAsync(Path.GetFullPath(Path.Combine(root, database)))); Console.WriteLine($"SQLite schema baseline written: {baseline}"); return; } if (args[1] != "compare") throw new ArgumentException("Unknown schema action."); var claim = Option(args, "--claim") ?? throw new ArgumentException("--claim is required for schema compare."); var result = await service.VerifySqliteSchemaAsync(root, claim, database, baseline); Console.WriteLine($"{result.Claim.Id}: {result.Claim.Status} ({result.Evidence.Id})"); }
    private static async Task Refactor(ProjectService service, CanonicalStore store, string root, string[] args) { Require(args, 2, "refactor start|status|checkpoint|resolve|workstream|safepoint|verify|finish|abandon"); switch (args[1]) { case "start": Require(args, 4, "refactor start <title> <objective> [--invariant <text>] [--inventory <item>] [--forbid <reference>]"); var invariants = Options(args, "--invariant"); var inventory = Options(args, "--inventory"); var guards = Options(args, "--forbid").Select(x => new RefactorGuard("forbiddenReference", x, true)).ToArray(); Console.WriteLine((await service.StartRefactorAsync(root, args[2], args[3], invariants, inventory, guards)).Id); break; case "status": Require(args, 3, "refactor status <id>"); Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await store.ReadAsync<RefactorCampaign>(root, "refactors", args[2]) ?? throw new InvalidOperationException("Refactor not found."), JsonDefaults.Options)); break; case "checkpoint": Require(args, 4, "refactor checkpoint <id> <summary>"); Console.WriteLine((await service.CheckpointAsync(root, $"{args[2]}: {string.Join(' ', args[3..])}")).Id); break; case "resolve": Require(args, 4, "refactor resolve <id> <inventory-item>"); Console.WriteLine($"Remaining inventory: {(await service.ResolveRefactorInventoryAsync(root, args[2], args[3])).Inventory.Count}"); break; case "workstream": Require(args, 4, "refactor workstream <id> <name> --owner <agent> --path <path> [--path <path>]"); var owner = Option(args, "--owner") ?? throw new ArgumentException("--owner is required."); var paths = Options(args, "--path"); Console.WriteLine($"Workstreams: {(await service.AddRefactorWorkstreamAsync(root, args[2], args[3], owner, paths)).Workstreams!.Count}"); break; case "safepoint": Require(args, 4, "refactor safepoint <id> <name> [--note <text>]"); Console.WriteLine($"Safe points: {(await service.AddRefactorSafePointAsync(root, args[2], args[3], Option(args, "--note"))).SafePoints!.Count}"); break; case "verify": Require(args, 3, "refactor verify <id>"); var failures = await service.VerifyRefactorAsync(root, args[2]); Console.WriteLine(failures.Count == 0 ? "All configured deterministic refactor guards pass." : "Refactor verification failed:\n- " + string.Join("\n- ", failures)); if (failures.Count > 0) throw new InvalidOperationException("Refactor has blocking verification failures."); break; case "finish": Require(args, 3, "refactor finish <id>"); Console.WriteLine((await service.FinishRefactorAsync(root, args[2])).Status); break; case "abandon": Require(args, 3, "refactor abandon <id>"); Console.WriteLine((await service.AbandonRefactorAsync(root, args[2])).Status); break; default: throw new ArgumentException("Unknown refactor action."); } }
    private static string? Option(string[] args, string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    private static string[] Options(string[] args, string name) => args.Select((value, index) => (value, index)).Where(x => x.value == name && x.index + 1 < args.Length).Select(x => args[x.index + 1]).ToArray();
    private static void Require(string[] args, int length, string usage) { if (args.Length < length) throw new ArgumentException(usage); }
    private static async Task WorkspaceCommand(WorkspaceRegistry registry, string[] args)
    {
        Require(args, 2, "workspace list | workspace add <name> <root> | workspace remove <root> | workspace use <root>");
        switch (args[1].ToLowerInvariant())
        {
            case "list": foreach (var project in await registry.ListAsync()) Console.WriteLine($"{project.Name}\t{project.Root}\t{project.LastSeenUtc:O}"); break;
            case "add": Require(args, 4, "workspace add <name> <root>"); var added = await registry.AddAsync(args[2], args[3]); Console.WriteLine($"Registered {added.Name}: {added.Root}"); break;
            case "remove": Require(args, 3, "workspace remove <root>"); await registry.RemoveAsync(args[2]); Console.WriteLine($"Removed {Path.GetFullPath(args[2])}"); break;
            case "use": Require(args, 3, "workspace use <root>"); Console.WriteLine($"Active project: {await registry.SetActiveAsync(args[2])}"); break;
            default: throw new ArgumentException("Unknown workspace action.");
        }
    }
    private static async Task LlmCommand(string root, string[] args)
    {
        Require(args, 2, "llm provider list|add|remove|test | llm context <task> [--budget N] | llm run <task> <prompt> [--claim <id>] | llm review <claim> <prompt> --reviewer <name> --rationale <text> --approved");
        var store = new LocalLlmSettingsStore();
        if (args[1].Equals("context", StringComparison.OrdinalIgnoreCase))
        {
            Require(args, 3, "llm context <task> [--budget N]");
            var marker = Array.IndexOf(args, "--budget");
            var budget = marker >= 0 && marker + 1 < args.Length && int.TryParse(args[marker + 1], out var parsed) ? parsed : 4000;
            var task = string.Join(' ', args.Skip(2).Take(marker >= 0 ? marker - 2 : args.Length - 2));
            var context = await new LlmContextComposer(new IndexStore()).ComposeAsync(root, task, budget);
            Console.WriteLine($"Context budget: {budget}\nEstimated tokens: {context.EstimatedTokens}\nSources: {string.Join(", ", context.Sources)}\n\n{context.Content}");
            return;
        }
        if (args[1].Equals("review", StringComparison.OrdinalIgnoreCase))
        {
            Require(args, 4, "llm review <claim> <prompt> --reviewer <name> --rationale <text> --approved");
            if (!args.Contains("--approved", StringComparer.Ordinal)) throw new InvalidOperationException("Explicit --approved is required for reviewer execution.");
            var reviewProfiles = (await store.ListAsync()).Where(x => x.Enabled).ToList();
            if (reviewProfiles.Count == 0) throw new InvalidOperationException("No enabled local LLM providers configured.");
            var workflow = new LlmReviewerWorkflow(new LlmOrchestrator(new LlmRouter(reviewProfiles.Select(p => (LlmProviderFactory.Create(p), p))), new CanonicalStore(), new JournalStore(), new GitInspector()), new CanonicalStore());
            var reviewPrompt = string.Join(' ', args.Skip(3).TakeWhile(x => x is not "--reviewer" and not "--rationale" and not "--approved"));
            var review = await workflow.RunAsync(root, args[2], Option(args, "--reviewer") ?? throw new ArgumentException("--reviewer is required."), Option(args, "--rationale") ?? throw new ArgumentException("--rationale is required."), reviewPrompt, true);
            Console.WriteLine($"Review evidence: {review.Execution.Evidence.Id}\nReview provider: {review.Execution.Route.Response.ProviderId} / {review.Execution.Route.Response.Model}");
            return;
        }
        if (args[1].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
        {
            Require(args, 4, "llm benchmark <prompt> --expected <text>");
            var expected = Option(args, "--expected") ?? throw new ArgumentException("--expected is required.");
            var benchmarkProfiles = (await store.ListAsync()).Where(x => x.Enabled).ToList();
            if (benchmarkProfiles.Count == 0) throw new InvalidOperationException("No enabled local LLM providers configured.");
            var benchmarkRouter = new LlmRouter(benchmarkProfiles.Select(p => (LlmProviderFactory.Create(p), p)));
            var prompt = string.Join(' ', args.Skip(2).TakeWhile(x => x != "--expected"));
            var results = await LlmBenchmark.RunAsync(new[] { new BenchmarkCase("cli", prompt, expected) }, async _ => await benchmarkRouter.CompleteAsync(new LlmRequest("benchmark", prompt)));
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(results, JsonDefaults.Options));
            return;
        }
        if (args[1].Equals("provider", StringComparison.OrdinalIgnoreCase))
        {
            Require(args, 3, "llm provider list|add|remove|test");
            switch (args[2].ToLowerInvariant())
            {
                case "list": foreach (var p in await store.ListAsync()) Console.WriteLine($"{p.Id}\t{p.Provider}\t{p.Model}\t{(p.Enabled ? "enabled" : "disabled")}"); break;
                case "add":
                    Require(args, 5, "llm provider add <id> <kind> <model> [--endpoint <url>] [--api-key-env <name>] [--api-key-stdin]");
                    if (!Enum.TryParse<LlmProviderKind>(args[3], true, out var kind)) throw new ArgumentException("Unknown provider kind.");
                    var key = Option(args, "--api-key-env") is { } env ? Environment.GetEnvironmentVariable(env) : args.Contains("--api-key-stdin", StringComparer.Ordinal) ? (await Console.In.ReadToEndAsync()).Trim() : null;
                    await store.UpsertAsync(new LlmProviderProfile(args[2], kind, args[4], Option(args, "--endpoint"), key));
                    Console.WriteLine($"Saved local provider profile '{args[2]}'."); break;
                case "remove": Require(args, 4, "llm provider remove <id>"); await store.RemoveAsync(args[3]); Console.WriteLine($"Removed local provider profile '{args[3]}'."); break;
                case "test":
                    Require(args, 4, "llm provider test <id>");
                    var profile = (await store.ListAsync()).FirstOrDefault(x => string.Equals(x.Id, args[3], StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Provider profile not found.");
                    var test = await LlmProviderFactory.Create(profile).TestConnectionAsync(); Console.WriteLine($"{test.ProviderId}: {(test.Success ? "OK" : "FAILED")} - {test.Message}"); break;
                default: throw new ArgumentException("Unknown llm provider action.");
            }
            return;
        }
        if (!args[1].Equals("run", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Unknown llm action.");
        Require(args, 4, "llm run <task> <prompt> [--claim <id>] [--with-context] [--budget N]");
        var profiles = (await store.ListAsync()).Where(x => x.Enabled).ToList();
        if (profiles.Count == 0) throw new InvalidOperationException("No enabled local LLM providers configured. Add one with 'llm provider add'.");
        var orchestrator = new LlmOrchestrator(new LlmRouter(profiles.Select(p => (LlmProviderFactory.Create(p), p))), new CanonicalStore(), new JournalStore(), new GitInspector());
        var prompt = string.Join(' ', args.Skip(3).TakeWhile(x => x is not "--claim" and not "--with-context" and not "--budget"));
        if (args.Contains("--with-context", StringComparer.Ordinal))
        {
            var budgetIndex = Array.IndexOf(args, "--budget");
            var budget = budgetIndex >= 0 && budgetIndex + 1 < args.Length && int.TryParse(args[budgetIndex + 1], out var parsedBudget) ? parsedBudget : 4000;
            var context = await new LlmContextComposer(new IndexStore()).ComposeAsync(root, args[2], budget);
            prompt = $"Repository context:\n{context.Content}\n\nTask prompt:\n{prompt}";
        }
        var execution = await orchestrator.ExecuteAsync(root, new LlmRequest(args[2], prompt), Option(args, "--claim") ?? "CLAIM-UNASSIGNED");
        Console.WriteLine($"{execution.Route.Response.Text}\n\nProvider: {execution.Route.Response.ProviderId}\nModel: {execution.Route.Response.Model}\nTokens: {execution.Route.Response.Usage.TotalTokens}\nEstimated cost: {execution.Route.EstimatedCost:0.########}\nEvidence: {execution.Evidence.Id}");
    }
    private static void Help() => Console.WriteLine("ArifCE CLI\n\nCommands: init, adopt, status, doctor [--repair], rebuild, search, context, checkpoint, handoff, workspace list|add|remove|use, task create|status|complete, decision create|status, attempt record|status, finding create|status|resolve, claim create|status, acceptance create|status|revoke, verify, architecture check, api baseline|compare, schema baseline|compare, review record|status, llm provider list|add|remove|test, llm context, llm run, llm review, llm benchmark, why, refactor start|status|checkpoint|resolve|workstream|safepoint|verify|finish|abandon");
}
