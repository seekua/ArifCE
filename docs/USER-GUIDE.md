# ArifCE User Guide

ArifCE is a local-first continuity layer for software projects worked on by people and coding agents. Install the CLI once per development environment; initialize project intelligence once per Git repository.

## Install

Requirements: Git and the .NET 10 SDK.

```bash
dotnet tool install --global ArifCE.Cli --version 0.6.0
arifce help
```

Optional local dashboard:

```bash
dotnet tool install --global ArifCE.Dashboard --version 0.5.0
arifce-dashboard
```

The dashboard binds to `http://127.0.0.1:5180`, uses the locally bundled Tabler UI styles, and never requires cloud access.

## Start a project

```bash
cd my-repository
git init
arifce init
arifce status
```

Use `arifce adopt` for an existing repository. Both operations are non-destructive and idempotent. Project state is stored under `.arifce/`; the SQLite index is derived and rebuildable.

## Tasks, decisions, and attempts

```bash
arifce task create "Ship tenant-aware caching"
arifce task status TASK-0001
arifce decision create "Use local cache" --decision "Avoid network dependency" --rationale "Local-first requirement"
arifce attempt record TASK-0001 "Redis invalidation" --result rejected --reason "Reconnect reliability risk"
```

Attempts must reference a task. Unknown historical rationale is stored as `Unknown.`.

## Checkpoints and handoffs

```bash
arifce checkpoint --summary "Implementation started"
arifce handoff
```

A handoff summarizes current state, decisions, failed attempts, evidence, findings, and Git state. It is not a transcript dump.

## Search, context, and tags

```bash
arifce search "tenant cache"
arifce context "finish tenant-aware caching" --budget 4000
arifce why decisions/adr-0001.json
```

Search is deterministic SQLite FTS5 lexical search. `context` selects ranked sources within an estimated token budget. `why` reports provenance or says when rationale is unknown.

ArifCE does not currently expose a standalone `tag` command or free-form tag index. Use searchable words in titles and summaries; `search` indexes canonical records. A future tag system must preserve provenance.

## Claims, evidence, and review

```bash
arifce claim create "The cache tests pass"
arifce verify CLAIM-0001 --command "dotnet test"
arifce claim status CLAIM-0001
arifce review record CLAIM-0001 --reviewer "agent-a" --verdict INCONCLUSIVE --summary "Needs human review"
```

Verification records command, exit code, bounded output, metrics, and Git snapshot. Evidence can become stale after repository changes. A review is evidence, not automatic truth.

## Acceptance

Acceptance is separate from claim verification. A claim must have current evidence, must not be contradicted or stale, and must not be blocked by an open high or critical finding.

```bash
arifce acceptance create CLAIM-0001 --actor "product-owner" --rationale "Acceptance criteria and current evidence reviewed"
arifce acceptance status ACCEPTANCE-0001
arifce acceptance revoke ACCEPTANCE-0001
```

Acceptance records preserve the actor, rationale, Git snapshot, and evidence IDs. They can be revoked without rewriting the original claim or evidence.

## Compatibility checks

```bash
arifce architecture check CLAIM-0001 --forbid "Forbidden.Namespace" --path src
arifce api baseline src/ArifCE.Core/bin/Release/net10.0/ArifCE.Core.dll --baseline api-baseline.json
arifce api compare src/ArifCE.Core/bin/Release/net10.0/ArifCE.Core.dll --baseline api-baseline.json --claim CLAIM-0001
arifce schema baseline .arifce/index/arifce.db --baseline schema-baseline.json
arifce schema compare .arifce/index/arifce.db --baseline schema-baseline.json --claim CLAIM-0001
```

These checks inspect only explicitly selected inputs and do not claim application-level compatibility beyond their evidence scope.

## Refactor campaigns

```bash
arifce refactor start "Rename resolver" "Remove legacy resolver" --invariant "Preserve behavior" --inventory "LegacyResolver.cs" --forbid "Legacy.Namespace"
arifce refactor status REF-0001
arifce refactor checkpoint REF-0001 "Before rename"
arifce refactor resolve REF-0001 "LegacyResolver.cs"
arifce refactor verify REF-0001
arifce refactor finish REF-0001
```

Blocking inventory and forbidden-reference guards prevent `finish`. Safe points record Git state; they do not perform rollback.

## Diagnostics and repair

```bash
arifce doctor
arifce doctor --repair
arifce rebuild
```

`doctor` is read-only by default. Repair backs up the journal, removes corrupt lines, and rebuilds the derived index.

## MCP and dashboard

The optional MCP server exposes local `status`, `search`, `checkpoint`, `handoff`, and safe refactor inspection tools over stdio. The dashboard exposes status, search, and non-sensitive record summaries over loopback. See [MCP setup](getting-started/mcp.md) and [IDE manifest](../integrations/ide/arifce.local.json).

## Documentation rule

Every change to a command, record schema, API endpoint, MCP tool, package version, or user-visible behavior must update this guide and the closest reference page in the same change. Run affected examples or explain the exception in the release checklist. Never document a deferred capability as shipped.
