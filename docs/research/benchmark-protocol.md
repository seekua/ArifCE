# Benchmark protocol

ArifCE does not claim context savings or quality improvements without repeatable evidence. A benchmark run should compare a baseline agent workflow with ArifCE-assisted retrieval on the same tasks, repository snapshot, model, and token budget.

Record task correctness, irrelevant source reads, retrieval latency, estimated context tokens, verification outcomes, and failure modes. Publish raw run metadata and scripts before reporting aggregate percentages. Results are advisory evidence, not a correctness guarantee.

The repository's engineering suite is defined in `benchmarks/engineering-tasks.json` and pinned to commit `be05904`. It contains ten task classes: bug fix, feature, refactor, regression, API change, canonical-data migration, unfinished continuation, handoff, old-decision revisit, and known failed approach. Both arms must use a fresh isolated checkout, the same model/version, and the same token budget. Later solution commits must not be visible to the agents.

`scripts/new-engineering-benchmark-trial.ps1` enforces the checkout boundary. It exports the pinned tree rather than creating a worktree, initializes a new repository with exactly one commit and no remotes, verifies the exported tree hash, writes immutable preparation metadata, and refuses replacement. `scripts/test-engineering-benchmark-trial.ps1` smoke-tests matched snapshot identity and isolation on every supported CI operating system. The harness prepares trials; it does not execute an agent, infer tokens, or invent outcomes.

`scripts/complete-engineering-benchmark-trial.ps1` records a committed candidate, runs the fixed repository test evaluator, and hashes every raw provenance artifact. `-VerifyOnly` detects later changes to the manifest, prompt, agent log, patch, evaluator output, final commit, or final tree. Its output intentionally reports deterministic check status rather than task success; a green existing test suite alone does not prove that the requested behavior was implemented.

`benchmarks/evaluators.json` provides the one-to-one independent scoring registry. Every task pins the full source commit and regression-test method that introduced its proof. The evaluator source is withheld from the candidate arm and is injected only after completion; merely adding a candidate-authored test with a matching name cannot affect scoring.

`scripts/run-engineering-task-evaluator.ps1` materializes the pinned methods in a separate project after provenance completion and derives `taskPassed` from the independent test process exit code. The result records hashes for the registry, injected source, and evaluator output. The candidate repository retains no remote and never receives access to the trusted source repository.

`scripts/validate-engineering-benchmark.ps1` validates manifest coverage and matched raw arm results. A normalized report is evidence only after every task has a real recorded execution. Empty values, invented measurements, unmatched tasks, and silently removed failures are invalid. Negative results remain in the published report.
