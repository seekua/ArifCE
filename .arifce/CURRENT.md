# Current State

## Objective

Strengthen V0.9 engineering trust with deterministic, low-noise dependency invalidation, canonical CLI mutation integrity, explicit graph-target selection, and fail-closed repository snapshots before broader semantic code-intelligence work.

## Status

Phase 56 through 70 are closed. Phase 71 under TASK-0021 found FINDING-0006: nested untracked edits retained the same snapshot digest; Windows quoted Unicode/deleted paths also failed. Fix and four real-Git tests are pinned at d040501. All 92 local tests, independent integration and good/six-mutant calibration pass. Remote closure is pending. FINDING-0005 and FINDING-0006 remain OPEN until their respective proof gates; productClaimEligible remains false.

## Blockers

The next comparative benchmark is gated on FINDING-0005 evaluator remediation and calibration, not on CI success. Heuristic caller/test relationships remain excluded from automatic stale propagation. Compiler-bound precision and previously deferred integrations remain outside this phase.

## Next steps

Close Phase 71 with calibration, independent integration and CI evidence before resolving FINDING-0006. Then strengthen stale propagation and the other remaining evaluator objectives. See docs/evidence/benchmark-freshness-calibration-2026-09-03.md for bounded coverage and compatibility implications. Only after calibration, establish equal permissions and rerun fresh matched sessions with captured usage/host timing. Active work remains unavailable because internal waits are not separated. Preserve evaluator errors, historical results and private reviews; no product-effectiveness result has been established.
