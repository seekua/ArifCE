using System.Text.Json;
using ArifCE.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://127.0.0.1:{Environment.GetEnvironmentVariable("ARIFCE_DASHBOARD_PORT") ?? "5180"}");
var app = builder.Build();
var locator = new ProjectLocator();
var canonical = new CanonicalStore();
var journal = new JournalStore();
var index = new IndexStore();
var git = new GitInspector();
var service = new ProjectService(canonical, journal, index, git);

app.MapGet("/", () => Results.Content(DashboardPage.Html, "text/html; charset=utf-8"));
app.MapGet("/assets/tabler.min.css", () => Results.File(Path.Combine(AppContext.BaseDirectory, "tabler.min.css"), "text/css"));
app.MapGet("/api/status", async () => Results.Text(await service.StatusAsync(Root()), "application/json"));
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

string Root() => locator.FindRoot(Environment.GetEnvironmentVariable("ARIFCE_PROJECT_ROOT") ?? Environment.CurrentDirectory);

static class DashboardPage
{
public const string Html = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>ArifCE Dashboard</title>
<link rel="stylesheet" href="/assets/tabler.min.css"><style>body{background:#0f172a;color:#e2e8f0}pre{white-space:pre-wrap;color:#cbd5e1}input{background:#0f172a;color:white}</style></head>
<body><main class="container-xl py-4"><div class="page-header mb-4"><div><h1 class="page-title">ArifCE Dashboard</h1><p class="text-secondary">Local project intelligence · no cloud connection</p></div></div><div class="row row-deck row-cards"><div class="col-12"><section class="card"><div class="card-header"><h2 class="card-title">Current status</h2></div><div class="card-body"><pre id="status">Loading…</pre></div></section></div><div class="col-12"><section class="card"><div class="card-header"><h2 class="card-title">Recent project records</h2></div><div class="card-body"><pre id="records">Loading…</pre></div></section></div><div class="col-12"><section class="card"><div class="card-header"><h2 class="card-title">Search project context</h2></div><div class="card-body"><div class="input-group"><input class="form-control" id="q" placeholder="Search decisions, tasks, evidence…"><button class="btn btn-primary" onclick="search()">Search</button></div><pre id="results" class="mt-3"></pre></div></section></div></div></main>
<script>async function load(){const [s,r]=await Promise.all([fetch('/api/status'),fetch('/api/records?limit=30')]);document.querySelector('#status').textContent=await s.text();document.querySelector('#records').textContent=JSON.stringify(await r.json(),null,2)}async function search(){const q=document.querySelector('#q').value;if(!q)return;const r=await fetch('/api/search?q='+encodeURIComponent(q));document.querySelector('#results').textContent=JSON.stringify(await r.json(),null,2)}load()</script></body></html>
""";
}
