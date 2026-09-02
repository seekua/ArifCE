# Change-contract relationship confidence evidence

## Finding

`CreateChangeContractAsync` previously copied each related node's confidence into `PotentialImpact` and `RelatedTests`. A caller file found by a heuristic identifier match was labeled `EXACT` because the file itself exists; its method could be labeled `STRUCTURAL` despite being connected only by a heuristic `CALLS` edge. This overstated the relationship in canonical contract records. It did not itself promote a claim to `VERIFIED` or expand trusted evidence closure.

## Correction

Phase 65 projects the strongest direct relationship between a candidate and the selected target into the contract: exact project references retain `EXACT`, lexical declaration/containment links retain `STRUCTURAL`, and candidates connected only by heuristic links retain `HEURISTIC`. No new dependency or canonical schema is introduced.

## Tests

- Before the fix, the persisted-contract regression failed on both the `EXACT` caller file and its `STRUCTURAL` method. The failed approach is recorded in `ATTEMPT-0010`, linked to `TASK-0015`; the finding is `FINDING-0004`.
- The regression now verifies heuristic caller and test candidates, structural declaration-file and containing-type links, and reads the persisted contract rather than only an in-memory projection.
- A separate project-reference test preserves the positive `EXACT` case.
- All 83 local tests pass, including contract-linked freshness boundaries.
- The package smoke fixture creates a contract through the installed CLI and inspects its canonical JSON for heuristic caller confidence.

## Compatibility and limits

Historical contracts remain snapshots with their original labels; they are not silently rewritten. Recreate a contract to obtain corrected labels. Candidate confidence is advisory: acceptance and evidence freshness continue to use their own verification rules and exact/structural closure.

Remote CI evidence is appended after the implementation commit is pushed.
