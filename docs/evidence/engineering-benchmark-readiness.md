# Engineering benchmark execution status

Status: **first matched run retained; comparative rerun gated on evaluator remediation**.

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

The first report's API-shape mismatches prompted a review of the exact pinned methods. Public compatibility requirements are now disclosed equally while regression implementations remain withheld. Telemetry and explicit contracts alone do not make the experiment valid; evaluator coverage remains a separate gate.

The [Phase 68 audit](benchmark-contract-audit-2026-09-03.md) now records all ten pinned evaluators' actual coverage. Required contracts are disclosed equally and evaluator errors are separated from assertion failures. However, false-positive-prone secret/acceptance tests, in-process-only concurrency coverage, and other partial assertions still require remediation and good/bad calibration. No comparative model run should be advertised as valid before that work is complete.

[Phase 69](benchmark-safety-calibration-2026-09-03.md) replaces the two secret/acceptance tests and proves a good control plus seven rejected incorrect variants, including actual pinned-source integration in CI. These two audit items are remediated within their documented scope. The remaining eight evaluator gaps, especially cross-process/index recovery, still block an effectiveness study; productClaimEligible remains false.

Current follow-up: Phases 70–73 also strengthen and calibrate [storage/index reconstruction](benchmark-storage-calibration-2026-09-03.md), [repository freshness](benchmark-freshness-calibration-2026-09-03.md), [acceptance/handoff propagation](benchmark-propagation-calibration-2026-09-03.md) and [deterministic graph](benchmark-graph-calibration-2026-09-03.md). Six of ten evaluator objectives are now remediated within the reports' finite limits. Contracts, flight recorder, MCP and unfinished-verification coverage remain open. The earlier counts above are historical phase snapshots, not current completion counts; productClaimEligible remains false and no fresh comparative model result has been added.
