# Current State

## Objective

Strengthen V0.9 engineering trust with deterministic, low-noise dependency invalidation, canonical CLI mutation integrity, explicit graph-target selection, and fail-closed repository snapshots before broader semantic code-intelligence work.

## Status

Phases 75–77 are complete. All ten evaluator objectives have pinned independent tests and finite bad-control calibration. FINDING-0009 through FINDING-0012 are resolved, and TASK-0025 through TASK-0027 are completed. GitHub Actions run 34215866974 passed the three-OS quality matrix, all Linux calibration controls, and five self-contained package smoke checks for commit dadddc3. See docs/evidence/benchmark-verification-calibration-2026-09-08.md. FINDING-0005 remains OPEN; productClaimEligible stays false because evaluator calibration does not establish model effectiveness.

## Blockers

The next comparative benchmark requires fresh, permission-matched model sessions with provider token telemetry and host active-time capture. It cannot use the earlier permission-confounded run as evidence. Heuristic caller/test relationships remain excluded from automatic stale propagation. Compiler-bound precision and previously deferred integrations remain outside this phase.

## Next steps

Phase 78 is complete. Ten categories × two fresh sessions produces 20 matched trial pairs, with a matched permission profile and required provider/agent-host token plus host-time telemetry. GitHub Actions run 34285263692 passed the three-OS matrix, five self-contained packages, the repeatability gate, provenance and all ten evaluator calibrations for commit b201762. Next run the externally metered trials and publish favorable, null, and negative results together. Product effectiveness remains unproven. See docs/evidence/benchmark-repeatability-gate-2026-09-09.md.
