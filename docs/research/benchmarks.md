# Benchmark Plan

ArifCE publishes no effectiveness result until repeatable fixtures produce evidence.

## Continuity

Compare a fresh agent using only the repository against one using ArifCE context and handoff. Measure task success, unnecessary reads, repeated mistakes, context tokens, and steps to useful work.

## Context efficiency

Compare cold start with budgeted retrieval. Record input size, correctness, relevant/irrelevant files read, and latency at multiple budgets.

## Verification

Seed true, false, stale, and insufficient claims about builds, tests, Git state, and removed references. Measure supported/contradicted/inconclusive outcomes, false positives, and freshness detection.

## Refactoring and overhead

Use a multi-file migration fixture with invariants and forbidden references. Measure remaining inventory, regressions, handoff effectiveness, builder/reviewer cost, defects detected, and completion accuracy.

Every result must name fixture commit, environment, commands, budgets, and failures. Inconclusive runs remain visible.

The V0.7 smoke fixture can be regenerated with `./scripts/run-example-benchmark.ps1`. Its output is stored in `docs/evidence/ab-run-v0.7.json` and contains 20 matched tasks. It records raw command metadata only; it is not an effectiveness, quality, or token-saving claim.

The realistic engineering-task manifest and strict matched-arm validator live under `benchmarks/` and `scripts/validate-engineering-benchmark.ps1`. The [first complete ten-pair run](../evidence/engineering-benchmark-results-2026-09-02.md) retained all outcomes, but its only differing pass was confounded by write-permission variance and provider token telemetry was unavailable. It therefore supports no ArifCE effectiveness claim. The earlier V0.7 retrieval smoke evidence remains separate and must not be presented as this experiment.
