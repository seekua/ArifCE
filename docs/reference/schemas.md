# Schema Reference

Canonical machine records use schema version `1`, camel-case JSON properties, UTC ISO 8601 timestamps, string IDs, and `UPPER_SNAKE_CASE` enum values. The reader remains compatible with early PascalCase enum records created while ArifCE was dogfooding its schema.

| Prefix | Entity directory |
|---|---|
| `TASK` | `tasks/` |
| `ADR` | `decisions/` |
| `ATTEMPT` | `attempts/` |
| `CHECKPOINT` | `checkpoints/` |
| `CLAIM` | `claims/` |
| `EVIDENCE` | `evidence/` |
| `FINDING` | `findings/` |
| `REVIEW` | `reviews/` |
| `REF` | `refactors/` |
| `HANDOFF` | `handoffs/` |

Records reference related entities by ID. Evidence contains command metadata, bounded summary, Git snapshot, and optional structured metrics. Refactors embed invariants, remaining inventory, guards, workstreams, and safe points. Schema-incompatible future data must require an explicit migration rather than silent reinterpretation.
