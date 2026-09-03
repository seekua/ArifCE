# CLI Reference

ArifCE finds the nearest parent containing `.git` or `.arifce`. Successful commands return exit code `0`, operational failures return `1`, and usage errors return `2`.

## Project lifecycle

```text
arifce init
arifce adopt
arifce status
arifce doctor [--repair]
arifce rebuild
```

Rotate an oversized append-only journal into a timestamped local archive:

```bash
arifce journal rotate [--max-bytes N]
```

`init` is non-destructive and idempotent. `adopt` inspects an existing repository without inventing historical rationale. `doctor` is read-only unless `--repair` is supplied. Repair creates a timestamped journal backup, removes corrupt lines, atomically writes valid events, and rebuilds the derived index.

## Local workspace registry

The workspace registry is optional and local-only. It stores project names and absolute roots, never project records or secrets:

```text
arifce workspace list
arifce workspace add <name> <root>
arifce workspace remove <root>
```

Adding a missing root or a duplicate root fails. Removing an entry never deletes the project directory or its `.arifce/` data.

`workspace use <root>` selects a registered project for the local dashboard. The dashboard still honors `ARIFCE_PROJECT_ROOT` when explicitly set.

## Retrieval

```text
arifce search <query>
arifce context <task> [--budget <estimated-tokens>]
arifce context explain <task> [--budget <estimated-tokens>]
arifce why <path-or-id>
arifce knowledge audit
```

Search uses SQLite FTS5. Context assembly uses deterministic lexical candidates, record-type priority, trust-state filtering, and a positive token budget. It updates the disposable index before retrieval and reports candidate, selected, rejected, and token totals. `context explain` lists every candidate with its score, priority, freshness, cost, disposition, and reason. Stale evidence and claims, superseded decisions, non-current acceptances, and malformed typed records are rejected; disputed and unverified claims remain visible only with an explicit warning. `why` reports known provenance or explicitly says the historical rationale is unknown.

`knowledge audit` reads canonical decisions and claims directly and reports duplicates, conflicting active decisions, opposing equivalent claims, malformed records, and broken supersession links. Blocking conflicts return a failure after the report; the command never chooses a winner or rewrites records.

## Work and continuity

```text
arifce task create <title> [--risk <LOW|MEDIUM|HIGH|CRITICAL>]
arifce task status <task-id>
arifce task complete <task-id>

arifce decision create <title> --decision <text> [--rationale <text>]
arifce decision status <decision-id>
arifce decision supersede <decision-id> --by <active-replacement-id>

arifce attempt record <task-id> <approach> --result <result> --reason <text> [--evidence <id> ...]
arifce attempt status <attempt-id>

arifce finding create <title> --description <text> [--severity <level>] [--task <id>] [--path <path>]
arifce finding status <finding-id>
arifce finding resolve <finding-id>

arifce checkpoint --summary <text>
arifce handoff
```

Task risk defaults to `MEDIUM`. `--risk` must follow the title and accepts `LOW`, `MEDIUM`, `HIGH`, or `CRITICAL`; unsupported options are rejected rather than becoming title text.

Omitted historical rationale is stored as `Unknown.`. Decision creation is serialized and rejects a normalized title already held by an active decision. Supersession requires two distinct active decisions and preserves the replaced record with a link to its active replacement. Attempts must reference an existing task. Handoffs select current engineering state, include trust and knowledge warnings, and never dump raw transcripts.

## Claims and verification

```text
arifce claim create <statement>
arifce claim status <claim-id>
arifce verify <claim-id> --command <deterministic-command> [--path <file-or-directory>] [--path <file-or-directory>] [--contract <id>]
arifce architecture check <claim-id> --forbid <reference> --path <source-path>
```

Verification runs recognized `dotnet build` and `dotnet test` commands directly without a shell and records command, exit code, redacted output summary, Git snapshot, and structured metrics. Shell metacharacters prevent a command from entering this named path. Any other command is classified as unsafe and requires explicit `--allow-unsafe-command`; its evidence kind is `UNSAFE_COMMAND` and success can only support, never verify, a claim. Commands containing detectable secrets are blocked before execution.

```text
arifce verify CLAIM-0001 --command "dotnet test --configuration Release"
arifce verify CLAIM-0001 --command "custom-local-check" --allow-unsafe-command
```

The unsafe flag is a local execution approval, not evidence that the command is deterministic or trustworthy.

One or more `--path` values opt evidence into dependency-scoped freshness. ArifCE hashes each selected file or directory at verification time; unrelated repository changes then leave the evidence current, while an edit, deletion, creation, or rename inside the selected scope makes it stale. Omitting `--path` preserves the conservative repository-wide snapshot behavior for backward compatibility. Architecture, public API, and SQLite schema verification infer their scopes from their required path arguments.

`--contract <id>` requires that the contract belongs to the verified claim. It adds the exact/structural target closure and reverse transitive exact project dependents to the evidence scope; explicit `--path` values are additive. The closure itself is hashed so a newly added exact project dependent invalidates prior evidence. Heuristic code-graph edges are excluded and cannot create automatic stale state.

