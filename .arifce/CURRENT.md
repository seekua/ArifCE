# Current State

## Objective

Strengthen V0.9 engineering trust with deterministic, low-noise dependency invalidation, canonical CLI mutation integrity, explicit graph-target selection, and fail-closed repository snapshots before broader semantic code-intelligence work.

## Status

Phases 75–78 are complete. Phase 79 harness remediation is active under TASK-0030. All ten evaluator objectives have pinned independent tests and finite bad-control calibration. Public machine-readable API probes, a compile-before-behavior gate, Windows temp-root isolation, bounded build/test output, churn telemetry, and a mandatory ArifCE treatment workflow pass remote CI on three operating systems. The success-only treatment and failure-signature retry fixes passed all eight CI jobs in run 34889340578. A fourth Terra/medium graph pair was infrastructure-clean but both candidates failed three explicit behavior tests. The predeclared `llm-secret-boundary` calibration attempt then exposed a Windows Codex fs-helper false positive in both arms before either could produce a candidate. The harness now pins the Windows restricted-token sandbox independently of user configuration, classifies reparse/sandbox stderr as infrastructure failure, and has passed a real structured-edit smoke run plus all 113 product tests. FINDING-0005 remains OPEN and productClaimEligible stays false until an evaluator-passing matched model pair succeeds.

## Blockers

The first three Terra/medium graph pairs remain ineligible because they exposed, in order, an underspecified public probe, a network-confounded repository check, and an incomplete ArifCE treatment. The fourth removed those confounders and demonstrated a repeatable model implementation failure, so the graph task will not be repeated. The first `llm-secret-boundary` calibration attempt is also ineligible: both arms independently stopped on the same host edit failure and consumed 35,458 and 63,434 primary tokens without producing candidates. The deterministic host fix and smoke proof still require remote CI before a clean-window rerun. Heuristic caller/test relationships remain excluded from automatic stale propagation. Compiler-bound precision and previously deferred integrations remain outside this phase.

## Next steps

Preserve all four ineligible graph pairs and the failed edit-blocked calibration attempt. Run the explicit Windows host-profile fix in remote CI. After CI and the next clean usage-window reset, rerun the predeclared Terra/medium `llm-secret-boundary` pair from identical temp-root checkouts through `invoke-codex-engineering-benchmark.ps1`. Compare successful-task tokens only if both arms pass repository tests, the public API gate, the independent evaluator, regression checks, host infrastructure checks, and harness policy. Do not resume the 40-run study or lower-model comparison until that pair is valid. Product effectiveness remains unproven.
