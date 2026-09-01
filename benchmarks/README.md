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

The pinned fixture commit must exist in the local Git object database. A shallow clone must fetch that exact commit before preparing a trial; the preparer never fetches implicitly or accepts a different snapshot.

After the agent commits its candidate and the agent host writes a raw activity log, complete and verify the trial:

```text
./scripts/complete-engineering-benchmark-trial.ps1 -TrialRoot artifacts/engineering-benchmark/trust-dirty-content/baseline -RawLog ./agent.log -TokensConsumed 12000 -TokenSource provider
./scripts/complete-engineering-benchmark-trial.ps1 -TrialRoot artifacts/engineering-benchmark/trust-dirty-content/baseline -VerifyOnly
```

Completion runs a fixed `dotnet test` evaluator and binds the preparation manifest, prompt, raw log, candidate patch, final commit/tree, and evaluator output with SHA-256 hashes. It refuses dirty, unchanged, or previously completed trials. The result deliberately contains no user-authored task-success field: passing repository tests is evidence, but task correctness still requires the independent evaluator introduced by the next phase.

`evaluators.json` pins each task to the full commit, trusted test source, fixture type, and regression-test method that first proved the requested behavior. Candidate-authored tests or a method with the same name are never scoring evidence. The Phase 51 runner must extract and hash the trusted evaluator only after the candidate run has ended.

`run-engineering-task-evaluator.ps1` performs that post-run injection. It first verifies the completed provenance bundle, extracts only the pinned `[Fact]` methods from the trusted Git object, builds a separate test project referencing the candidate projects, records the injected source/output/registry hashes, and derives `taskPassed` solely from its exit code. It refuses a second evaluation. The trusted source repository must contain the pinned commits; it is never exposed as a remote to the candidate checkout.

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
