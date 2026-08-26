# Roadmap

This roadmap distinguishes implemented behavior from planned work. Nothing listed here should be read as shipped unless the release checklist links passing evidence.

## V0.1 implementation phases

- [x] Phase 0: inspect the empty repository and Git state.
- [x] Phase 1: define the product contract, statuses, layout, and honest claims.
- [x] Phase 2: bootstrap the .NET 10 solution and CLI.
- [x] Phase 3: canonical store, JSONL journal, SQLite FTS5 index, rebuild.
- [ ] Phase 4: task create/status/complete, checkpoints, Git snapshots, and canonical attempts work; dedicated decision/attempt authoring commands remain.
- [x] Phase 5: deterministic lexical context retrieval with budget enforcement.
- [x] Phase 6: claims, command evidence, freshness comparison, and status transitions.
- [ ] Phase 7: deterministic command verification and structured .NET build/test metrics work; typed blind-review interfaces remain.
- [x] Phase 8: semantic handoffs from current state, task, checkpoint, claim, and Git state.
- [ ] Phase 9: CLI campaign lifecycle, invariants, inventory resolution, forbidden-reference guards, verification, finish, and abandonment work; workstreams and rollback points remain.
- [x] Phase 10: concise Codex, Claude Code, and OpenCode adapters.
- [ ] Phase 11: redaction, diagnostics, and partial-final-line recovery work; explicit repair/backup workflow remains.
- [ ] Phase 12: core documentation matches behavior; the full manual set remains deferred below.
- [ ] Phase 13: build/tests, a service-level definition-of-done fixture, CLI dogfood, and packaged global-tool smoke test pass; license, observed cross-platform CI results, and a complete packaged CLI end-to-end fixture remain release blockers.

## Explicit deferrals

- **External semantic reviewer invocation:** V0.1 defines typed blind-review interfaces but does not pretend to invoke Codex, Claude, or OpenCode reliably. Vendor authentication, process control, cost policy, and capability discovery require dedicated integrations.
- **MCP server:** deferred until filesystem and CLI contracts are stable. The core does not depend on MCP.
- **A2A and multi-worktree coordination:** domain metadata remains extensible, but orchestration is outside V0.1.
- **Vector search, cloud service, UI, IDE extension, and autonomous swarms:** explicit V0.1 non-goals.
- **Benchmark results:** only the benchmark protocol is defined until repeatable experiments are run; no effectiveness percentages will be claimed.
- **License selection:** permissive MIT and Apache-2.0 are candidates. The repository owner must choose before public release; a placeholder-free final `LICENSE` will then be added.
- **Dedicated decision, attempt, finding, review, and evidence-authoring commands:** canonical directories and types exist, but V0.1 currently exposes only task creation, checkpoints, command evidence, claims, and handoffs. Adding empty commands would violate the product contract.
- **Advanced refactor coordination:** CLI options now author invariants, inventory, and forbidden-reference guards and can resolve inventory, verify, finish, or abandon campaigns. Workstream ownership, path partitioning, safe points, and rollback metadata remain deferred until multi-worktree policy is specified.
- **Full documentation tree:** high-value contract and architecture documents exist. Topic files that would only repeat the specification are deferred until their corresponding command contracts stabilize.
- **Additional automated evidence kinds:** .NET test/build commands are classified and localized summary counts are stored as structured metrics. API-diff, architecture-boundary, and database-compatibility adapters are not yet implemented.

## Environment note

The workstation initially exposed only .NET SDK 9.0.317. .NET SDK 10.0.400 was installed during bootstrap, and the solution now builds and tests on `net10.0`.
