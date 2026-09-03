# Engineering benchmark execution status

Status: **runner proven; first complete matched run recorded**.

On 2026-09-02 the repository completed ten matched task pairs with history-free trials, hash-bound candidate provenance, and withheld task-specific evaluators. The raw independent pass counts were three baseline and four ArifCE-assisted candidates, but a write-permission confound determined the only differing pass. No effectiveness claim is made.

See the [complete result, negative outcomes, and limitations](engineering-benchmark-results-2026-09-02.md).

The repeatable workflow remains:

```text
./scripts/new-engineering-benchmark-suite.ps1 -Model exact-model-and-version -TokenBudget 50000
# Run each generated prompt in its isolated checkout, capture the raw host log, commit the candidate,
# then run complete-engineering-benchmark-trial.ps1 and run-engineering-task-evaluator.ps1.
./scripts/collect-engineering-benchmark-suite.ps1
```

The collector refuses missing arms, mismatched models or budgets, duplicate run IDs, altered provenance, evaluator-registry drift, changed evaluator artifacts, and hand-authored evaluator outcomes. The next run must also remove approval variance and capture host-reported token and active-time telemetry before aggregate comparison is meaningful.

Phase 66 adds [log-bound token ingestion](benchmark-token-telemetry-2026-09-03.md) for a single completed Codex JSONL turn. The parser, tamper rejection, missing-value aggregation, and completion integration pass local tests. This prepares measurement; it does not add real runs or active-time telemetry. Prior results remain unchanged and inconclusive.

Phase 67 adds a [host-process stopwatch and capture wrapper](benchmark-host-timing-2026-09-03.md), separating process execution from outer preparation/evaluation delay. In-process waits cannot yet be subtracted, so active work remains unavailable. Capture integrity and completion integration pass local tests; no new model executions are included.
