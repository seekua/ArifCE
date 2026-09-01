# Benchmark protocol

ArifCE does not claim context savings or quality improvements without repeatable evidence. A benchmark run should compare a baseline agent workflow with ArifCE-assisted retrieval on the same tasks, repository snapshot, model, and token budget.

Record task correctness, irrelevant source reads, retrieval latency, estimated context tokens, verification outcomes, and failure modes. Publish raw run metadata and scripts before reporting aggregate percentages. Results are advisory evidence, not a correctness guarantee.

The repository's engineering suite is defined in `benchmarks/engineering-tasks.json` and pinned to commit `be05904`. It contains ten task classes: bug fix, feature, refactor, regression, API change, canonical-data migration, unfinished continuation, handoff, old-decision revisit, and known failed approach. Both arms must use a fresh isolated checkout, the same model/version, and the same token budget. Later solution commits must not be visible to the agents.

`scripts/new-engineering-benchmark-trial.ps1` enforces the checkout boundary. It exports the pinned tree rather than creating a worktree, initializes a new repository with exactly one commit and no remotes, verifies the exported tree hash, writes immutable preparation metadata, and refuses replacement. `scripts/test-engineering-benchmark-trial.ps1` smoke-tests matched snapshot identity and isolation on every supported CI operating system. The harness prepares trials; it does not execute an agent, infer tokens, or invent outcomes.

`scripts/validate-engineering-benchmark.ps1` validates manifest coverage and matched raw arm results. A normalized report is evidence only after every task has a real recorded execution. Empty values, invented measurements, unmatched tasks, and silently removed failures are invalid. Negative results remain in the published report.
