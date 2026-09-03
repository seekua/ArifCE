# Benchmark protocol

ArifCE does not claim context savings or quality improvements without repeatable evidence. A benchmark run should compare a baseline agent workflow with ArifCE-assisted retrieval on the same tasks, repository snapshot, model, and token budget.

Record task correctness, irrelevant source reads, retrieval latency, estimated context tokens, verification outcomes, and failure modes. Publish raw run metadata and scripts before reporting aggregate percentages. Results are advisory evidence, not a correctness guarantee.

The repository's engineering suite is defined in `benchmarks/engineering-tasks.json` and pinned to commit `be05904`. It contains ten task classes: bug fix, feature, refactor, regression, API change, canonical-data migration, unfinished continuation, handoff, old-decision revisit, and known failed approach. Both arms must use a fresh isolated checkout, the same model/version, and the same token budget. Later solution commits must not be visible to the agents.

`scripts/new-engineering-benchmark-trial.ps1` enforces the checkout boundary. It exports the pinned tree rather than creating a worktree, initializes a new repository with exactly one commit and no remotes, verifies the exported tree hash, writes immutable preparation metadata, and refuses replacement. `scripts/test-engineering-benchmark-trial.ps1` smoke-tests matched snapshot identity and isolation on every supported CI operating system. The harness prepares trials; it does not execute an agent, infer tokens, or invent outcomes.

`scripts/complete-engineering-benchmark-trial.ps1` records a committed candidate, runs the fixed repository test evaluator, and hashes every raw provenance artifact. `-VerifyOnly` detects later changes to the manifest, prompt, agent log, patch, evaluator output, final commit, or final tree. Its output intentionally reports deterministic check status rather than task success; a green existing test suite alone does not prove that the requested behavior was implemented.

`benchmarks/evaluators.json` provides the one-to-one independent scoring registry. Every task pins the full source commit and regression-test method that introduced its proof. The evaluator source is withheld from the candidate arm and is injected only after completion; merely adding a candidate-authored test with a matching name cannot affect scoring.

`scripts/run-engineering-task-evaluator.ps1` materializes the pinned methods in a separate project after provenance completion and derives `taskPassed` from the independent test process exit code. The result records hashes for the registry, injected source, and evaluator output. The candidate repository retains no remote and never receives access to the trusted source repository.

Suite preparation and collection are separate. Preparation creates all twenty isolated directories but never invokes a model. Collection rejects partial or unmatched suites and verifies provenance plus independent-evaluator artifacts again before aggregating results. Execution status is recorded in `docs/evidence/engineering-benchmark-readiness.md`; the first complete run is reported in `docs/evidence/engineering-benchmark-results-2026-09-02.md`.

`scripts/validate-engineering-benchmark.ps1` is a legacy shape/coverage validator for imported rows, not host-usage provenance verification. Its imported totals must not be presented as captured token measurements. Use the completion and collection pipeline above for log-bound measurements. Negative results remain in the published report.

## Captured token usage

For a captured `codex exec --json` stdout log, complete a committed trial with:

```powershell
./scripts/complete-engineering-benchmark-trial.ps1 -TrialRoot <trial-directory> -RawLog <captured-jsonl-file> -UsageFormat codex-exec-jsonl
```

The parser follows the [documented host event format](https://learn.chatgpt.com/docs/non-interactive-mode): one `thread.started`, one `turn.started`, and one `turn.completed` with integer `input_tokens`, `cached_input_tokens`, and `output_tokens`. Total usage is input plus output; cached input is a subset, not an additional charge. This is a token count, not a monetary cost calculation.

Completion hashes the raw log and stores its parsed counters in `tokenMeasurement`. Verification and collection reparse the log and reject counters that disagree. Manual `-TokensConsumed` values alone are not accepted; with a supported log, an explicitly supplied value must match. This checks consistency, not cryptographic host authenticity: someone controlling all artifacts can forge a log. Do not publish raw logs without a separate secret/privacy review.

Absent, failed, unsupported, or multi-turn telemetry must be preserved with the default `-UsageFormat none` and `-TokenSource unavailable`, not estimated. New records use null tokens; legacy unavailable zero sentinels remain readable. Any unavailable trial makes that arm's aggregate token total null. `tokenComparisonAvailable` means complete counters exist in both arms, not that a causal effectiveness comparison is valid.

This bounded parser deliberately rejects concatenated threads, repeated completion events, and host errors. Multi-turn aggregation and other hosts require documented counter semantics and fixtures before support. Active execution time is still not measured: `durationMs` includes preparation, queue, approval, and evaluation time. Equal permissions and repeated matched real executions are still required. Parser tests use synthetic events and do not constitute an A/B measurement.

## Host-process elapsed time

`scripts/invoke-engineering-benchmark-host.ps1` wraps an explicitly chosen executable. It supplies the prepared prompt on standard input, starts in the isolated checkout, captures stdout as `agent.log` and stderr as `host.stderr.log`, and writes `host-timing.json` after exit. Pass arguments as an array, not a shell command string:

```powershell
./scripts/invoke-engineering-benchmark-host.ps1 -TrialRoot <trial-directory> -Executable <host-executable> -HostArguments @('<argument-1>', '<argument-2>') -TimeoutSeconds 1800
# Complete using <trial-directory>/agent.log; select UsageFormat only if that log matches it.
```

Choose host arguments that read the prompt from stdin. The runner does not select a model, enforce its token budget, approve tool requests, or provide an OS sandbox. Apply equal, externally enforced permissions to both arms before running. Do not use it to bypass approval controls. Command arguments and environment values are not copied into the timing record, but host logs may still contain secrets and must remain private until reviewed.

The monotonic stopwatch covers process launch through observed process exit. Preparation, queueing before launch, and the later independent evaluator are excluded. Startup, model-internal queueing, network waits, and any in-process approval waits are included. Therefore `timeMeasurement.hostElapsedMs` is **not active model time**; `activeWorkMs` remains null. `durationMs` retains its legacy preparation-through-repository-test meaning. Do not compare unlike clocks.

Completion and collection verify the timing record against the session, prompt, stdout, stderr, and parsed result. Missing capture remains unavailable. The collector reports a host-time total only when every trial in the arm has a measurement; active work is never inferred. A captured nonzero exit is retained as `hostExitCode`, not silently discarded or treated as task success.

The persistent `host-capture.started` reservation prevents concurrent/repeated capture from overwriting a trial. Timeout, launch failure, changed instructions, or incomplete output leave an interrupted capture: preserve its artifacts and record the failure before preparing a separately identified retry. An interrupted capture cannot be completed as if its timing were absent. This does not yet score interrupted runs automatically, so do not silently omit them from a published experiment.

Hash consistency is not operator attestation. A party controlling all artifacts could replace them. Time capture tests use local fixture processes, not real model work. Approval-wait segmentation, active-work timing, host identity attestation, and a permission-controlled real rerun remain open.
