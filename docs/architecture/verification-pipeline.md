# Verification Pipeline

ArifCE treats an agent completion statement as a claim, then evaluates deterministic evidence before considering semantic review.

```text
Claim
  -> deterministic build, test, Git, or search evidence
  -> evidence freshness evaluation
  -> risk policy
       low/medium: deterministic verdict when sufficient
       high:       independent semantic review also required
       critical:   independent review and human approval required
```

## Blind review contract

`BlindReviewRequest` has two explicit phases. `INDEPENDENT_INSPECTION` contains the task, acceptance criteria, repository snapshot, relevant diff, evidence identifiers, and project constraints. Its validation rejects a builder claim so the reviewer is not anchored by the builder's explanation.

`RECONCILIATION` adds the builder claim after independent inspection. A reviewer may return `AGREE`, `PARTIALLY_AGREE`, `DISAGREE`, or `INCONCLUSIVE` plus finding identifiers. Agreement is review evidence, not deterministic truth.

`ISemanticReviewAdapter` is the vendor boundary. V0.1 intentionally provides no adapter that invokes an external coding agent: authentication, cost, capability discovery, process isolation, and context policy require explicit integration work. Filesystem and CLI behavior do not depend on an adapter.

## Risk policy

The typed `VerificationPolicy` maps `LOW`, `MEDIUM`, `HIGH`, and `CRITICAL` to required build, test, independent-review, and human-approval flags. A critical claim cannot become verified from model agreement alone. Evidence remains scoped to its Git snapshot and can become stale.
