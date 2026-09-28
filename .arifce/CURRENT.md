# Current State

## Objective

Strengthen V0.9 engineering trust with deterministic, low-noise dependency invalidation, canonical CLI mutation integrity, explicit graph-target selection, and fail-closed repository snapshots before broader semantic code-intelligence work.

## Status

Phases 75–78 are complete. Phase 79 external measurement remains active under TASK-0030. All ten evaluator objectives have pinned independent tests and finite bad-control calibration. The corrected `task-bounded-context` Terra/medium pair is the first fully eligible matched comparison: both arms passed repository tests, the public API gate, the independent evaluator, regression checks, and harness policy. ArifCE consumed 154,458 cache-excluded input-plus-output tokens versus 90,935 for baseline, so this single task establishes no token-saving or broader product-effectiveness claim. The subsequent Terra/low pair was ineligible because the baseline missed a pinned behavior and the ArifCE checkout remained dirty. The subsequent Luna/medium pair was ineligible because the ArifCE candidate missed the pinned foreign-link exclusion behavior. No failed pair was silently retried. The complete localization and packaging state at `fbc84a7` passed all eight remote CI jobs in run 36384049304; the first Windows attempt exposed one intermittent child-process test failure, and the disclosed same-SHA failed-job rerun passed all 120 tests and remaining gates. FINDING-0005 remains OPEN and `productClaimEligible` remains false.

## Blockers

The eligible Terra/medium result is negative on token efficiency and cannot support an ArifCE advantage claim. The two lower-model outcomes are ineligible and cannot support a model-quality comparison. One eligible task is insufficient to generalize product effectiveness, and the larger balanced study remains incomplete. Heuristic caller/test relationships remain excluded from automatic stale propagation. Compiler-bound precision and previously deferred integrations remain outside this phase. Metered model work must not start in a materially used five-hour window or consume a free reset credit.

## Next steps

Preserve every eligible, negative, failed, and ineligible pair without favorable-result filtering. Do not repeat the completed `task-bounded-context` pairs merely to seek a better outcome. Before the next metered pair, require a fresh clean or near-clean five-hour usage window, identical isolated checkouts, a predeclared task/model/reasoning combination, and unchanged completion gates. Compare successful-task tokens only when both arms pass repository tests, the public API gate, the independent evaluator, regression checks, host infrastructure checks, and harness policy. Keep the 50,000-token target diagnostic until more successful pairs establish a realistic threshold. Product effectiveness remains unproven.
