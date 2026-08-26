# Entity Statuses

## Claim status

- `UNVERIFIED`: no sufficient evaluation exists.
- `SUPPORTED`: available evidence increases confidence but does not satisfy every acceptance condition.
- `PARTIALLY_VERIFIED`: some explicit parts are verified and others are unverified or inconclusive.
- `VERIFIED`: all declared verification requirements pass with current evidence for the referenced repository snapshot.
- `DISPUTED`: a review raises a material unresolved challenge.
- `CONTRADICTED`: deterministic evidence demonstrates the statement is false for the referenced snapshot.
- `STALE`: the claim was previously supported or verified, but relevant repository state changed.

`VERIFIED` requires deterministic evidence where deterministic checks exist. Reviewer agreement alone can produce at most `SUPPORTED`. New contradictory evidence moves any non-final positive state to `CONTRADICTED`. Repository change can move `SUPPORTED`, `PARTIALLY_VERIFIED`, or `VERIFIED` to `STALE`. Re-verification can transition from `STALE`, `DISPUTED`, or `CONTRADICTED` based on new evidence; history remains append-only.

## Evidence freshness

- `CURRENT`: recorded commit/digest and relevant hashes match.
- `STALE`: a recorded relevant state is known to differ.
- `UNKNOWN`: comparison cannot be made safely.

## Knowledge confidence and lifecycle

Confidence is `CONFIRMED`, `OBSERVED`, `INFERRED`, or `UNKNOWN`. Lifecycle is `ACTIVE`, `STALE`, `SUPERSEDED`, `DEPRECATED`, or `HISTORICAL`. An inference never becomes confirmed without a new provenance record. Superseded knowledge points to its successor.

## Review and risk

Review reconciliation is `AGREE`, `PARTIALLY_AGREE`, `DISAGREE`, or `INCONCLUSIVE`. Risk is `LOW`, `MEDIUM`, `HIGH`, or `CRITICAL`. Critical completion always requires explicit human approval; ArifCE must not synthesize it.

## Work status

Tasks use `OPEN`, `IN_PROGRESS`, `BLOCKED`, `COMPLETED`, or `ABANDONED`. Refactor campaigns use `PLANNED`, `ACTIVE`, `BLOCKED`, `READY_TO_FINISH`, `COMPLETED`, or `ABANDONED`.
