static class DashboardPageV2
{
    public const string Html = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
  <title>ArifCE Dashboard</title><link rel="stylesheet" href="/assets/tabler.min.css">
  <style>body{background:#f4f6fa}.brand-logo{height:32px;width:auto}.hero{background:linear-gradient(135deg,#182d57,#2563ad);color:#fff}.hero .text-secondary{color:#dceaff!important}.metric{font-size:1.7rem;font-weight:700}pre{white-space:pre-wrap;max-height:330px;overflow:auto;background:#f8fafc;padding:1rem;border-radius:var(--tblr-border-radius)}</style>
</head>
<body>
<div class="page">
  <header class="navbar navbar-expand-md d-print-none">
    <div class="container-xl">
      <div class="navbar-brand navbar-brand-autodark"><a href="#overview" class="d-flex align-items-center"><img src="/assets/ArifCE.svg" class="brand-logo" alt="ArifCE"></a></div>
      <div class="navbar-nav flex-row order-md-last"><a class="nav-link" href="https://github.com/seekua/ArifCE" target="_blank" rel="noreferrer">GitHub ↗</a></div>
    </div>
  </header>
  <div class="page-wrapper">
    <aside class="navbar navbar-vertical navbar-expand-lg" data-bs-theme="light">
      <div class="container-fluid"><div class="collapse navbar-collapse show"><ul class="navbar-nav pt-lg-3">
        <li class="nav-item"><a class="nav-link active" href="#overview"><span class="nav-link-title">Overview</span></a></li>
        <li class="nav-item"><a class="nav-link" href="#context"><span class="nav-link-title">Project context</span></a></li>
        <li class="nav-item"><a class="nav-link" href="#records"><span class="nav-link-title">Records</span></a></li>
        <li class="nav-item"><a class="nav-link" href="#search"><span class="nav-link-title">Search</span></a></li>
        <li class="nav-item mt-4"><div class="nav-link disabled"><span class="nav-link-title">Learn ArifCE</span></div></li>
        <li class="nav-item"><a class="nav-link" href="https://github.com/seekua/ArifCE/tree/master/docs" target="_blank" rel="noreferrer"><span class="nav-link-title">Documentation ↗</span></a></li>
        <li class="nav-item"><a class="nav-link" href="https://github.com/seekua/ArifCE/blob/master/docs/getting-started/quick-start.md" target="_blank" rel="noreferrer"><span class="nav-link-title">Quick start ↗</span></a></li>
      </ul></div></div>
    </aside>
    <div class="page-body"><div class="container-xl py-4" id="overview">
      <section class="card hero mb-4"><div class="card-body p-4 p-md-5"><div class="row align-items-center"><div class="col-lg-8"><div class="text-uppercase text-white-50 small fw-bold mb-2">Local-first project intelligence</div><h1 class="display-5 mb-3">Keep the engineering story alive.</h1><p class="lead mb-4">Decisions, evidence, failed attempts, and handoffs stay with your repository so every agent can continue with context.</p><a class="btn btn-light" href="#context">Explore this project</a></div><div class="col-lg-4 d-none d-lg-block text-center"><div class="display-1">◈</div><div class="text-white-50">Your context. Your repository. Your control.</div></div></div></div></section>
      <div class="row row-cards mb-4" id="context"><div class="col-sm-6 col-lg-3"><div class="card card-sm"><div class="card-body"><div class="text-secondary">Project health</div><div class="metric" id="health">Loading…</div><div class="text-secondary small">from current state</div></div></div></div><div class="col-sm-6 col-lg-3"><div class="card card-sm"><div class="card-body"><div class="text-secondary">Tracked records</div><div class="metric" id="recordCount">—</div><div class="text-secondary small">canonical project memory</div></div></div></div><div class="col-sm-6 col-lg-3"><div class="card card-sm"><div class="card-body"><div class="text-secondary">Storage</div><div class="metric">Local</div><div class="text-secondary small">no cloud connection</div></div></div></div><div class="col-sm-6 col-lg-3"><div class="card card-sm"><div class="card-body"><div class="text-secondary">Continuity</div><div class="metric">Ready</div><div class="text-secondary small">for the next agent</div></div></div></div></div>
      <div class="row row-deck row-cards"><div class="col-lg-7"><div class="card h-100"><div class="card-header"><h2 class="card-title">Welcome to your project cockpit</h2></div><div class="card-body"><p>Understand what the repository knows before you change it.</p><div class="row g-3"><div class="col-md-6"><strong>Context first</strong><div class="text-secondary small">Read protocol, current state, and task memory.</div></div><div class="col-md-6"><strong>Evidence over confidence</strong><div class="text-secondary small">Trace claims to verification and findings.</div></div><div class="col-md-6"><strong>Handoffs that work</strong><div class="text-secondary small">Leave the next contributor a precise starting point.</div></div><div class="col-md-6"><strong>Searchable memory</strong><div class="text-secondary small">Find decisions, tasks, evidence, and failures.</div></div></div></div></div></div><div class="col-lg-5"><div class="card h-100"><div class="card-header"><h2 class="card-title">Current project status</h2></div><div class="card-body"><pre id="status">Loading…</pre></div></div></div>
      <div class="col-12" id="records"><div class="card"><div class="card-header"><h2 class="card-title">Recent records</h2></div><div class="card-body"><pre id="recordsData">Loading…</pre></div></div></div>
      <div class="col-12" id="search"><div class="card"><div class="card-header"><h2 class="card-title">Search project context</h2></div><div class="card-body"><div class="input-group"><input class="form-control" id="q" placeholder="Search decisions, tasks, evidence…"><button class="btn btn-primary" onclick="searchContext()">Search</button></div><pre id="results" class="mt-3">Type a query to search project memory.</pre></div></div></div></div>
    </div></div>
  </div>
</div>
<script>async function load(){try{const[s,r]=await Promise.all([fetch('/api/status'),fetch('/api/records?limit=30')]);const st=await s.json(),rec=await r.json();document.querySelector('#health').textContent=st.status||'Healthy';document.querySelector('#status').textContent=st.details||JSON.stringify(st,null,2);document.querySelector('#recordsData').textContent=JSON.stringify(rec,null,2);document.querySelector('#recordCount').textContent=rec.length}catch(e){document.querySelector('#status').textContent='Unable to load project status: '+e}}async function searchContext(){const q=document.querySelector('#q').value;if(!q)return;const r=await fetch('/api/search?q='+encodeURIComponent(q));document.querySelector('#results').textContent=JSON.stringify(await r.json(),null,2)}load()</script>
</body></html>
""";
}
