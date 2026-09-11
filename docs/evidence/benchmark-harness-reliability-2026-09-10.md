# Benchmark harness reliability gate — 2026-09-10

## Outcome

The harness now separates public compilation requirements from hidden behavioral assertions. Every engineering task has a public C# API probe bound into both trial prompts and sessions by SHA-256. A cheap compile-only gate runs before the independent evaluator. The deterministic-code-graph probe explicitly exercises every record constructor and public member used by its pinned evaluator, including record `with` updates and cancellation-token overloads; a legacy underspecified graph API is a required negative control.

Windows trials are rejected when their output root is nested under the source repository. Both arms receive the same structured-edit rule and compact build/test wrapper. Large source payloads embedded in shell commands, exact unchanged full-read/search repeats, direct unbounded build/test output, and repeated failures without replanning are recorded as harness-policy violations.

The ArifCE arm must exercise context, canonical search, task/claim, evidence, and handoff operations. Reading only `PROTOCOL.md` or `CURRENT.md` is not a valid treatment. The baseline receives the identical task, public contract, fixture, check wrapper, model, reasoning setting, token target, and permission profile, without ArifCE memory access.

## Metrics

Captured Codex JSONL now records:

- non-cached input and output as the primary task-cost metric;
- cache-included total and churn ratio;
- visible file-read, repeated-file, search, edit/retry, build/test, and ArifCE workflow estimates;
- model/tool rounds;
- approximate Useful Context Ratio and Context Amplification Factor;
- failed-run tokens separately from successful-task comparisons.

The 50,000 primary-token value remains diagnostic until evaluator-passing matched runs establish whether it is realistic. It does not currently turn a correct run into a failed task.

## Local verification

- All ten public API probes compile against the current reference implementation.
- The deterministic graph probe rejects the previously accepted API shape that omitted node/edge names, endpoints, and confidence.
- API contract tampering is rejected.
- Context-efficiency fixtures detect repeated reads/searches, shell-embedded source, compact checks, and replanning signals.
- Trial isolation, suite rejection, token parsing, and completion provenance smoke tests pass.

Remote CI passed on all three operating systems and all five self-contained package targets in [run 34462957826](https://github.com/seekua/ArifCE/actions/runs/34462957826). A subsequent matched Terra/medium pilot produced two repository-test and API-gate passes but two independent-evaluator compilation errors because the then-bound graph probe omitted evaluator-required record members. That pair is retained locally as an invalid failed run, not product evidence. The public probe and negative control were corrected only after both arms ended; a fresh pair is therefore still required and `productClaimEligible` remains false.

The corrected graph probe then passed the three-OS matrix in [run 34466811725](https://github.com/seekua/ArifCE/actions/runs/34466811725). A second matched Terra/medium pair used that corrected contract and produced legitimate behavioral results rather than evaluator infrastructure errors:

| Metric | Baseline | ArifCE |
| --- | ---: | ---: |
| Public API gate | PASS | PASS |
| Repository tests | FAIL (NuGet audit network error) | PASS |
| Independent evaluator | FAIL (2 passed, 2 failed) | FAIL (1 passed, 3 failed) |
| Non-cached input + output | 45,565 | 78,466 |
| Cache-included total | 543,997 | 1,276,290 |
| Context Amplification Factor | 49.691 | 44.844 |
| Useful Context Ratio | 0.020124 | 0.022300 |
| Model/tool rounds | 16 | 26 |
| Duration | 317,928 ms | 553,620 ms |

Both implementations failed the explicitly documented same-line overload identity and trusted-closure behavior. The ArifCE implementation also failed lifecycle parsing for an edited one-line method. These are model implementation failures and are not hidden-contract defects. The baseline repository-test failure was a separate `NU1900` network-dependent vulnerability-audit error, so this pair still cannot enter successful-task token comparison.

Post-pair review found a second harness false positive: retry-loop grouping compared only the first 120 normalized command characters. Long Windows PowerShell wrapper prefixes therefore grouped different inner commands and unrelated failures. Retry grouping now compares the complete normalized command, with positive and negative regression fixtures. Benchmark build/test, API-gate, and independent-evaluator entry points also force `NuGetAudit=false`; vulnerability auditing remains a CI/product concern but is not permitted to make an otherwise unchanged isolated benchmark depend on external advisory availability.

These changes are prospective. Neither completed pair has been modified or reclassified. A new matched pair must pass repository tests, public API compilation, independent behavior evaluation, regression checks, and harness policy in both arms before successful-task token costs are compared. Lower-model trials and the larger study remain blocked until then.
