# ArifCE V0.2 Specification

Status: active implementation contract  
Schema version: `1`

## Objective

V0.2 makes three repository-local checks first-class, deterministic evidence adapters: architecture boundaries, public API surface compatibility, and SQLite schema compatibility. Each adapter must describe exactly what it checks, report its inputs and result, and never claim broader coverage.

## Scope

1. **Architecture boundary evidence** checks configured source roots for forbidden namespace or project-reference relationships. It reports every matching file and line and fails when a blocking rule matches.
2. **Public API surface evidence** generates a normalized, deterministic baseline from explicitly selected .NET assemblies and compares a later run against that baseline. Additions, removals, and signature changes are reported separately.
3. **SQLite schema compatibility evidence** captures normalized table, column, index, and foreign-key metadata from an explicitly selected database. Later runs report added, removed, and changed schema elements.

Each adapter writes an `EvidenceRecord` linked to an existing claim, captures the Git snapshot before the check, and emits a bounded machine-readable summary. A successful process exit supports only the named adapter's contract.

## Safety and determinism

- Paths are resolved beneath the repository root; derived, raw, backup, Git, build, and artifact directories are excluded by default.
- Checks do not alter source, assemblies, or databases. Baseline creation is explicit and uses atomic canonical writes.
- Adapter output is sorted with ordinal comparison and normalizes line endings and paths.
- Missing inputs, unreadable files, unsupported assembly metadata, or locked databases produce actionable failure evidence rather than fabricated success.
- Binary, credential, and raw transcript paths are not scanned as source inputs.

## V0.2 non-goals

V0.2 does not add remote services, embeddings, a UI, IDE extensions, MCP, A2A orchestration, autonomous worktrees, or automatic invocation of external reviewers. Those require separate authority, credentials, lifecycle policy, or an owner-approved product decision.

## Acceptance criteria

Automated tests must prove deterministic ordering, root-path confinement, positive and negative adapter results, meaningful change summaries, snapshot-linked evidence, and preservation of the V0.1 CLI behavior. The packaged global-tool fixture and Windows, Ubuntu, and macOS CI matrix must pass before a V0.2 release claim.
