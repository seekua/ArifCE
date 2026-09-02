# Schema Reference

Canonical machine records use schema version `1`, camel-case JSON properties, UTC ISO 8601 timestamps, string IDs, and `UPPER_SNAKE_CASE` enum values. The reader remains compatible with early PascalCase enum records created while ArifCE was dogfooding its schema.

| Prefix | Entity directory |
|---|---|
| `TASK` | `tasks/` |
| `ADR` | `decisions/` |
| `ATTEMPT` | `attempts/` |
| `CHECKPOINT` | `checkpoints/` |
| `CLAIM` | `claims/` |
| `ACCEPTANCE` | `acceptances/` |
| `EVIDENCE` | `evidence/` |
| `FINDING` | `findings/` |
| `REVIEW` | `reviews/` |
| `REF` | `refactors/` |
| `HANDOFF` | `handoffs/` |

Records reference related entities by ID. Acceptance records preserve the approving actor, rationale, current Git snapshot, and evidence IDs separately from claim status. Evidence contains command metadata, bounded summary, Git snapshot, optional structured metrics, and an optional dependency scope containing normalized repository paths, digest modes, and digests. `CONTENT` hashes file/directory content, while `PUBLIC_API_SURFACE` and `SQLITE_SCHEMA` hash normalized semantic projections. Absence of the optional scope retains the repository-snapshot freshness rule, so existing canonical records remain readable without migration. Refactors embed invariants, remaining inventory, guards, workstreams, and safe points. Schema-incompatible future data must require an explicit migration rather than silent reinterpretation.
