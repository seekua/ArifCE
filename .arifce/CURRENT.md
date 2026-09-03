# Current State

## Objective

Strengthen V0.9 engineering trust with deterministic, low-noise dependency invalidation, canonical CLI mutation integrity, explicit graph-target selection, and fail-closed repository snapshots before broader semantic code-intelligence work.

## Status

Phase 56 through 69 are closed. Phase 70 is in progress under TASK-0020: storage evaluator source pinned to 2850fd5, with three real child processes and index deletion/rebuild plus canonical-byte checks. All 88 test records pass locally (includes the worker-host entrypoint); good/four-mutant calibration and independent integration pass. Remote closure is pending. Production storage code is unchanged. FINDING-0005 stays OPEN; productClaimEligible remains false.

## Blockers

The next comparative benchmark is gated on FINDING-0005 evaluator remediation and calibration, not on CI success. Heuristic caller/test relationships remain excluded from automatic stale propagation. Compiler-bound precision and previously deferred integrations remain outside this phase.

## Next steps

Finish Phase 70 calibration/integration and CI proof; then strengthen repository freshness and the other remaining evaluator objectives before a new study. See docs/evidence/benchmark-storage-calibration-2026-09-03.md for exact coverage and limits. Only after evaluator calibration, establish equal permissions and rerun fresh matched sessions with captured usage/host timing. Active work remains unavailable because internal waits are not separated. Preserve evaluator/interruption errors, historical results and private reviews; no product-effectiveness result has been established.
