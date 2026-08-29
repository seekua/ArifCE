# Roadmap

This roadmap distinguishes implemented behavior from planned work. Nothing listed here should be read as shipped unless the release checklist links passing evidence.

## V0.1 implementation phases

- [x] Phase 0: inspect the empty repository and Git state.
- [x] Phase 1: define the product contract, statuses, layout, and honest claims.
- [x] Phase 2: bootstrap the .NET 10 solution and CLI.
- [x] Phase 3: canonical store, JSONL journal, SQLite FTS5 index, rebuild.
- [x] Phase 4: project memory, task create/status/complete, decision and failed-attempt authoring, checkpoints, and Git snapshots work.
- [x] Phase 5: deterministic lexical context retrieval with budget enforcement.
- [x] Phase 6: claims, command evidence, freshness comparison, and status transitions.
- [x] Phase 7: deterministic verification, structured .NET build/test metrics, risk policy, and typed two-phase blind-review interfaces work; external agent invocation remains explicitly deferred.
- [x] Phase 8: semantic handoffs from current state, task, checkpoint, claim, and Git state.
- [x] Phase 9: campaign lifecycle, invariants, inventory, guards, findings, checkpoints, workstream ownership/path scopes, Git safe points, verification, finish, and abandonment work. Autonomous worktree orchestration remains a non-goal.
- [x] Phase 10: concise Codex, Claude Code, and OpenCode adapters.
- [x] Phase 11: redaction, diagnostics, partial/corrupt journal detection, backup-first repair, and index recovery work.
- [x] Phase 12: getting-started, concepts, architecture, agent, reference, research, and release documentation match implemented behavior and mark deferred boundaries explicitly.
- [x] Phase 13: build/tests, CLI dogfood, the complete packaged global-tool definition-of-done fixture, Apache-2.0 licensing, and observed Windows/Ubuntu/macOS CI results pass.

## V0.2 implementation phases

- [x] Phase 14: define the deterministic verification-adapter contract, safety boundaries, acceptance criteria, and explicit V0.2 non-goals.
- [x] Phase 15: implement configured architecture-boundary evidence with deterministic source scanning, actionable findings, package-fixture coverage, and observed CI evidence.
- [x] Phase 16: implement normalized public API surface baselines and compatibility diffs for selected .NET assemblies.
- [x] Phase 17: implement normalized SQLite schema baselines and compatibility diffs for selected databases.
- [x] Phase 18: complete V0.2 documentation, package fixture coverage, and observed Windows/Ubuntu/macOS CI evidence.

## V0.3 implementation phases

- [x] Phase 19: define the local-first MCP transport, tool contract, security boundaries, and compatibility policy.
- [x] Phase 20: implement the MCP server adapter over existing application services without creating a second source of truth.
- [x] Phase 21: add deterministic MCP protocol tests, malformed-input handling, capability discovery, and fixture coverage.
- [x] Phase 22: document MCP setup for coding agents and record observed cross-platform CI evidence.
- [x] Phase 23: design the UI/IDE integration boundary and A2A/multi-worktree contracts; implementation remains separately gated.
- [x] Phase 24: define the benchmark protocol for retrieval and verification quality; do not claim effectiveness before repeatable runs.

## V0.4 implementation phases

- [x] Phase 25: define the local-only dashboard and expanded MCP safety contract.
- [x] Phase 26: implement a local dashboard for status, tasks, decisions, evidence, findings, and handoffs.
- [x] Phase 27: expose narrowly scoped refactor inspection tools through MCP with explicit validation; shell-backed verification remains deferred for a stronger command policy.
- [x] Phase 28: add deterministic UI/MCP tests and observed cross-platform CI evidence.
- [x] Phase 29: complete V0.4 documentation and release readiness; cloud remains deferred.

## V0.5 implementation phases

- [x] Phase 30: define dashboard record projections and IDE local-connection contract.
- [x] Phase 31: expose project record lists and summary panels in the local dashboard.
- [x] Phase 32: add an IDE integration manifest that launches the local dashboard/MCP boundaries.
- [x] Phase 33: add deterministic API/UI smoke tests and CI evidence.
- [x] Phase 34: complete V0.5 documentation and release readiness.

## V0.6 implementation phases

- [x] Phase 35: define separate claim acceptance semantics and safety gates.
- [x] Phase 36: implement acceptance records, CLI lifecycle, tests, and documentation.
- [x] Phase 37: package and publish V0.6.0 with checksum and release evidence.

## V0.7 planned phases

