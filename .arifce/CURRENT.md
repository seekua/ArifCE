# Current State

## Objective

Strengthen V0.9 engineering trust with deterministic, low-noise dependency invalidation, canonical CLI mutation integrity, explicit graph-target selection, and fail-closed repository snapshots before broader semantic code-intelligence work.

## Status

Phase 71 under TASK-0021 is pending remote closure. Freshness fix/tests pinned at d040501 pass 92 local tests, independent integration and good/six-mutant calibration (FINDING-0006). Reviewing later Phase 70 closure CI 33735216710 exposed real lost ID data: 29/30 task records. FINDING-0007 and ATTEMPT-0013 record the stale scan/reservation race; target recheck and forced-stale-scan controls are being validated. Do not dismiss that failure because the earlier storage CI passed. FINDING-0005/0006/0007 remain OPEN; productClaimEligible stays false.

## Blockers

The next comparative benchmark is gated on FINDING-0005 evaluator remediation and calibration, not on CI success. Heuristic caller/test relationships remain excluded from automatic stale propagation. Compiler-bound precision and previously deferred integrations remain outside this phase.

## Next steps

Validate the ID race fix with the full regression suite, forced-stale-scan success/rejected-no-recheck controls, then rerun CI and close FINDING-0006/0007 only with evidence. Afterwards strengthen stale propagation and other evaluator objectives. See docs/evidence/benchmark-freshness-calibration-2026-09-03.md. Only after calibration, establish equal permissions and rerun fresh matched sessions with captured usage/host timing. Active work remains unavailable. Preserve errors, historical results and private reviews; no product-effectiveness result is established.
