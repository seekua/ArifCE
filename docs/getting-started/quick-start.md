# Quick Start

Run these commands from a Git repository after installing the `arifce` tool:

```bash
arifce init
arifce task create "Add tenant-aware permission caching"
arifce checkpoint --summary "Task created; implementation not started"
arifce context "implement tenant-aware permission caching" --budget 4000
arifce status
```

`init` creates portable canonical state under `.arifce/`, concise Codex/Claude/OpenCode adapters, and a disposable SQLite index. Running it again is safe and preserves existing content.

After doing real work, record a claim and deterministic evidence:

```bash
arifce claim create "Permission cache tests pass"
arifce verify CLAIM-0001 --command "dotnet test"
arifce handoff
```

A successful command does not make every claim true. Normal medium-risk claims become `SUPPORTED`; evidence remains scoped to its Git snapshot and can become stale.

If the index is deleted or damaged:

```bash
arifce rebuild
arifce doctor
```

Canonical project intelligence remains in the human-readable files.
