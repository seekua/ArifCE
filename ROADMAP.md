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

## V0.8 trust and distribution phases

- [x] Phase 42: harden evidence freshness, acceptance rules, secret boundaries, and atomic canonical mutations.
- [x] Phase 43: propagate stale trust state into claims, acceptances, and handoffs.
- [x] Phase 44: add deterministic code relationships and change impact contracts without introducing a graph database.
- [x] Phase 45: add structured agent flight-recorder records and strict MCP/verification boundaries.
- [x] Phase 46: add a matched ten-category engineering benchmark protocol without unmeasured effectiveness claims.
- [x] Phase 47: verify self-contained binaries on five native targets and enforce immutable release assets.
- [x] Phase 48: publish V0.8.0 and record the tag workflow, checksums, assets, and release evidence.

## V0.9 measured-value phases

- [x] Phase 49: enforce history-free, matched benchmark trial isolation with reproducible fixture trees and cross-platform smoke coverage.
- [x] Phase 50: capture tamper-evident raw run provenance and deterministic evaluator outcomes without accepting hand-authored success claims.
- [x] Phase 51: execute and publish the first complete matched engineering benchmark, including negative results and limitations. The first run is inconclusive and makes no effectiveness claim because its only differing pass was permission-confounded.
- [x] Phase 52: unify CLI, MCP, and provider context assembly behind deterministic ranking, trust filtering, budget telemetry, and per-candidate inclusion/exclusion explanations.
- [x] Phase 53: add backward-compatible file and directory dependency scopes to evidence freshness, inferred by deterministic verification commands and optionally declared by generic verification.
- [x] Phase 54: detect canonical decision/claim duplicates and conflicts, preserve explicit decision supersession, and surface unresolved knowledge warnings in context and handoffs.
- [x] Phase 55: make deterministic code-graph reads verify their source-tree digest and atomically rebuild stale, legacy, or corrupt derived graphs before impact analysis.
- [x] Phase 56: reduce deterministic C# method-declaration false positives, version the graph generator, and publish a bounded real-repository noise audit before dependency-closure work.
- [x] Phase 57: bind verification evidence explicitly to change contracts, propagate stale state through exact/structural dependency closure, and prove false-stale boundaries without trusting heuristic edges.
- [x] Phase 58: parse task risk explicitly, reject unsupported task-create options, and prove canonical title/risk integrity through packaged CLI smoke coverage.
- [x] Phase 59: add deterministic path-qualified graph targets so same-name declarations in other files cannot broaden a trusted contract closure.
- [x] Phase 60: fail closed when Git repository state cannot be captured, with cross-platform regression coverage using real Git fixtures.
- [x] Phase 61: confine Git snapshot paths to the repository root and reject traversal before hashing file content.
- [x] Phase 62: replace C# declaration regex scanning with a Roslyn syntax adapter and version derived graphs.
- [x] Phase 63: expose parser-backed C# call candidates for graph exploration while retaining their heuristic, non-trust status.
- [x] Phase 64: record exact C# type-member ownership in the disposable graph without inferring semantic dependencies.

## Explicit deferrals

