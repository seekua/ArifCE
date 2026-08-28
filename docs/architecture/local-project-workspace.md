# Local project workspace contract

ArifCE remains local-first. A workspace may reference multiple projects, but every project keeps its own `.arifce/` directory and canonical records. The workspace is an index of local roots, not a shared memory store.

## Invariants

- A project root must be an existing directory containing a Git repository or an explicit adoption target.
- Project records never cross project boundaries during context retrieval, dashboard queries, rebuilds, or verification.
- SQLite remains a disposable per-project index; no workspace-level database becomes authoritative.
- Removing a workspace entry never deletes the referenced project or its `.arifce/` data.
- No network access, cloud synchronization, credential forwarding, or remote mutation is implied by workspace membership.

## Planned local registry

The future local registry will contain only a display name, canonical absolute root, last-seen timestamp, and optional color. It must reject duplicate roots, normalize path casing on Windows, and validate the root before opening a project. The registry itself contains no project records or secrets.

## Dashboard behavior

The dashboard will show the active project explicitly and require an intentional switch before changing scope. Every list, count, search result, and status card must be derived from the active project root. A missing or invalid root produces a recoverable error and cannot silently fall back to another project.

This contract is design-only until the local registry, safe switching flow, isolation tests, and migration guidance are implemented together.