- [x] Phase 38: complete and review full translations for each localized README with the translator-review agent, preserving canonical commands, links, diagrams, badges, security language, and explicit deferrals. A human linguistic sign-off remains optional and is tracked separately.
- [x] Phase 39: add a reviewed-language gate so CI can distinguish canonical scope parity from human translation review status. The default CI check reports pending languages without falsely marking them reviewed; `-RequireReviewed` is available for a release gate.
- [x] Phase 40: improve dashboard decision-maker summaries with agent attribution, latest action, evidence freshness, and project-level filters.
- [x] Phase 41: implement the local project switcher and multi-project workspace contract without introducing cloud synchronization. Registry storage, CLI list/add/remove/use commands, read-only dashboard API, workspace display, active switching, and isolation tests are complete.

V0.7 starts with documentation integrity. No language is marked reviewed until its complete canonical content has been translated and checked by a human.

## Explicit deferrals

- **Post-V0.7 local LLM platform:** completed and verified by remote CI run [33278676898](https://github.com/seekua/ArifCE/actions/runs/33278676898) at commit `95327e3`; see [verification checklist](docs/release/llm-platform-checklist.md).

- **Local LLM provider platform:** OpenAI, Anthropic, Gemini, OpenRouter, Ollama, and LM Studio adapters, local profile storage, fallback/task routing, connection tests, token/cost accounting, canonical response evidence, explicit reviewer approval, dashboard model/cost projections, local embedding selection, approved MCP execution, and local A2A handoffs are implemented and tested. Hosted vector stores, vendor notifications, issue automation, and full IDE-native extensions remain opt-in integrations.

- **Acceptance lifecycle:** implemented as a separate canonical record in the current release; future policy engines may add configurable approver roles and multi-stage approval without changing the claim model.

- **External semantic reviewer invocation:** V0.1 defines typed blind-review interfaces but does not pretend to invoke Codex, Claude, or OpenCode reliably. Vendor authentication, process control, cost policy, and capability discovery require dedicated integrations.
- **Advanced MCP surface:** the local stdio MCP server is implemented in V0.3. Shell-backed verification and broad mutation tools remain deferred until a stronger command policy exists.
- **A2A and multi-worktree coordination:** local sequential A2A handoffs and multi-project workspace switching are implemented; autonomous worktree creation, assignment, merging, and rollback remain deferred.
- **Vector search, cloud service, full IDE extension, and autonomous swarms:** explicit future-phase scope. A local dashboard and IDE connection manifest are implemented; a full IDE-native experience is not yet shipped.
- **Benchmark results:** only the benchmark protocol is defined until repeatable experiments are run; no effectiveness percentages will be claimed.
- **Manual evidence authoring:** decision, failed-attempt, finding, and review commands persist canonical records. Deterministic command evidence remains available through `verify`; arbitrary manual evidence waits for a provenance and trust policy.
- **Autonomous refactor coordination:** CLI metadata now covers invariants, inventory, forbidden-reference guards, workstream ownership/path scopes, and Git-snapshot safe points. Creating worktrees, assigning agents, merging, and rollback execution remain explicit post-V0.1 orchestration work.
- **Additional automated evidence kinds:** .NET test/build commands are classified and localized summary counts are stored as structured metrics. Architecture-boundary, public API, and SQLite schema evidence are implemented in V0.2. Future evidence kinds require a new owner-approved scope.
- **GitHub Actions runtime maintenance:** the successful V0.1 run emitted Node 20 deprecation annotations for `actions/checkout@v4` and `actions/setup-dotnet@v4`. `FINDING-0002` tracks the non-blocking action-major upgrade.
- **External integrations:** A2A orchestration, vendor reviewer invocation, cloud services, and full IDE integrations remain outside V0.5. They require an owner-approved scope and dedicated security/lifecycle design.
- **GitHub account avatar:** repository Social Preview branding is shipped. GitHub does not provide a repository-specific avatar; changing the `seekua` account or organization avatar remains an account-level operation outside the repository release scope.
- **Localized README parity:** all 21 language files now retain the complete canonical README reference and pass CI marker checks. Full human-quality translation of every canonical paragraph remains a follow-up because automated translation would risk altering commands, links, and safety language; the English canonical file remains authoritative until each language is reviewed.
- **DeepL translation pass:** `scripts/translate-readme-locales.ps1` batches paragraph-level DeepL web requests and protects executable Markdown. The public endpoint currently rate-limits automated batches; Bengali, Bosnian, and Thai are not exposed as DeepL web targets, so those translations remain explicitly deferred until an approved provider or credentials are available.

## Environment note

The workstation initially exposed only .NET SDK 9.0.317. .NET SDK 10.0.400 was installed during bootstrap, and the solution now builds and tests on `net10.0`.
