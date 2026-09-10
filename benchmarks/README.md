# Engineering benchmark suite

`engineering-tasks.json` defines ten matched engineering tasks pinned to ArifCE commit `be05904`, before the trust-remediation implementation. The suite covers bug fixing, feature work, refactoring, regression prevention, API change, canonical-data migration, unfinished-task continuation, handoff recovery, old-decision review, and a known failed approach. A product-effectiveness study runs every task twice in fresh sessions: this produces 20 matched **trial pairs**, not 20 falsely described unique tasks.

Run each task twice from a fresh isolated checkout of the fixture commit:

1. `baseline`: the agent receives the repository and task instruction without ArifCE-generated context.
2. `arifce`: the same model and token budget receive the repository plus the normal ArifCE protocol, retrieval, and handoff flow.

Do not expose later commits, previous-arm output, or another agent's workspace to either arm. Record one JSON object per task with these fields:

Prepare a history-free trial instead of using a worktree from the current repository. On Windows, use an output root outside the source repository (the default is the system temporary directory):

```text
./scripts/new-engineering-benchmark-trial.ps1 -TaskId trust-dirty-content -Arm baseline -Trial 1 -Model model-and-version -TokenBudget 50000 -PermissionProfile preauthorized-write-build-v1 -OutputRoot "$env:TEMP/arifce-study"
./scripts/new-engineering-benchmark-trial.ps1 -TaskId trust-dirty-content -Arm arifce -Trial 1 -Model model-and-version -TokenBudget 50000 -PermissionProfile preauthorized-write-build-v1 -OutputRoot "$env:TEMP/arifce-study"
```

The preparer exports only the fixture tree, replaces the product repository's ArifCE-requiring `AGENTS.md` with identical neutral participant instructions in both arms, adds the same public compile contract and compact check runner, creates a new one-commit repository with no remotes, and refuses to overwrite an existing trial. The session preserves both the source tree and the neutralized fixture tree. Each arm receives that same neutralized snapshot and a separate prompt. The ArifCE arm must use product context, search, task/claim, evidence, and handoff workflow; the baseline arm is explicitly prohibited from reading `.arifce` or using ArifCE retrieval. No arm receives hidden acceptance information.

The pinned fixture commit must exist in the local Git object database. A shallow clone must fetch that exact commit before preparing a trial; the preparer never fetches implicitly or accepts a different snapshot.

After the agent commits its candidate and the agent host writes a raw activity log, complete and verify the trial:

```text
./scripts/complete-engineering-benchmark-trial.ps1 -TrialRoot "$env:TEMP/arifce-study/trust-dirty-content/baseline/trial-01" -RawLog ./agent.log -UsageFormat codex-exec-jsonl
./scripts/complete-engineering-benchmark-trial.ps1 -TrialRoot "$env:TEMP/arifce-study/trust-dirty-content/baseline/trial-01" -VerifyOnly
./scripts/run-engineering-task-evaluator.ps1 -TrialRoot "$env:TEMP/arifce-study/trust-dirty-content/baseline/trial-01"
```

Completion runs a fixed, single-worker `dotnet test --no-restore` evaluator with reusable build servers disabled, then binds the preparation manifest, prompt, raw log, candidate patch, final commit/tree, and evaluator output with SHA-256 hashes. It refuses dirty, implicitly unchanged, or previously completed trials. The evaluator measures the checkout the agent actually left behind: it cannot download packages or repair missing restore state after the run. The bounded build topology prevents concurrent benchmark arms from multiplying persistent MSBuild workers. The result deliberately contains no user-authored task-success field: passing repository tests is evidence, but task correctness still requires the independent evaluator introduced by the next phase.

If an agent produces no candidate, preserve the negative run with `-AllowNoCandidate`. This explicit path still requires a clean checkout and raw log, records `candidateChanged: false`, and remains subject to independent evaluation. It must never be used to turn an absent solution into success.

`evaluators.json` pins each task to the full commit, trusted test source, fixture type, and regression-test method that first proved the requested behavior. Candidate-authored tests or a method with the same name are never scoring evidence. The Phase 51 runner must extract and hash the trusted evaluator only after the candidate run has ended.

`run-engineering-task-evaluator.ps1` performs that post-run injection. It first compiles the candidate against the public hash-bound API probe. Only a passing gate reaches the hidden behavioral evaluator. It then extracts only the pinned `[Fact]` methods from the trusted Git object, builds a separate test project referencing the candidate projects, records the gate/source/project/output/registry hashes, and derives `taskPassed` from executed TRX evidence. It refuses a second evaluation.

`new-engineering-benchmark-suite.ps1` prepares all forty isolated trial directories without invoking an agent: ten task categories × two fresh trials × two arms. Every pair must use the same model, token budget, isolated fixture, and non-secret permission profile. Product-study collection rejects a pair with a different permission profile, missing host-process timing, or unavailable token telemetry. After every candidate has been completed and independently evaluated, `collect-engineering-benchmark-suite.ps1` emits a report only if all 20 matched pairs are complete, matched, telemetry-complete, and hash-consistent. Partial runs are not aggregated.

Create the immutable run order after preparation. The seed is stored only as a SHA-256 digest. Pair order is reproducible, matched arms remain adjacent, and which arm runs first is exactly balanced across the 20 pairs:

```powershell
./scripts/new-engineering-benchmark-execution-plan.ps1 -SuiteRoot artifacts/engineering-benchmark -Seed '<private random study seed>'
```

The plan binds every prepared session and prompt by SHA-256 and stores only suite-relative paths. It refuses replacement and does not invoke a model. Execute entries strictly by `sequence`; do not choose a favorable order after observing outcomes.

`tokenBudget` is currently a predeclared non-cached-input-plus-output target, not a hard task-success criterion. Completion derives `tokenBudgetCompliant` from captured host usage and rejects later tampering. Collection preserves over-target and failed runs separately; successful-task token comparisons include only candidates that pass repository tests, the public API gate, the independent evaluator, regression checks, and harness policy.

Create a single-pair report with `compare-engineering-benchmark-pair.ps1`. It reports primary/cache-included tokens, churn, tool rounds, read/search/edit/build estimates, ArifCE overhead, Useful Context Ratio, Context Amplification Factor, duration, failed-run tokens, and optional account-level Plus snapshots. Estimated fields remain labeled as estimates.

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
