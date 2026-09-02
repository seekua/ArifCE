# Change Impact Contracts

A Change Impact Contract is a repository-owned pre-change brief. It combines a target code symbol with deterministic graph candidates, related tests, historical project records, explicit invariants, risk, and required verification.

Create and inspect a contract:

```text
arifce codegraph build
arifce contract create Calculate --risk HIGH --invariant "Financial rounding remains unchanged"
arifce contract status CONTRACT-0001
```

Creating a contract also creates a normal ArifCE claim. The contract does not duplicate evidence, review, freshness, or acceptance rules. Use the linked claim with the existing `verify`, `review record`, `trust refresh`, and `acceptance create` commands.

Potential impact and related-test entries preserve the confidence supplied by the code graph. A heuristic relationship is a review candidate, not proof that code is affected. Historical records are lexical matches from canonical decisions, failed attempts, findings, refactors, and claims; they remain links to inspect rather than automatically accepted constraints.

Contract creation verifies the disposable graph's source digest first and automatically rebuilds it after any `.cs` or `.csproj` edit, addition, deletion, or rename. A legacy or malformed derived graph is also rebuilt. This prevents contracts from consuming known-stale graph snapshots; it does not turn heuristic relationships into verified dependencies.

Risk controls required verification:

- `LOW`: inspect impact candidates.
- `MEDIUM`: successful build and test evidence.
- `HIGH`: build, tests, and an agreeing independent review.
- `CRITICAL`: the high-risk requirements plus explicit human acceptance and rationale.

The contract records the repository snapshot at creation. It is canonical; the code graph it references remains disposable derived data.
