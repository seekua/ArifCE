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

- Do not aggregate fewer than 20 matched tasks into a product claim.
- Publish raw run metadata and failures, not only favorable averages.
- A result is evidence about the tested workflow and repository snapshot; it is not a guarantee for every project or model.
- If ArifCE does not improve a measure, keep that result and use it to narrow the product scope.

No experiment has been completed yet. This is intentional: the project currently records the protocol before claiming an outcome.
