# Change Impact Contracts

A Change Impact Contract is a repository-owned pre-change brief. It combines a target code symbol with deterministic graph candidates, related tests, historical project records, explicit invariants, risk, and required verification.

Create and inspect a contract:

```text
arifce codegraph build
arifce contract create Calculate --risk HIGH --invariant "Financial rounding remains unchanged"
arifce verify CLAIM-0002 --command "dotnet test" --contract CONTRACT-0001
arifce contract status CONTRACT-0001
```

Creating a contract also creates a normal ArifCE claim. The contract does not duplicate evidence, review, freshness, or acceptance rules. Use the linked claim with the existing `verify`, `review record`, `trust refresh`, and `acceptance create` commands.

Potential impact and related-test entries use the strongest direct graph relationship connecting each candidate to the selected target: `EXACT` project references, `STRUCTURAL` declaration or containment edges, or `HEURISTIC` references, test candidates, and calls. A file's existence or a method's parsed declaration does not upgrade a heuristic link. These labels describe the relationship; even a structural link does not prove that a runtime behavior will change. Historical records are lexical matches from canonical decisions, failed attempts, findings, refactors, and claims; they remain links to inspect rather than automatically accepted constraints.

Contracts created before the Phase 65 correction may contain overly strong candidate labels. Canonical historical snapshots are not rewritten automatically; recreate the contract for corrected impact labels. This change does not alter prior evidence or acceptance, whose scope is calculated separately.

Contract creation verifies the disposable graph's source digest and generator version first and automatically rebuilds it after any `.cs` or `.csproj` edit, addition, deletion, rename, or scanner upgrade. A legacy or malformed derived graph is also rebuilt. This prevents contracts from consuming known-stale graph snapshots; it does not turn heuristic relationships into verified dependencies.

Contract-linked verification is explicit rather than automatic. It requires the contract's own linked claim, stores the contract ID in the evidence scope, and hashes the target's trusted closure. Declaration-to-file `STRUCTURAL` edges and reverse transitive `EXACT` project references participate. Heuristic references/tests do not. The current C# closure remains file-level, so any edit in a scoped declaration file requires re-verification; symbol-level content hashing remains deferred.

Risk controls required verification:

- `LOW`: inspect impact candidates.
- `MEDIUM`: successful build and test evidence.
- `HIGH`: build, tests, and an agreeing independent review.
- `CRITICAL`: the high-risk requirements plus explicit human acceptance and rationale.

The contract records the repository snapshot at creation. It is canonical; the code graph it references remains disposable derived data.
