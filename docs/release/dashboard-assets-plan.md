# Dashboard asset refactor plan

The local dashboard currently serves a Tabler-based page assembled from embedded C# string literals. This works offline, but the large generated file is difficult to review and maintain.

The stable custom CSS and dashboard card controllers are now versioned local assets (`arifce-dashboard.css` plus the `dashboard-*.js` files) with explicit endpoints. The HTML shell and remaining helper markup stay embedded until browser smoke coverage is available.

## Target shape

- Keep the dashboard local-only and preserve the current routes/API contract.
- Move stable CSS and JavaScript into versioned static assets.
- Keep only a small HTML shell in the server project.
- Serve assets with explicit content types and no external CDN dependency.
- Preserve the real ArifCE logo and responsive sidebar behavior.

## Acceptance criteria

- Existing overview, records, search, workspace, provider, and handoff views render unchanged at desktop and mobile widths.
- A browser smoke test verifies navigation, API error rendering, and sidebar layout.
- Tabler asset loading works with network disabled.
- No inline executable content is introduced from repository records.
- The current embedded page remains available as a rollback fixture until the new asset path passes CI.

This is intentionally a separate visual refactor; no generated string is being split without screenshot and browser verification.
