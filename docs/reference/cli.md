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
arifce why <path-or-id>
```

Search uses SQLite FTS5. Context selection uses deterministic lexical matches, reports inclusion reasons and estimated token costs, and does not exceed a positive budget. `why` reports known provenance or explicitly says the historical rationale is unknown.

## Work and continuity

```text
arifce task create <title>
arifce task status <task-id>
arifce task complete <task-id>

arifce decision create <title> --decision <text> [--rationale <text>]
arifce decision status <decision-id>

arifce attempt record <task-id> <approach> --result <result> --reason <text> [--evidence <id> ...]
arifce attempt status <attempt-id>

arifce finding create <title> --description <text> [--severity <level>] [--task <id>] [--path <path>]
arifce finding status <finding-id>
arifce finding resolve <finding-id>

arifce checkpoint --summary <text>
arifce handoff
```

Omitted historical rationale is stored as `Unknown.`. Attempts must reference an existing task. Handoffs select current engineering state and never dump raw transcripts.

## Claims and verification

```text
arifce claim create <statement>
arifce claim status <claim-id>
arifce verify <claim-id> --command <deterministic-command>
arifce architecture check <claim-id> --forbid <reference> --path <source-path>
```

Verification executes the user-supplied command in the project root and records command, exit code, output summary, Git snapshot, and structured .NET build/test metrics when recognized. Medium-risk claims become `SUPPORTED`, not automatically `VERIFIED`, after one successful command.

Run `arifce trust refresh` after repository changes or before a handoff. It compares current repository content with recorded evidence snapshots, moves affected claims to `STALE`, and marks linked accepted records as `NEEDS_REVIEW`. Handoff generation performs the same refresh and prints trust warnings. Changes under `.arifce/` are excluded from the code-state fingerprint so recording evidence does not invalidate itself.

Build and query the disposable deterministic code graph:

```text
arifce codegraph build
arifce codegraph query Calculate
```

Project-reference edges are exact, declarations are structural, and identifier-based references or related-test candidates are explicitly heuristic. Code-graph output identifies possible impact; it is not verification evidence by itself.

Create a pre-change engineering contract from a graph symbol:

```text
arifce contract create Calculate --risk HIGH --invariant "Financial rounding remains unchanged"
arifce contract status CONTRACT-0001
```

The contract collects impact candidates, related tests, matching historical records, invariants, and risk-derived verification requirements. It creates and references a normal claim; evidence, review, freshness, and acceptance continue through the existing claim lifecycle.

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

`llm context` previews indexed repository memory under a token budget. `llm run --with-context` injects that bounded memory into the prompt automatically. Runs use enabled profiles with fallback and persist canonical evidence. API keys remain local and should be supplied through an environment variable or stdin.

`llm benchmark` runs a deterministic comparison case and reports provider, expected-token recall (`TokenRecall`), latency, tokens, and estimated cost. The pass threshold is a coarse smoke-test threshold, not a quality score.
