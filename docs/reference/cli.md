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

`init` is non-destructive and idempotent. `adopt` inspects an existing repository without inventing historical rationale. `doctor` is read-only unless `--repair` is supplied. Repair creates a timestamped journal backup, removes corrupt lines, atomically writes valid events, and rebuilds the derived index.

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
```

Verification executes the user-supplied command in the project root and records command, exit code, output summary, Git snapshot, and structured .NET build/test metrics when recognized. Medium-risk claims become `SUPPORTED`, not automatically `VERIFIED`, after one successful command.

```text
arifce review record <claim-id> --reviewer <agent> --verdict <verdict> --summary <text> [--finding <id> ...]
arifce review status <review-id>
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
