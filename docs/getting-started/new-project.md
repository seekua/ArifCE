# New Project

Create or enter a Git repository, then initialize ArifCE:

```bash
git init
arifce init
```

The command discovers the repository root and creates `.arifce/PROJECT.md`, `CURRENT.md`, `PROTOCOL.md`, canonical entity directories, the JSONL journal, memory documents, configuration, the derived FTS index, `.gitignore`, and small agent adapters. Existing files are not overwritten.

Next, describe the project facts in `.arifce/PROJECT.md` and current objective in `.arifce/CURRENT.md`. Keep `CURRENT.md` bounded and state-oriented rather than chronological.

Create the first work record:

```bash
arifce task create "Establish the first vertical feature"
arifce decision create "Select persistence format" --decision "Use canonical JSON records" --rationale "Required for deterministic V0.1 serialization"
arifce checkpoint --summary "Project initialized and first task defined"
```

Commit canonical `.arifce/` data when it is appropriate for the repository. Index, cache, and raw directories are ignored by default.
