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

These changes are prospective. Neither of the first two completed pairs has been modified or reclassified. A new matched pair must pass repository tests, public API compilation, independent behavior evaluation, regression checks, and harness policy in both arms before successful-task token costs are compared. Lower-model trials and the larger study remain blocked until then.

## Third diagnostic pair and treatment validation

A third sequential Terra/medium pair started from identical isolated commit `ffa6e00310f8341d2a7f64a87fa599db374a1838`, with source commit `be059044fecff123edcca8d5b1e4cadca1880049` and harness commit `7a9d8bdf6d1e3e161b18711023796b87934ba696`. Both arms used the same task, public contract, evaluator, host version, reasoning level, permission profile, and baseline-first execution order. Neither result is eligible for successful-task comparison.

| Metric | Baseline | ArifCE |
| --- | ---: | ---: |
| Repository tests | PASS | PASS |
| Public API gate | PASS | PASS |
| Independent evaluator | FAIL (2/4) | FAIL (2/4) |
| Non-cached input + output | 54,804 | 128,271 |
| Cache-included total | 756,244 | 2,813,967 |
| Churn ratio | 13.799066 | 21.937671 |
| Context Amplification Factor | 42.527 | 82.318 |
| Useful Context Ratio | 0.023514 | 0.012148 |
| Model/tool rounds | 18 | 46 |
| Duration | 395,962 ms | 918,278 ms |
| ArifCE workflow | N/A | FAIL |

Both candidates failed `Graph_preserves_declarations_and_relationship_confidence` because the required overload nodes were absent, and `Graph_trusted_closure_excludes_heuristics_and_follows_project_dependents` because the exact project target was missing. This repeatable same-test failure is candidate behavior, not an evaluator compilation defect. The run remains failed-run cost evidence only.

The ArifCE arm attempted status/context/search before a disposable index existed; index access failed and task, claim, verification evidence, and handoff were not completed. The earlier validator searched raw log text and could therefore count failed command text as workflow completion. Treatment prompts now require a platform-neutral `net10.0` CLI invocation, an explicit `rebuild`, and one host action per workflow operation. Completion now counts only successful command events and requires rebuild, context, search, task, claim, verify, and handoff. Positive and negative provenance fixtures cover this distinction.

The run also showed that repeating the same bounded build command after an edit can reveal a different compiler error. Such progress was incorrectly classified as a blind retry. Retry-loop detection now groups by both the complete normalized command and the meaningful failure signature. Regression fixtures distinguish identical failures, progressive compiler failures, and different commands sharing a long Windows wrapper prefix.

Local verification after these prospective fixes passes the context-efficiency, trial-isolation, and completion-provenance controls plus all 113 product tests. Remote CI and a clean-window rerun remain required; no historical result is reclassified and no product-effectiveness claim is made.

## Fourth diagnostic pair: clean infrastructure, failed candidates

Commit `73bef7ae91d9656d550070a21de8926e8dcf40b7` passed all three operating-system jobs and all five self-contained package jobs in [CI run 34889340578](https://github.com/seekua/ArifCE/actions/runs/34889340578). A fourth sequential Terra/medium pair then started at one percent five-hour usage from the same isolated commit, public contract, evaluator, host version and permission profile. Both candidate checkouts were clean and committed before evaluation. Both repository suites and public API gates passed, both harness policies passed, and the ArifCE workflow passed. The independent evaluator failed both candidates, so the pair is ineligible and the token values are failed-run costs only.

| Metric | Baseline | ArifCE |
| --- | ---: | ---: |
| Repository tests | PASS | PASS |
| Public API gate | PASS | PASS |
| Independent evaluator | FAIL (1 passed / 3 failed) | FAIL (1 passed / 3 failed) |
| Harness policy | PASS | PASS |
| ArifCE workflow | N/A | PASS |
| Non-cached input + output | 59,480 | 92,558 |
| Cache-included total | 767,832 | 1,812,110 |
| Churn ratio | 12.909079 | 19.578102 |
| Context Amplification Factor | 35.206 | 73.380 |
| Useful Context Ratio | 0.028404 | 0.013628 |
| Model/tool rounds | 17 | 37 |
| Duration | 329,791 ms | 585,996 ms |
| Visible ArifCE overhead | 0 | 12,230 |

Both candidates failed the explicitly disclosed requirement to retain distinct same-line overload identities, refresh a one-line added method through exact search, and resolve the qualified method target used by trusted closure. Their own added tests did not cover the full public behavior contract. The evaluator compiled and executed normally; no hidden API requirement, repository failure, permission variance, network dependency or treatment-classification defect explains the outcome. The result is therefore a real, repeatable model failure and does not justify weakening the evaluator or changing the harness.

The five-hour account snapshot moved from 1% to 36% and the weekly snapshot from 25% to 30%; these integer account-level deltas are not attributable solely to the trials. The free reset credit was not used. Raw local trial artifacts remain outside the public repository because they include machine-specific paths and agent activity.

Repeating the graph task again would spend tokens without testing a new harness hypothesis. Before observing a new outcome, the next pipeline-calibration task is fixed as `llm-secret-boundary`: its pinned evaluator exercises a successful clean request, pre-provider secret rejection, persisted response redaction and seven calibrated bad controls, and both arms passed its earlier evaluator revision. This selection calibrates successful-run telemetry only and is excluded from product-effectiveness claims. The larger study remains governed by its precommitted balanced execution plan; lower-model comparisons remain blocked until both calibration arms pass.
