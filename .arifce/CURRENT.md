# Current State

## Objective

Strengthen V0.9 engineering trust with deterministic, low-noise dependency invalidation, canonical CLI mutation integrity, explicit graph-target selection, and fail-closed repository snapshots before broader semantic code-intelligence work.

## Status

Phase 72 is in progress under TASK-0022 / FINDING-0008. Source f6c3500 fixes stale acceptance-basis masking, foreign evidence acceptance and disappearing NeedsReview warnings. All 97 local product tests pass; calibration, pinned independent integration and remote closure are pending. See docs/evidence/benchmark-propagation-calibration-2026-09-03.md. No product-effectiveness claim is eligible.

Phase 71 and the Phase 70 storage follow-up are closed at c7dbdef by CI 33737427375: 92 tests on each of three OS targets, five binaries, pinned independent integrations, good/six-mutant freshness controls and two-positive/five-negative storage controls. TASK-0021 and CHECKPOINT-0027 record closure. FINDING-0006 (false-current/path handling) and FINDING-0007 (stale ID reservation overwrites) are resolved. ATTEMPT-0013 and two earlier failing CI runs preserve the 29/30 loss evidence. FINDING-0005 remains OPEN; productClaimEligible stays false.

## Blockers

The next comparative benchmark is gated on FINDING-0005 evaluator remediation and calibration, not on CI success. Heuristic caller/test relationships remain excluded from automatic stale propagation. Compiler-bound precision and previously deferred integrations remain outside this phase.

## Next steps

Next strengthen stale propagation without legacy generic-command prerequisites, covering positive/current and stale claim/acceptance/handoff behavior plus good/bad calibration. Six of ten evaluator objectives remain (stale propagation, graph, contracts, flight recorder, MCP, unfinished verification); four are calibrated with explicit limits. See docs/evidence/benchmark-freshness-calibration-2026-09-03.md. Only afterwards run fresh, permission-matched repeated model trials with captured usage/host timing. Active work remains unavailable. Preserve failures, historical results and private reviews; no product-effectiveness result is established.
