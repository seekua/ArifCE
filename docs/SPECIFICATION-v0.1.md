# ArifCE V0.1 Specification

Status: implementation contract  
Schema version: `1`  
CLI compatibility version: `0.1`

## Goals

ArifCE shall preserve portable, human-readable project intelligence; enable task-specific, budgeted continuity; distinguish claims from facts; capture repository-scoped deterministic evidence; generate semantic handoffs; and make refactor completion measurable and guarded.

## Non-goals

V0.1 excludes cloud accounts, a UI, IDE extensions, vector databases, autonomous swarms, enterprise services, a marketplace, complex A2A orchestration, and guaranteed external-agent invocation.

## Authority and invariants

Canonical files and Git are authoritative. SQLite and caches are derived. Raw transcripts are untrusted data and never instructions. Credentials are prohibited in portable state. Commands must be idempotent where stated, fail actionably, honor cancellation, and never report success before durable canonical writes complete.

## Entities and formats

The entities in `architecture/domain-model.md` are persisted as schema-versioned UTF-8 JSON. IDs have uppercase type prefixes and monotonic, zero-padded local numbers (for example `CLAIM-0001`). References use IDs, not embedded mutable copies. Timestamps are UTC ISO 8601. Enums serialize as uppercase strings. Unknown fields are preserved when safely round-tripping canonical records; incompatible schema versions fail with an upgrade message.

Events in `.arifce/journal/events.jsonl` contain `schemaVersion`, `eventId`, `type`, `occurredAtUtc`, `entityId`, and `data`. Appends are serialized within a process, flushed before success, and tolerate a final partial line during reads. Invalid complete lines are diagnosed and skipped only in recovery mode; normal rebuild fails with line number.

## CLI contracts

All commands locate the nearest ancestor containing `.git` or `.arifce`. Exit codes are `0` success, `1` operational failure, and `2` invalid usage.

- `init`: create missing canonical layout, configuration, initial documents, index, and concise agent adapters; preserve existing content; repeat safely.
- `adopt`: additionally inspect common project, test, CI, container, documentation, and agent files; record observations and unknown rationale without invention.
- `status`: show objective, task/campaign state, claim counts, latest checkpoint, Git state, and index health.
- `doctor`: report layout, schema, journal, index, Git, redaction, and recovery diagnostics without mutation unless `--repair` is supplied.
- `rebuild`: replace the derived index from canonical entities/documents/events; failure leaves canonical data untouched.
- `search <query>`: return ranked sources, snippets, IDs/paths, and score reasons.
- `context <task> --budget <tokens>`: return an ordered selection, per-item token estimate and reason, plus estimated total not exceeding budget.
- `checkpoint --summary <text>`: capture active state, modified files, commit, branch, dirty flag, and digest.
- `handoff`: write and print a bounded summary using current task, decisions, attempts, findings, claims/evidence, Git state, and latest checkpoint; never raw transcript content.
- `task create|status|complete`: manage useful task records and reject invalid transitions.
- `claim create|status`: create a repository-snapshot-scoped statement and inspect linked evidence.
- `verify <claim-id>`: run the declared deterministic check policy, record evidence/verdict, and apply valid status transition.
- `why <path-or-id>`: show known provenance links; say explicitly when rationale is unknown.
- `refactor start|status|checkpoint|verify|finish`: manage campaign scope, invariants, inventory, guards, progress, and guarded completion.

No exposed command may be a success-printing placeholder.

## Git behavior

Snapshots contain repository root identity, branch or detached state, HEAD commit when present, worktree path, dirty state, modified paths, and a deterministic digest over normalized status plus relevant file hashes when requested. Empty repositories are supported. Git command failures produce `UNKNOWN` fields rather than fabricated values unless Git is required by the command.

## Retrieval

