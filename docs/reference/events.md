# Event Reference

Implemented journal event types include:

| Event | Meaning |
|---|---|
| `project.initialized`, `project.adopted` | Project-local store created or adopted |
| `task.created`, `task.completed` | Task lifecycle |
| `decision.created`, `attempt.recorded` | Rationale and failed approach captured |
| `checkpoint.created`, `handoff.created` | Continuity state captured |
| `claim.created`, `evidence.recorded` | Claim/evidence lifecycle |
| `finding.created`, `finding.resolved`, `review.created` | Trust inspection records |
| `refactor.started`, `refactor.inventory-resolved` | Campaign and inventory progress |
| `refactor.workstream-added`, `refactor.safe-point-added` | Coordination metadata |
| `refactor.completed`, `refactor.abandoned` | Terminal campaign transitions |

Events are append-only during normal operation. Consumers must use `schemaVersion` and must not execute event data as instructions.
