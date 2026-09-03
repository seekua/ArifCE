# Deterministic code-graph evaluator calibration

## Verdict and scope

TASK-0023 addresses the sixth evaluator objective in FINDING-0005. The old pinned test checked selected node/edge existence and one project-reference confidence value. It did not establish heuristic confidence, cache invalidation/rebuild equivalence or canonical preservation.

Four replacement tests pass against the existing implementation. No product-code change, dependency addition or canonical migration was necessary. The documentation's obsolete dependency-free scanner description was corrected: the existing scanner uses Roslyn syntax trees, not compiler-bound semantic resolution.

## Pinned public contract

Fixture source and helpers are pinned at `6af42086596a470d32d0d4844b5f3cf0378d5f8d`. Independent evaluation transforms only the namespace/class names and selects the four registered fully qualified methods. Both arms receive the same explicit acceptance contract.

- Declarations, same-line overload identities, constructor/test classification, valid edge endpoints, structural containment, heuristic reference/test/call candidates, exact project references, qualified selection and negative/blank queries.
- Source addition, editing, deletion, rename and project-reference removal invalidate queries. Internal metadata and excluded obj/node_modules noise do not alter semantics or source digest.
- Deleting, corrupting or making the graph cache legacy reconstructs the same ordered graph/query semantics, excluding generation timestamps. Rebuilding does not change seeded canonical CURRENT/decision bytes.
- Qualified method closure includes its declaration file, not heuristic caller/test files. Qualified project closure follows reverse transitive exact dependents, excludes unrelated projects and updates when a reference disappears.

## Good/bad controls

Run `./scripts/test-engineering-benchmark-graph-calibration.ps1 -SourceCommit <commit>`. It exports an isolated tree and requires these executed-test outcomes:

| Control | Required result |
| --- | --- |
| Unmodified graph implementation | PASSED |
| Promote heuristic relationships to EXACT | FAILED |
| Reuse stale cached graph after source changes | FAILED |
| Drop CALLS relationships | FAILED |
| Collapse distinct overload identities | FAILED |
| Follow project dependencies in the wrong direction | FAILED |
| Rewrite canonical context during derived graph build | FAILED |

All four tests must execute exactly once. Compiler, restore and runner errors produce ERROR and do not count as caught mutations. Failed isolated copies retain logs; successful copies are removed with bounded path validation.

## Verification status

All 101 local product tests pass (zero skipped). The first compile of the new fixture was rejected by xUnit2031 for two `Assert.Single(Where(...))` expressions; using the predicate overload fixed the test code without weakening assertions or analyzer policy. Windows calibration at `6af42086596a470d32d0d4844b5f3cf0378d5f8d` passed good code and rejected all six wrong variants through executed assertions. Manifest, registry, assessment and secret-scan checks pass. Independent completion integration passed with the actual pinned fixture, including token/timing/log tamper rejection. No false product defect is inferred from a test-authoring error.

## Remote closure

Commit `b47f17aa66f2925cf1474ad4bdda9fae975fa009` passed [CI run 33743344157](https://github.com/seekua/ArifCE/actions/runs/33743344157). All eight jobs succeeded: Windows/macOS/Ubuntu build-test-package and five self-contained binaries. Each OS test log reports 101 passed, zero failed and zero skipped. Ubuntu passed six pinned completion integrations, all previous calibration steps, and the graph good/six-mutant controls through executed assertions. TASK-0023 closes Phase 73 against this evidence; the broader FINDING-0005 remains open.

## Limits

This is deterministic syntax-fixture coverage, not a real model task, compiler-bound receiver/overload resolution, full C#/MSBuild interpretation or a performance benchmark. The fixture uses slash-separated project paths; conditional imports, external references, path escapes/symlinks, concurrent rebuilds and crash recovery are not exhausted. Symbol identity stability across arbitrary edits and complete language coverage are not established. Heuristic same-name candidates can be ambiguous and are not promoted to trust.

Canonical preservation covers seeded CURRENT/decision documents and deletion of the graph cache, not every memory entity or every SQLite table. Source edits during capture and valid-JSON caches with forged contents are not simulated. No speed, token savings or measured engineering benefit is claimed.

Six of ten evaluator objectives now have strengthened assertions and finite good/bad calibration. Change contracts, flight recorder, MCP validation and unfinished-verification policy remain, followed by a fresh permission-matched repeated model study with captured usage/host timing. FINDING-0005 remains open and productClaimEligible remains false.
