# Incremental index plan

SQLite is a disposable derived index. The current `rebuild` command intentionally recreates it from canonical files and remains the recovery path.

## Acceptance criteria

- Detect added, changed, and deleted canonical files using a persisted path/hash manifest.
- Update only affected FTS rows while preserving the same search schema and ranking contract.
- Keep a full rebuild command that can recreate the index from zero.
- Fall back to a full rebuild when the manifest is missing, corrupt, or incompatible.
- Prove that incremental and clean rebuild results are equivalent on the same fixture.
- Never treat SQLite as the source of truth and never delete canonical records during maintenance.

The optimization is not enabled yet; current behavior favors deterministic recovery over premature complexity.