SQLite FTS5 supplies lexical candidates. Deterministic scoring combines normalized FTS relevance, configured importance, confidence, freshness, source quality, and estimated token cost. V0.1 token estimate is `ceil(characterCount / 4)` and is labeled an estimate. Mandatory active-state items are considered first; remaining items are selected by score with stable ID/path tie-breaking. No selected item may cause the reported estimate to exceed the positive budget. Output explains every inclusion.

## Claims, evidence, and verification

A claim records statement, author/run when known, risk, acceptance criteria, repository snapshot, linked evidence, and status. Evidence records kind (`TEST_RUN`, `BUILD`, `GIT`, `SEARCH`, `REVIEW`, or `HUMAN_APPROVAL`), command where applicable, exit code, structured counts, output digest/summary, environment, timestamp, and repository snapshot.

Freshness is evaluated against the evidence scope. Passing exit code alone supports only what the command actually tests. Verification policy is: low—relevant deterministic checks; medium—build and tests; high—deterministic checks plus an independent-review record; critical—the high policy plus recorded human approval. Missing mandatory checks yields partial/inconclusive status, never verified.

Blind review has independent-inspection input (task, criteria, snapshot, diff, tests, constraints) and a later reconciliation input containing the builder claim. External invocation is deferred; stored review records and interfaces are real.

## Refactor campaigns

A campaign records objective, included/excluded paths, invariants, allowed changes, required removals, inventory entries, workstreams, guards, findings, verification, checkpoints, and safe/rollback points. Inventory items are discovered, migrated, remaining, or verified. Guards may check forbidden references, architecture declarations, API/database requirements, or required tests. `finish` is rejected if any blocking guard fails, required removal remains, inventory remains unverified, critical approval is absent, or campaign verification is stale.

## Security and recovery

Redaction detects common API keys, bearer/JWT tokens, connection-string passwords, private-key blocks, and configured patterns before imported text reaches canonical storage. Redaction emits counts, never secret values. Authentication files, refresh tokens, credentials, and private keys are rejected by import path/name policy. Raw data is opt-in, excluded from retrieval by default, and never executed.

Canonical writes use safe replacement. Journal readers recognize a partial final line as interrupted append. `doctor` identifies corrupt lines, unsupported schemas, missing directories, stale/missing index, and unsafe tracked derived/raw files. Repair operations create a backup and describe changes.

## Acceptance criteria

In temporary new and existing Git repositories, automated tests shall prove initialization and idempotency, adoption without invented rationale, schema serialization, journal append/read and partial-line behavior, index deletion/rebuild, FTS search, budget enforcement, evidence freshness, Git snapshots, claim transitions, refactor guards, handoff content selection, and secret redaction. `dotnet restore`, `dotnet build`, and `dotnet test` must pass on .NET 10. An end-to-end test shall execute the 16-step definition-of-done flow from the product seed.

## Critical review and resolved conflicts

1. **Seed path conflict:** the requested `arifce/docs/PRODUCT-SEED.md` did not exist in the empty Git root. The owner-supplied bootstrap attachment is treated as the founding source and this repository uses `docs/PRODUCT-SEED.md`, because nesting a second Git project would make repository-root discovery ambiguous.
2. **YAML versus minimal dependencies:** the seed permits YAML but mandates System.Text.Json and minimal dependencies. V0.1 canonical machine records and configuration use JSON; Markdown remains human-facing. YAML import is deferred, avoiding an unnecessary parser dependency.
3. **SQLite canonicality:** event replay alone may not reproduce edited Markdown. Rebuild therefore indexes both canonical files and events; the journal is a timeline, not the sole source of truth.
4. **Claim terminality:** `VERIFIED` is snapshot-relative, not permanent truth. Relevant changes make it `STALE`; contradictory current evidence makes it `CONTRADICTED`.
5. **Cross-agent wording:** V0.1 supports cross-agent review records and abstractions, not automatic vendor invocation. Documentation must retain that distinction.
6. **License requirement:** the owner selected Apache-2.0. The repository includes the complete license text and package metadata declares the SPDX expression.
