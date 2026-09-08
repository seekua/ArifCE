# Product evidence log

ArifCE does not claim token savings, fewer regressions, or better review quality without repeatable evidence. This file is the repository-owned place for those measurements.

## A/B protocol

For each task, keep the repository snapshot, model, prompt, and token budget constant. Run one baseline workflow without ArifCE retrieval and one workflow with ArifCE context. Record:

| Field | Baseline | ArifCE | Notes |
| --- | ---: | ---: | --- |
| Task identifier |  |  | Same task in both arms |
| Agent clarification questions |  |  | Count |
| Rejected approach repeated |  |  | Count |
| Total tokens |  |  | Provider-reported when available |
| Rework passes |  |  | Re-entry into an already changed file |
| Verification outcome |  |  | Deterministic result |
| Elapsed time |  |  | Wall-clock duration |

## Interpretation rules

- Do not aggregate fewer than 20 matched trial pairs into a product claim. The current protocol achieves this with ten categories repeated in two fresh sessions; it must not call those repetitions 20 unique tasks.
- Publish raw run metadata and failures, not only favorable averages.
- A result is evidence about the tested workflow and repository snapshot; it is not a guarantee for every project or model. Both arms must carry the same recorded, non-secret permission profile, and product-study collection rejects unavailable provider/agent-host token telemetry or host-process timing.
- If ArifCE does not improve a measure, keep that result and use it to narrow the product scope.

The repository includes `scripts/run-ab-benchmark.ps1`, a strict normalizer that accepts two caller-produced raw JSON arms and refuses to write a report unless both contain the same 20 task identifiers. It does not fabricate scores or claim effectiveness.

The newer engineering suite completed its first ten matched task pairs on 2026-09-02. The [published result](evidence/engineering-benchmark-results-2026-09-02.md) is explicitly inconclusive: a write-permission confound determined the only differing pass, token telemetry was unavailable, and the run count remains below the threshold for an aggregate product claim.
