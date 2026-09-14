# Current State

## Objective

Strengthen V0.9 engineering trust with deterministic, low-noise dependency invalidation, canonical CLI mutation integrity, explicit graph-target selection, and fail-closed repository snapshots before broader semantic code-intelligence work.

## Status

Phases 75–78 are complete. Phase 79 harness remediation is active under TASK-0030. All ten evaluator objectives have pinned independent tests and finite bad-control calibration. Public machine-readable API probes, a compile-before-behavior gate, Windows temp-root isolation, bounded build/test output, churn telemetry, and a mandatory ArifCE treatment workflow pass remote CI on three operating systems. The corrected graph contract passed remote CI. A third Terra/medium diagnostic pair passed repository and API gates in both arms, but both candidates failed the same two graph behavior tests and the ArifCE arm did not complete the product workflow. Retry-loop detection now requires the same normalized command and the same meaningful failure, while treatment validation counts only independently observable successful CLI operations and requires a disposable-index rebuild. Local regression coverage and all 113 product tests pass. FINDING-0005 remains OPEN and productClaimEligible stays false until a clean evaluator-passing matched model pair succeeds.

## Blockers

The first clean-window Terra/medium pair is diagnostic only because its independent graph evaluator could not compile against the then-underspecified public probe. A second contract-corrected pair is also ineligible because both candidates failed behavioral assertions and the baseline repository check was independently polluted by a NuGet audit network failure. The third diagnostic pair removed that network confounder: both repository checks and public API gates passed, but both candidates still failed same-line overload identity and trusted-closure behavior. Its ArifCE treatment is additionally invalid because index initialization failed and task/claim/evidence/handoff operations were not completed. The two harness classification weaknesses exposed by this run are fixed prospectively; no historical pair is retrofitted. Heuristic caller/test relationships remain excluded from automatic stale propagation. Compiler-bound precision and previously deferred integrations remain outside this phase.

## Next steps

Run the success-only workflow and failure-signature retry fixes in remote CI, then prepare and run a new Terra/medium baseline-versus-ArifCE pair from identical temp-root checkouts after a clean usage-window reset. Compare successful-task tokens only if both arms pass repository tests, the public API gate, the independent evaluator, regression checks, and harness policy. Preserve all three ineligible pairs and report their failed-run tokens separately. Do not resume the 40-run study or lower-model comparison until a pair is valid. Product effectiveness remains unproven.