- **Self-contained distribution:** the repeatable CLI packaging script, dependency policy, five-target native smoke matrix, archive-level checksums, and immutable release publication are proven by [`v0.8.0`](https://github.com/seekua/ArifCE/releases/tag/v0.8.0). [Tag CI run 33496418909](https://github.com/seekua/ArifCE/actions/runs/33496418909) and [release workflow run 33496418947](https://github.com/seekua/ArifCE/actions/runs/33496418947) passed. The five public archives were downloaded and verified against the release-level and archive-internal checksums. Signing, package-manager manifests, and NativeAOT compatibility remain deferred.
- **V0.7 release operations:** the versioned release checklist is prepared; tagging, signing, immutable asset attachment, and external package-manager submissions remain maintainer-controlled steps.
- **Semantic contradiction discovery:** Phase 54 detects normalized same-title decisions and equivalent claim statements deterministically. Differently worded semantic contradictions, automatic winner selection, and LLM-authored consolidation remain deferred because they could manufacture false certainty; unresolved cases require explicit review and supersession.
- **General dependency-closure evidence:** Phase 57 limits automatic invalidation to explicitly selected contracts, declaration/file structure, and exact reverse project references. Heuristic callers/tests, semantic dependencies, and implicit expansion for evidence without `--contract` remain deferred because false invalidation would undermine trust.
- **Parser-backed code intelligence:** Phases 62–64 add Roslyn-backed declarations, constructors, distinct overload nodes, explicit-interface methods, heuristic invocation candidates, and direct lexical type-member ownership. Operator declarations, compiler-bound overload/dispatch resolution, semantic dependencies, and non-C# language adapters remain deferred. Graph IDs identify declarations within a source snapshot rather than persistent semantic symbols; heuristic edges are not eligible to prove dependency closure.

GitHub repository description/topics remain an external account-setting task. No local `GITHUB_TOKEN` is available in this environment, so metadata has not been claimed as updated; repository code and releases continue to be pushed normally. Journal rotation is now implemented with a size threshold and timestamped archive.

### Independent technical review (2026-08-30)

The external review was cross-checked against the current `master` tree. Thirteen concrete findings are now applied: user search input is normalized into a safe FTS5 literal query (preventing syntax errors from punctuation such as `-`, `:`, and parentheses), common stopwords are removed, and context snippets scale with the requested budget; canonical redaction covers common provider tokens, credential assignments, and credential-bearing connection strings; new record IDs reserve an atomic `.reserve` marker, use unique temporary files, and never silently overwrite a competing create; generic arbitrary-command verification is recorded as `UNVERIFIED_COMMAND`/`SUPPORTED` rather than falsely promoted to `VERIFIED`; handoffs render compact field summaries instead of raw JSON; `CURRENT.md` soft/hard size warnings are reported by `doctor` and bounded in handoffs; MCP exposes typed canonical write tools for the core record lifecycle; README/package versions are checked in CI; benchmark output is explicitly named and documented as `TokenRecall` smoke-test coverage rather than a quality score; journal reads stream line-by-line while appends use an async file stream; `doctor` warns when the journal reaches 10 MB/50 MB rotation thresholds; and the embedding component is explicitly named/documented as a deterministic hash-vector test stub, not semantic search. Deterministic adapters remain eligible for verified status. The behavior suite passes (44 tests).

The review's larger recommendations remain deliberately scoped rather than silently treated as complete. Atomic/merge-safe record IDs, richer retrieval ranking and full-record budgeting, named verification checks, journal rotation, NativeAOT/npm distribution, OKF migration, and empirical A/B value measurement require separate owner-approved phases. Incremental canonical indexing is now implemented with a persisted manifest and delta entity updates; FTS5 projection rebuilds only when the manifest changes. An `.editorconfig` baseline is now present; a strict `dotnet format --verify-no-changes` CI gate is deferred until the existing generated/dashboard-heavy files are normalized (the current formatter check reports legacy whitespace violations). The review's requests to remove localized READMEs, dashboard, LLM platform, or benchmark are product-direction opinions and conflict with the current owner-approved scope; they are not adopted.

Remote validation after the audit passed on Ubuntu, macOS, and Windows in [Actions run #33339893986](https://github.com/seekua/ArifCE/actions/runs/33339893986) for commit `968775e`.

- **Post-V0.7 local LLM platform:** completed and verified by remote CI run [33311981481](https://github.com/seekua/ArifCE/actions/runs/33311981481) at commit `253b160`; see [verification checklist](docs/release/llm-platform-checklist.md). 

- **Local LLM provider platform:** OpenAI, Anthropic, Gemini, OpenRouter, Ollama, and LM Studio adapters, local profile storage, fallback/task routing, connection tests, token/cost accounting, canonical response evidence, explicit reviewer approval, dashboard model/cost projections, deterministic hash-vector test selection, approved MCP execution, and local A2A handoffs are implemented and tested. Semantic embeddings, hosted vector stores, vendor notifications, issue automation, and full IDE-native extensions remain opt-in integrations.

- **Acceptance lifecycle:** implemented as a separate canonical record in the current release; future policy engines may add configurable approver roles and multi-stage approval without changing the claim model.

- **External semantic reviewer invocation:** V0.1 defines typed blind-review interfaces but does not pretend to invoke Codex, Claude, or OpenCode reliably. Vendor authentication, process control, cost policy, and capability discovery require dedicated integrations.
- **Advanced MCP surface:** the local stdio MCP server is implemented in V0.3. Shell-backed verification and broad mutation tools remain deferred until a stronger command policy exists.
- **A2A and multi-worktree coordination:** local sequential A2A handoffs and multi-project workspace switching are implemented; autonomous worktree creation, assignment, merging, and rollback remain deferred.
- **Vector search, cloud service, full IDE extension, and autonomous swarms:** explicit future-phase scope. A local dashboard and IDE connection manifest are implemented; a full IDE-native experience is not yet shipped.
- **Benchmark results:** the first complete ten-pair execution is [published with all negative outcomes and limitations](docs/evidence/engineering-benchmark-results-2026-09-02.md). Its raw independent pass count was baseline 3/10 and ArifCE 4/10, but the only differing pass was caused by write-permission variance. Tokens were unavailable and elapsed durations included queue/approval delay. No effectiveness percentage is claimed; repeated, pre-authorized runs with provider telemetry remain deferred.
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
