# Engineering benchmark suite

`engineering-tasks.json` defines ten matched engineering tasks pinned to ArifCE commit `be05904`, before the trust-remediation implementation. The suite covers bug fixing, feature work, refactoring, regression prevention, API change, canonical-data migration, unfinished-task continuation, handoff recovery, old-decision review, and a known failed approach.

Run each task twice from a fresh isolated checkout of the fixture commit:

1. `baseline`: the agent receives the repository and task instruction without ArifCE-generated context.
2. `arifce`: the same model and token budget receive the repository plus the normal ArifCE protocol, retrieval, and handoff flow.

Do not expose later commits, previous-arm output, or another agent's workspace to either arm. Record one JSON object per task with these fields:

Prepare a history-free trial instead of using a worktree from the current repository:

```text
./scripts/new-engineering-benchmark-trial.ps1 -TaskId trust-dirty-content -Arm baseline -Model model-and-version -TokenBudget 50000
./scripts/new-engineering-benchmark-trial.ps1 -TaskId trust-dirty-content -Arm arifce -Model model-and-version -TokenBudget 50000
```

The preparer exports only the fixture tree, creates a new one-commit repository with no remotes, and refuses to overwrite an existing trial. Each arm receives the same snapshot and a separate prompt. The ArifCE arm is permitted to use only the canonical memory already present in that snapshot; the baseline arm is explicitly prohibited from using ArifCE retrieval. This prevents later solution commits and other-arm output from leaking through shared Git history.

```json
{
  "taskId": "trust-dirty-content",
  "arm": "baseline",
  "fixtureCommit": "be05904",
  "model": "model-and-version",
  "tokenBudget": 50000,
  "success": false,
  "durationMs": 0,
  "tokensConsumed": 0,
  "filesRead": 0,
  "contextReconstructionMs": 0,
  "repeatedInvestigations": 0,
  "repeatedFailedApproaches": 0,
  "incorrectAssumptions": 0,
  "regressions": 0,
  "handoffRecoveryMs": 0,
  "verificationFailures": 0,
  "notes": "Factual run notes, including failures."
}
```

Validate and normalize completed arms:

```text
./scripts/validate-engineering-benchmark.ps1 -Baseline baseline.json -Arifce arifce.json -Output docs/evidence/engineering-ab-run.json
```

The validator rejects missing tasks, mismatched commits, models or budgets, negative metrics, duplicate IDs, and incomplete result fields. It never executes an agent or invents a missing measurement.
