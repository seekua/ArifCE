# Benchmark execution order and budget gate — 2026-09-09

## Outcome

TASK-0029 is complete. The repeated external study can now be bound before execution to a single immutable plan. Pair order is SHA-256 seeded, both arms of each matched pair remain adjacent, and first-arm assignment is balanced across the 20 pairs. The plaintext seed is not persisted.

Measured Codex JSONL usage is also compared with the predeclared total-token ceiling. Over-budget runs are preserved with their independent evaluator outcome, but cannot count as protocol passes. Provenance verification rejects a modified compliance value.

## Evidence

- Local `dotnet test ArifCE.slnx --configuration Release --no-restore`: 113 passed, 0 failed.
- Local balanced execution-plan smoke: passed.
- Local completion provenance and token-ceiling smoke: passed.
- Local suite preparation/rejection smoke: passed.
- Local secret scan: passed.
- GitHub Actions [run 34295202034](https://github.com/seekua/ArifCE/actions/runs/34295202034): passed on commit `69d5a5a`.
- The remote run covered three operating-system build/test jobs, all benchmark gates and evaluator controls on Linux, and five self-contained package targets.

## Boundary

This gate does not execute a model and does not prove that ArifCE improves correctness, time, token use, or handoff recovery. The next evidence requires all 40 externally metered agent runs with one model/version, reasoning level, token ceiling, and permission profile, followed by publication of favorable, null, negative, failed, and over-budget outcomes. FINDING-0005 remains open and `productClaimEligible` remains false.
