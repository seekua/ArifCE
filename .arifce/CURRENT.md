# Current State

## Objective

Deliver V0.2 deterministic verification adapters while preserving the published V0.1 baseline.

## Status

V0.1 remains published as GitHub Release v0.1.0. TASK-0004 is open for V0.2: deterministic architecture-boundary, public API surface, and SQLite schema compatibility evidence adapters. ADR-0005 records the owner-authorized scope. Phases 15 and 16 are complete with local and cross-platform CI evidence; Phase 17 is next.

## Blockers

No V0.1 release blocker remains. V0.2 must not claim adapter coverage until each adapter has deterministic tests, package-fixture coverage, and observed cross-platform CI evidence.

## Next steps

Implement Phase 17 normalized SQLite schema compatibility evidence with deterministic database baselines and diffs.
