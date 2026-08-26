# Domain Model

All persisted entities have a schema version, strongly typed textual ID, timestamps, lifecycle state where relevant, and provenance.

| Entity | Purpose | Key relationships |
|---|---|---|
| Project | Repository identity and metadata | runs, tasks |
| Agent | Tool/vendor identity without credentials | runs, claims, reviews |
| Run | Bounded work session and Git endpoints | agent, checkpoints |
| Task | Objective, criteria, risk, and work status | claims, attempts |
| MemoryItem | Long-lived knowledge with confidence/lifecycle | provenance |
| Decision | Chosen option and rationale known at decision time | task, supersession |
| Attempt | Tried approach and result | task, evidence |
| Checkpoint | Recoverable current-state snapshot | task, Git snapshot |
| Handoff | Selected current state for a successor | checkpoint, claims |
| Claim | Testable completion statement | task, evidence, reviews |
| Evidence | Deterministic observation scoped to repository state | claim |
| Review | Independent inspection and reconciliation verdict | claim, findings |
| Finding | Actionable observation and severity | review, task |
| RefactorCampaign | Objective, scope, inventory, and completion state | guards, workstreams |
| ContextItem | Retrieval candidate plus scoring metadata | source entity |
| ContextSelection | Budgeted ordered set with inclusion reasons | context items |

Claims move only through the rules in `docs/reference/entity-statuses.md`. Evidence never mutates history; a freshness evaluation states whether it still applies. Reviews do not directly manufacture truth. Refactor finish fails while any blocking guard fails, required inventory remains, or required verification is absent.

Future communication types (`REVIEW_REQUEST`, `CLAIM`, `CHALLENGE`, `FINDING`, `RESPONSE`, `VERDICT`, `HANDOFF`) wrap these domain records rather than creating a separate chat-centric truth model.
