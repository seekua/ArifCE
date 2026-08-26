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
