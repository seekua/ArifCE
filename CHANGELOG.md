# Changelog

- Start V0.2 with an explicit deterministic verification-adapter contract covering architecture, API surface, and SQLite schema compatibility.
- Add repository-confined, deterministic architecture-boundary evidence with actionable file-and-line findings and packaged CLI coverage.
- Begin the V0.2 public API surface adapter with normalized assembly baselines and compatibility comparison commands.
- Prepare V0.2 release metadata and a consolidated release-readiness checklist.
- Publish GitHub Release [`v0.2.0`](https://github.com/seekua/ArifCE/releases/tag/v0.2.0) with the NuGet tool package and SHA-256 checksum.

- Select Apache-2.0 for the repository and NuGet tool package.
- Record the successful Windows, Ubuntu, and macOS GitHub Actions run that closes the V0.1 release checklist.
- Replace placeholder NuGet publication metadata with the V0.1 product identity and repository information.
- Publish GitHub Release [`v0.1.0`](https://github.com/seekua/ArifCE/releases/tag/v0.1.0) with the verified global-tool package and SHA-256 checksum.
- Upgrade CI to Node 24-based `actions/checkout@v5` and `actions/setup-dotnet@v5`; the verified matrix no longer emits Node 20 deprecation annotations.

All notable changes will be documented here. The project follows semantic versioning after its first published release.

## Unreleased

- Defined the V0.1 product, domain, storage, lifecycle, and CLI contracts.
- Added the .NET 10 core, canonical project store, JSONL journal, SQLite FTS5 index, Git snapshots, retrieval, claims/evidence, verification, handoffs, refactor guards, redaction, diagnostics, agent adapters, and behavior tests.
- Dogfooded initialization and continuity records in this repository.
- Added structured English/Turkish .NET build and test evidence metrics.
- Added task status/completion and claim status commands.
- Added CLI refactor invariants, inventory resolution, forbidden-reference guards, deterministic verification, guarded finish, and abandonment.
- Added a README-bearing global-tool package, repeatable package smoke test, and Windows/Linux/macOS CI matrix.
- Expanded the packaged fixture through the complete continuity, verification, refactor, handoff, and index-recovery flow.
- Added vendor-neutral two-phase blind-review contracts and risk-based verification requirements.
- Added refactor workstream ownership/path scopes and Git-snapshot safe points.
- Added canonical decision and failed-attempt authoring commands.
- Added read-only journal diagnosis and explicit backup-first `doctor --repair` recovery.
- Added a value-safe tracked-file secret scan and CI enforcement.
- Added canonical finding lifecycle and manual review records linked to claims and findings.
- Ensured positive semantic agreement cannot verify a claim while disagreement can dispute it.
- Aligned canonical enum output with the specified `UPPER_SNAKE_CASE` format while retaining legacy read compatibility.
- Completed the V0.1 getting-started, concepts, architecture, agent, reference, research, and release documentation sets.
