# First Refactor Campaign

Start a measurable campaign with invariants, inventory, and a forbidden-reference guard:

```bash
arifce refactor start "Replace legacy cache" "Move callers to VersionedCache" --invariant "Preserve tenant isolation" --inventory "LegacyCache.cs" --forbid "LegacyCache"
```

Use the returned refactor ID:

```bash
arifce refactor workstream REF-0001 domain --owner codex --path "src/Domain/**" --path "tests/Domain.Tests/**"
arifce refactor safepoint REF-0001 before-domain --note "Known rollback position"
arifce refactor checkpoint REF-0001 "Domain callers migrated"
arifce refactor resolve REF-0001 "LegacyCache.cs"
arifce refactor verify REF-0001
arifce refactor finish REF-0001
```

`finish` fails while inventory remains or a blocking forbidden reference is present. Safe points capture Git state but do not execute rollback. Workstreams record ownership/path scopes; V0.1 does not create worktrees or invoke agents.

If the campaign should not continue:

```bash
arifce refactor abandon REF-0001
```

Preserve the failed approach separately when it could save future work:

```bash
arifce attempt record TASK-0001 "Global cache flush" --result rejected --reason "Breaks tenant isolation guarantees"
```
