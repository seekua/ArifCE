# Engineering benchmark execution readiness

Status: **runner ready; real matched execution not started**.

The repository can prepare twenty history-free trials, bind completed candidates to raw provenance, inject task-specific trusted evaluators after each run, and collect only a complete matched result set. It cannot manufacture an agent run or infer missing provider usage.

On 2026-09-01 the execution environment contained no ArifCE LLM provider profile and none of the supported provider API-key environment variables were configured. A Codex desktop executable was present but could not be invoked as a headless CLI from the benchmark process. Therefore no baseline or ArifCE task result was recorded, and no effectiveness claim was made.

Once one fixed agent/model/version is callable for all trials:

```text
./scripts/new-engineering-benchmark-suite.ps1 -Model exact-model-and-version -TokenBudget 50000
# Run each generated prompt in its isolated checkout, capture the raw host log, commit the candidate,
# then run complete-engineering-benchmark-trial.ps1 and run-engineering-task-evaluator.ps1.
./scripts/collect-engineering-benchmark-suite.ps1
```

The collector refuses missing arms, mismatched models or budgets, duplicate run IDs, altered provenance, evaluator-registry drift, changed evaluator artifacts, and hand-authored evaluator outcomes.
