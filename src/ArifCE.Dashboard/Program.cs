using System.Text.Json;
using ArifCE.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ARIFCE_DASHBOARD_URL") ?? "http://127.0.0.1:5180");
var app = builder.Build();
var locator = new ProjectLocator();
var canonical = new CanonicalStore();
var journal = new JournalStore();
var index = new IndexStore();
var git = new GitInspector();
var service = new ProjectService(canonical, journal, index, git);

app.MapGet("/", () => Results.Content(DashboardPage.Html, "text/html; charset=utf-8"));
app.MapGet("/api/status", async () => Results.Text(await service.StatusAsync(Root()), "application/json"));
app.MapGet("/api/search", async (string q, int? limit) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest(new { error = "q is required" });
    var hits = await index.SearchAsync(Root(), q, Math.Clamp(limit ?? 20, 1, 50));
    return Results.Json(hits.Select(x => new { path = x.Path, score = x.Score, snippet = x.Snippet }));
});
app.Run();

string Root() => locator.FindRoot(Environment.GetEnvironmentVariable("ARIFCE_PROJECT_ROOT") ?? Environment.CurrentDirectory);

static class DashboardPage
{
public const string Html = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>ArifCE Dashboard</title>
<style>body{font:16px system-ui;margin:0;background:#0f172a;color:#e2e8f0}main{max-width:960px;margin:auto;padding:32px}section{background:#1e293b;border-radius:12px;padding:20px;margin:16px 0}pre{white-space:pre-wrap;color:#cbd5e1}input,button{font:inherit;padding:10px;border-radius:8px;border:1px solid #475569}button{background:#2563eb;color:white;cursor:pointer}input{width:65%;background:#0f172a;color:white}</style></head>
<body><main><h1>ArifCE Dashboard</h1><p>Local project intelligence · no cloud connection</p><section><h2>Current status</h2><pre id="status">Loading…</pre></section><section><h2>Search project context</h2><input id="q" placeholder="Search decisions, tasks, evidence…"><button onclick="search()">Search</button><pre id="results"></pre></section></main>
<script>async function load(){const r=await fetch('/api/status');document.querySelector('#status').textContent=await r.text()}async function search(){const q=document.querySelector('#q').value;if(!q)return;const r=await fetch('/api/search?q='+encodeURIComponent(q));document.querySelector('#results').textContent=JSON.stringify(await r.json(),null,2)}load()</script></body></html>
""";
}