Run `arifce trust refresh` after repository changes or before a handoff. It compares scoped path digests when present and otherwise compares the legacy repository snapshot, moves affected claims to `STALE`, and marks linked accepted records as `NEEDS_REVIEW`. Handoff generation performs the same refresh and prints trust warnings. Changes under `.arifce/` are excluded from the repository-wide code-state fingerprint so recording evidence does not invalidate itself.

Repository snapshots expand untracked directories into their individual files and preserve literal Git filenames/rename endpoints. Git-reported unexpanded directories (for example an embedded repository) are rejected because their contents cannot be proved current by this snapshotter. Ignored files are not automatically included. Historical evidence is retained; corrected path handling can require re-verification of affected snapshots.

Build and query the disposable deterministic code graph:

```text
arifce codegraph build
arifce codegraph query Calculate
```

Project-reference edges are exact, declarations are structural, and identifier-based references or related-test candidates are explicitly heuristic. Code-graph output identifies possible impact; it is not verification evidence by itself.

The derived graph stores a digest of repository `.cs` and `.csproj` paths and contents plus its generator version. `codegraph query` and contract creation automatically rebuild after source edits, additions, deletions, renames, or scanner upgrades. Contract creation requires an exact symbol name. Legacy files and malformed derived graph JSON are rebuilt safely; continuously changing sources make the build fail instead of publishing a mixed snapshot. The dependency-free scanner covers common C# declarations but is not a complete compiler symbol model.

Create a pre-change engineering contract from a graph symbol:

```text
arifce contract create Calculate --risk HIGH --invariant "Financial rounding remains unchanged"
arifce contract status CONTRACT-0001
```

The contract collects impact candidates, related tests, matching historical records, invariants, and risk-derived verification requirements. It creates and references a normal claim; evidence, review, freshness, and acceptance continue through the existing claim lifecycle.

Record a bounded agent engineering run:

```text
arifce run start "Investigate payment calculation" --provider codex --agent builder --task TASK-0001
arifce run event RUN-0001 --kind ATTEMPT --summary "Tried cached totals" --outcome FAILED --exit-code 1
arifce run finish RUN-0001 --summary "Fallback implementation passed"
```

The recorder stores structured, redacted summaries rather than transcripts. A failed attempt linked to a task also becomes a canonical failed-attempt record so search and handoff can surface it later.

`architecture check` is a V0.2 deterministic evidence adapter. It scans only the explicitly supplied repository-local source paths (`.cs`, project, props, and targets files), excludes derived and raw directories, and records matching forbidden references with file and line numbers. A clean check proves only that the selected paths do not contain the supplied text references.

`api baseline <assembly-path> --baseline <path>` writes a normalized public API baseline for one selected .NET assembly. `api compare <assembly-path> --baseline <path>` reports added and removed public entries and exits non-zero when the selected baseline is not compatible. These commands inspect only the explicitly named assembly and baseline paths.

```text
arifce review record <claim-id> --reviewer <agent> --verdict <verdict> --summary <text> [--finding <id> ...]
arifce review status <review-id>

arifce acceptance create <claim-id> --actor <name> --rationale <text>
arifce acceptance status <acceptance-id>
arifce acceptance revoke <acceptance-id>
```

Review verdicts are `AGREE`, `PARTIALLY_AGREE`, `DISAGREE`, or `INCONCLUSIVE`. Positive agreement does not promote a claim. A disagreement can move an eligible claim to `DISPUTED`.

## Refactor campaigns

```text
arifce refactor start <title> <objective> [--invariant <text> ...] [--inventory <item> ...] [--forbid <reference> ...]
arifce refactor status <refactor-id>
arifce refactor checkpoint <refactor-id> <summary>
arifce refactor resolve <refactor-id> <inventory-item>
arifce refactor workstream <refactor-id> <name> --owner <agent> --path <scope> [--path <scope> ...]
arifce refactor safepoint <refactor-id> <name> [--note <text>]
arifce refactor verify <refactor-id>
arifce refactor finish <refactor-id>
arifce refactor abandon <refactor-id>
```

Blocking inventory or forbidden-reference guards prevent finish. Workstreams are coordination metadata only; V0.1 does not create worktrees or invoke agents. Safe points capture Git state but do not execute rollback.
# LLM commands

```text
arifce llm provider list
arifce llm provider add <id> <kind> <model> [--endpoint <url>] [--api-key-env <name>] [--api-key-stdin]
arifce llm provider test <id>
arifce llm provider remove <id>
arifce llm context <task> [--budget N]
arifce llm run <task> <prompt> [--claim <id>] [--with-context] [--budget N]
arifce llm review <claim> <prompt> --reviewer <name> --rationale <text> --approved
arifce llm benchmark <prompt> --expected <text>
```

`llm context` previews the same trust-aware context assembly used by `context` and reports its telemetry. `llm run --with-context` injects that bounded memory into the prompt automatically. The MCP `arifce_context` tool returns the same structured items and telemetry rather than maintaining a separate selection path. Runs use enabled profiles with fallback and persist canonical evidence. API keys remain local and should be supplied through an environment variable or stdin.

`llm benchmark` runs a deterministic comparison case and reports provider, expected-token recall (`TokenRecall`), latency, tokens, and estimated cost. The pass threshold is a coarse smoke-test threshold, not a quality score.
