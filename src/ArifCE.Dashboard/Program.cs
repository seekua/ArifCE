using System.Text.Json;
using ArifCE.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{Environment.GetEnvironmentVariable("ARIFCE_DASHBOARD_PORT") ?? "5180"}");
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
    await next();
});
var locator = new ProjectLocator();
var canonical = new CanonicalStore();
var journal = new JournalStore();
var index = new IndexStore();
var git = new GitInspector();
var service = new ProjectService(canonical, journal, index, git);
var workspace = new WorkspaceRegistry();

app.MapGet("/", () => Results.Content(DashboardPageV2.Html.Replace("</body>", DashboardPageV2.DailyBriefScript + DashboardPageV2.ActivityTimelineScript + DashboardPageV2.ExecutiveSummaryScript + DashboardPageV2.ExtraScript + DashboardPageV2.DecisionBriefScript + DashboardPageV2.WorkScript + DashboardPageV2.HandoffScript + DashboardPageV2.ExplorerScript + DashboardPageV2.WorkspaceScript + DashboardPageV2.LlmScript + DashboardPageV2.ProviderScript + "</body>"), "text/html; charset=utf-8"));
app.MapGet("/assets/tabler.min.css", () => Results.File(Path.Combine(AppContext.BaseDirectory, "tabler.min.css"), "text/css"));
app.MapGet("/assets/arifce-dashboard.css", () => Results.File(Path.Combine(AppContext.BaseDirectory, "arifce-dashboard.css"), "text/css"));
app.MapGet("/assets/dashboard-daily-brief.js", () => Results.File(Path.Combine(AppContext.BaseDirectory, "dashboard-daily-brief.js"), "text/javascript"));
app.MapGet("/assets/dashboard-activity-timeline.js", () => Results.File(Path.Combine(AppContext.BaseDirectory, "dashboard-activity-timeline.js"), "text/javascript"));
app.MapGet("/assets/ArifCE.svg", () => Results.File(Path.Combine(AppContext.BaseDirectory, "ArifCE.svg"), "image/svg+xml"));
app.MapGet("/api/status", async () => Results.Json(new { status = "Healthy", details = await service.StatusAsync(Root()) }));
app.MapGet("/api/workspace", async () => Results.Json(await workspace.ListAsync()));
app.MapGet("/api/workspace/active", async () => Results.Json(new { root = await workspace.GetActiveAsync() }));
app.MapGet("/api/llm/providers", async () => Results.Json((await new LocalLlmSettingsStore().ListAsync()).Select(p => new { p.Id, provider = p.Provider.ToString(), p.Model, p.Endpoint, p.Enabled, runtime = p.RuntimeMode.ToString() })));
app.MapPost("/api/llm/providers/{id}/test", async (string id) =>
{
    var profile = (await new LocalLlmSettingsStore().ListAsync()).FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    if (profile is null) return Results.NotFound(new { error = "Provider profile not found." });
    return Results.Json(await LlmProviderFactory.Create(profile).TestConnectionAsync());
});
app.MapPost("/api/workspace/active", async (WorkspaceSelection selection) =>
{
    try { return Results.Json(new { root = await workspace.SetActiveAsync(selection.Root) }); }
    catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
});
app.MapGet("/api/overview", () =>
{
    var root = Root();
    // Canonical records do not carry an author field by design. Resolve attribution
    // from the append-only journal so the dashboard never invents an agent.
    var actorByEntity = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    var journalPath = Path.Combine(root, ".arifce", "journal", "events.jsonl");
    if (File.Exists(journalPath))
    {
        foreach (var line in File.ReadLines(journalPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var e = doc.RootElement;
                var entityId = e.TryGetProperty("entityId", out var entity) ? entity.ToString() : "";
                if (string.IsNullOrWhiteSpace(entityId)) continue;
                var actor = "repository";
                if (e.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object && data.TryGetProperty("agent", out var agent)) actor = agent.ToString();
                actorByEntity[entityId] = string.IsNullOrWhiteSpace(actor) ? "repository" : actor;
            }
            catch (JsonException) { /* a malformed journal line must not break the dashboard */ }
        }
    }
    object[] Read(string kind) => Directory.Exists(Path.Combine(root, ".arifce", kind))
        ? Directory.EnumerateFiles(Path.Combine(root, ".arifce", kind), "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(8).Select(file =>
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var value = doc.RootElement;
            string Get(string name) => value.TryGetProperty(name, out var p) ? p.ToString() : "";
            var summary = Get("summary");
            if (string.IsNullOrWhiteSpace(summary) && value.TryGetProperty("markdown", out var markdown)) summary = markdown.ToString().Replace("\r", "").Replace("\n", " ").Trim();
            var id = Get("id");
            var createdAt = Get("createdAtUtc");
            if (string.IsNullOrWhiteSpace(createdAt)) createdAt = File.GetLastWriteTimeUtc(file).ToString("O");
            object? metrics = value.TryGetProperty("metrics", out var metricValue) ? JsonSerializer.Deserialize<object>(metricValue.GetRawText()) : null;
            return (object)new { id, title = Get("title"), status = Get("status"), agent = actorByEntity.TryGetValue(id, out var actor) ? actor : "repository", createdAtUtc = createdAt, summary, statement = Get("statement"), claimId = Get("claimId"), metrics };
        }).ToArray() : [];
    object[] Events() => File.Exists(journalPath) ? File.ReadLines(journalPath).Reverse().Take(20).Select(line =>
    {
        using var doc = JsonDocument.Parse(line); var e = doc.RootElement;
        string Get(string name) => e.TryGetProperty(name, out var p) ? p.ToString() : "";
        var actor = e.TryGetProperty("data", out var data) && data.TryGetProperty("agent", out var agent) ? agent.ToString() : "repository";
        return (object)new { type = Get("type"), entityId = Get("entityId"), occurredAtUtc = Get("occurredAtUtc"), actor };
    }).ToArray() : [];
    object[] LlmActivity() => File.Exists(journalPath) ? File.ReadLines(journalPath).Reverse().Select(line =>
    {
        try
        {
            using var doc = JsonDocument.Parse(line); var e = doc.RootElement;
            if (!e.TryGetProperty("type", out var type) || type.GetString() != "llm.completed") return null;
            var data = e.TryGetProperty("data", out var d) ? d : default;
            string Get(string name) => data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out var p) ? p.ToString() : "";
            return (object?)new { provider = Get("provider"), model = Get("model"), tokens = Get("tokens"), estimatedCost = Get("estimatedCost"), evidenceId = e.TryGetProperty("entityId", out var id) ? id.ToString() : "", occurredAtUtc = e.TryGetProperty("occurredAtUtc", out var at) ? at.ToString() : "" };
        }
        catch (JsonException) { return null; }
    }).Where(x => x is not null).Cast<object>().Take(30).ToArray() : [];
    var evidence = Read("evidence");
    var llm = evidence.Where(x => ((dynamic)x).summary?.ToString()?.StartsWith("Provider ", StringComparison.OrdinalIgnoreCase) == true).ToArray();
    return Results.Json(new { decisions = Read("decisions"), tasks = Read("tasks"), claims = Read("claims"), evidence, llm, llmActivity = LlmActivity(), findings = Read("findings"), handoffs = Read("handoffs"), reviews = Read("reviews"), attempts = Read("attempts"), events = Events() });
});
app.MapGet("/api/search", async (string q, int? limit) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "q is required" });
    var hits = await index.SearchAsync(Root(), q, Math.Clamp(limit ?? 20, 1, 50));
    return Results.Json(hits.Select(x => new { path = x.Path, score = x.Score, snippet = x.Snippet }));
});
app.MapGet("/api/records", (string? kind, int? limit) =>
{
    var selected = string.IsNullOrWhiteSpace(kind) ? CanonicalStore.EntityDirectories : [kind!];
    if (selected.Any(x => !CanonicalStore.EntityDirectories.Contains(x, StringComparer.OrdinalIgnoreCase))) return Results.BadRequest(new { error = "Unknown record kind" });
    var max = Math.Clamp(limit ?? 20, 1, 100);
    var result = selected.SelectMany(directory => Directory.Exists(Path.Combine(Root(), ".arifce", directory))
        ? Directory.EnumerateFiles(Path.Combine(Root(), ".arifce", directory), "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(max).Select(path => new { kind = directory, id = Path.GetFileNameWithoutExtension(path), modifiedUtc = File.GetLastWriteTimeUtc(path) })
        : []).Take(max).ToArray();
    return Results.Json(result);
});
app.Run();

string Root()
{
    var configured = Environment.GetEnvironmentVariable("ARIFCE_PROJECT_ROOT");
    if (!string.IsNullOrWhiteSpace(configured)) return locator.FindRoot(configured);
    var active = workspace.GetActiveAsync().GetAwaiter().GetResult();
    return locator.FindRoot(active ?? Environment.CurrentDirectory);
}

record WorkspaceSelection(string Root);

static class DashboardPage
{
public const string Html = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>ArifCE · Project continuity</title>
<link rel="stylesheet" href="/assets/tabler.min.css"><style>
:root{--arif-blue:#2f80ed;--arif-ink:#182433;--arif-header-height:56px}body{background:#f4f7fb;color:var(--arif-ink)}body>header.navbar{position:relative;z-index:1040;background:#fff}.navbar-brand{font-weight:700;letter-spacing:-.02em}.brand-logo{height:34px;width:auto;display:block}.hero{background:linear-gradient(120deg,#14213d,#2057a6);color:#fff;border:0}.hero .text-secondary{color:#d7e6ff!important}.sidebar-link{border-radius:6px;margin:.15rem 0}.sidebar-link.active{background:#e8f1ff;color:#1769d0;font-weight:600}.card-title{font-weight:650}pre{white-space:pre-wrap;color:#536273;background:#f8fafc;border-radius:6px;padding:1rem;max-height:320px;overflow:auto}.metric{font-size:1.65rem;font-weight:700}.feature-icon{color:var(--arif-blue)}.section-label{font-size:.75rem;letter-spacing:.08em;text-transform:uppercase;color:#718096;font-weight:700}@media(min-width:992px){.page{display:block}.navbar.navbar-vertical.navbar-expand-lg{position:fixed;top:var(--arif-header-height)!important;bottom:0;left:0;z-index:1030;height:calc(100vh - var(--arif-header-height));overflow-y:auto}.navbar-vertical .container-fluid{padding-top:1rem}.navbar-vertical .navbar-nav{padding-top:1.5rem!important}.page-wrapper{margin-left:var(--tblr-navbar-width)}.sidebar-hidden .navbar-vertical{display:none}.sidebar-hidden .page-wrapper{margin-left:0}}.sidebar-collapsed .navbar-vertical{width:4.5rem}.sidebar-collapsed .navbar-vertical .nav-link{font-size:0;justify-content:center}.sidebar-collapsed .navbar-vertical .nav-link:before{content:'•';font-size:1.2rem}.sidebar-collapsed .navbar-collapsed .section-label{display:none}.sidebar-collapsed .page-wrapper{margin-left:4.5rem}
</style></head>
<body><header class="navbar navbar-expand-md d-print-none"><div class="container-xl"><button class="btn btn-icon me-2 d-none d-lg-inline-flex" id="sidebarToggle" title="Toggle navigation" aria-label="Toggle navigation">☰</button><a class="navbar-brand" href="#overview"><img class="brand-logo" src="/assets/ArifCE.svg" alt="ArifCE logo"></a><div class="navbar-nav flex-row order-md-last"><a class="nav-link" href="https://github.com/seekua/ArifCE" target="_blank">GitHub ↗</a></div></div></header>
<div class="page"><aside class="navbar navbar-vertical navbar-expand-lg" data-bs-theme="light"><div class="container-fluid"><h2 class="navbar-brand d-lg-none">ArifCE</h2><div class="collapse navbar-collapse show"><ul class="navbar-nav pt-lg-3"><li class="nav-item"><a class="nav-link sidebar-link active" href="#overview">Overview</a></li><li class="nav-item"><a class="nav-link sidebar-link" href="#context">Project context</a></li><li class="nav-item"><a class="nav-link sidebar-link" href="#records">Records</a></li><li class="nav-item"><a class="nav-link sidebar-link" href="#search">Search</a></li><li class="nav-item mt-3"><span class="section-label px-3">Learn ArifCE</span></li><li class="nav-item"><a class="nav-link sidebar-link" href="https://github.com/seekua/ArifCE/tree/main/docs" target="_blank">Documentation ↗</a></li><li class="nav-item"><a class="nav-link sidebar-link" href="https://github.com/seekua/ArifCE/blob/main/docs/getting-started/quick-start.md" target="_blank">Quick start ↗</a></li></ul></div></div></aside>
<div class="page-wrapper"><main class="container-xl py-4" id="overview"><section class="card hero mb-4"><div class="card-body p-4 p-md-5"><div class="row align-items-center"><div class="col-lg-8"><div class="section-label text-white-50 mb-2">LOCAL-FIRST PROJECT INTELLIGENCE</div><h1 class="display-5 mb-3">Keep the engineering story alive.</h1><p class="lead mb-4">ArifCE keeps decisions, evidence, failed attempts, and handoffs with your repository so every agent can pick up where the last one stopped.</p><a class="btn btn-light" href="#context">Explore this project</a></div><div class="col-lg-4 d-none d-lg-block text-center"><div style="font-size:7rem;line-height:1;color:#8fc1ff">◈</div><div class="text-white-50">Your context. Your repository. Your control.</div></div></div></div></section>
<section class="row row-cards mb-4" id="context"><div class="col-sm-6 col-lg-3"><div class="card card-sm"><div class="card-body"><div class="text-secondary">Project health</div><div class="metric" id="health">—</div><div class="text-secondary small">from current state</div></div></div></div><div class="col-sm-6 col-lg-3"><div class="card card-sm"><div class="card-body"><div class="text-secondary">Tracked records</div><div class="metric" id="recordCount">—</div><div class="text-secondary small">canonical project memory</div></div></div></div><div class="col-sm-6 col-lg-3"><div class="card card-sm"><div class="card-body"><div class="text-secondary">Storage</div><div class="metric">Local</div><div class="text-secondary small">no cloud connection</div></div></div></div><div class="col-sm-6 col-lg-3"><div class="card card-sm"><div class="card-body"><div class="text-secondary">Continuity</div><div class="metric">Ready</div><div class="text-secondary small">for the next agent</div></div></div></div></section>
<section class="row row-deck row-cards"><div class="col-lg-7"><div class="card h-100"><div class="card-header"><h2 class="card-title">Welcome to your project cockpit</h2></div><div class="card-body"><p>Use this local panel to understand what the repository knows before you change it.</p><div class="row g-3"><div class="col-md-6"><div class="d-flex"><div class="feature-icon me-2">◆</div><div><strong>Context first</strong><div class="text-secondary small">Read protocol, current state, and task-specific memory.</div></div></div></div><div class="col-md-6"><div class="d-flex"><div class="feature-icon me-2">✓</div><div><strong>Evidence over confidence</strong><div class="text-secondary small">Trace claims to verification and findings.</div></div></div></div><div class="col-md-6"><div class="d-flex"><div class="feature-icon me-2">↗</div><div><strong>Handoffs that work</strong><div class="text-secondary small">Leave the next contributor a precise starting point.</div></div></div></div><div class="col-md-6"><div class="d-flex"><div class="feature-icon me-2">⌕</div><div><strong>Searchable memory</strong><div class="text-secondary small">Find decisions, tasks, evidence, and failures quickly.</div></div></div></div></div></div></div></div><div class="col-lg-5"><div class="card h-100" id="statusCard"><div class="card-header"><h2 class="card-title">Current project status</h2></div><div class="card-body"><pre id="status">Loading…</pre></div></div></div>
<div class="col-12" id="records"><div class="card"><div class="card-header"><h2 class="card-title">Recent records</h2><div class="card-actions"><a href="https://github.com/seekua/ArifCE/tree/main/.arifce" target="_blank" class="btn btn-sm">View repository memory ↗</a></div></div><div class="card-body"><pre id="recordsData">Loading…</pre></div></div></div>
<div class="col-12" id="search"><div class="card"><div class="card-header"><h2 class="card-title">Search project context</h2></div><div class="card-body"><div class="input-group"><input class="form-control" id="q" placeholder="Search decisions, tasks, evidence…" onkeydown="if(event.key==='Enter')search()"><button class="btn btn-primary" onclick="search()">Search</button></div><pre id="results" class="mt-3">Type a query to search the indexed project memory.</pre></div></div></div></section></main><footer class="footer footer-transparent"><div class="container-xl"><div class="text-secondary small">ArifCE runs locally and keeps project intelligence in your repository.</div></div></footer></div></div>
<script>async function load(){try{const [s,r]=await Promise.all([fetch('/api/status'),fetch('/api/records?limit=30')]);const st=await s.json();const rec=await r.json();document.querySelector('#status').textContent=JSON.stringify(st,null,2);document.querySelector('#recordsData').textContent=JSON.stringify(rec,null,2);document.querySelector('#recordCount').textContent=rec.length;document.querySelector('#health').textContent=st.status||st.health||'Ready'}catch(e){document.querySelector('#status').textContent='Unable to load project status: '+e}}async function search(){const q=document.querySelector('#q').value;if(!q)return;const r=await fetch('/api/search?q='+encodeURIComponent(q));document.querySelector('#results').textContent=JSON.stringify(await r.json(),null,2)}load()</script></body></html>
""";
}
