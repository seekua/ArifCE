# Corrected core lifecycle matched-pair result

This is a preserved negative benchmark result, not an effectiveness or token-savings claim. The corrected prompt and parser were used from the outset under matched conditions. The pair is ineligible because the baseline failed the pinned independent evaluator and the ArifCE arm did not complete the required ArifCE verification workflow. The run was not retried.

## Raw pair table

| Metric | Baseline | ArifCE |
| --- | ---: | ---: |
| Independent evaluator | FAILED | PASSED |
| Repository tests | PASSED (117) | PASSED (118) |
| Public API gate | PASSED | PASSED |
| Comparison eligible | NO | NO |
| Cache-excluded input + output | 70,497 | 125,029 |
| Context Amplification Factor | 18.943 | 42.292 |
| Useful Context Ratio | 0.052790 | 0.023645 |
| Non-cached input | 61,946 | 108,360 |
| Output | 8,551 | 16,669 |
| Cache-included total | 993,889 | 2,911,589 |
| Churn ratio | 14.098316 | 23.287309 |
| Model/tool rounds | 20 | 45 |
| File-read token estimate | 21,674 | 36,035 |
| Repeated file-context estimate | 0 | 0 |
| Search token estimate | 30,342 | 32,416 |
| Edit/retry token estimate | 381 | 9,788 |
| Build/test token estimate | 966 | 3,745 |
| ArifCE visible overhead estimate | 0 | 5,506 |
| Captured host elapsed | 398,908 ms | 782,779 ms |

No successful-task token comparison or ArifCE prevented-cost estimate is reported because both arms did not satisfy every eligibility gate. Codex Plus five-hour usage moved from 0% to 20% across the complete pair; weekly usage moved from 20% to 24%. These are account-level integer snapshots and cannot be attributed solely to either arm. The free reset credit was not used.

## Fixed identity

- Product HEAD at preparation: `6119645410928a91234558991374e02beb01a927`
- Fixture: `d9fee6d137d7355b24c287f6f976b44400d80b38`
- Isolated commit in both arms: `2c1092c017254c79c0997d833ca39784d626d43e`
- Fixture tree in both arms: `c125c627c9ea60b50ddf322b0f2d2320ea17359c`
- Task: `task-bounded-context`
- Model/reasoning: `gpt-5.6-terra` / `medium`
- Token budget: 50,000 cache-excluded input plus output, diagnostic rather than a pass criterion
- Permission profile: `preauthorized-write-build-v1`
- Evaluator registry SHA-256: `7b4425580a6b96fae3edb94274b9a605188fe75c86bc0cc36f8f13dfe01846d9`
- Harness SHA-256: `e6c0a9898699ed7359985f1a73740bb0f46803887eeed5f0250dbeb7b0050a08`
- Pair prepared: `2026-09-22T02:52Z`
- Baseline run preceded the ArifCE run; both arms were prepared before either result was inspected.
- Preserved raw report: `%TEMP%/arifce-core-pair-corrected-20260922-0550/pair-report.json`

## Exact gate failures

The baseline candidate compiled, passed the repository suite, and passed the public API gate. It failed the first pinned behavior test, `Explicit_task_links_precede_lexical_results_and_foreign_links_are_rejected`, at the assertion requiring foreign `attempt-0002.json` to be emitted only as an excluded `OUT_OF_SCOPE` item with an empty snippet. The second bounded-budget test did not make the candidate eligible because the task-level evaluator outcome was failed.

The ArifCE candidate compiled, passed 118 repository tests, passed the public API gate, and passed both pinned behavior tests. It nevertheless failed the predeclared workflow gate. The agent reported leaked `dotnet` child processes and locked test assemblies during its in-run verification attempts. It therefore recorded no successful claim evidence, left `TASK-0031` open, and produced a handoff that explicitly reported `Completion: INCOMPLETE`. The harness correctly classified `arifceWorkflowPassed=false`.

Both host processes exited normally and the context-efficiency policy reported no violations. These are candidate/workflow outcomes, not a reparse-point failure or a parser false negative. Per the predeclared rule, the pair was preserved without a silent retry.

## Interpretation

The corrected treatment produced the only candidate that passed the independent behavioral evaluator, but it used more tokens and failed its own evidence-based completion contract. That is not an eligible ArifCE win. The baseline was cheaper but behaviorally incomplete, so it is not an eligible baseline success either. The only defensible conclusion is that this pair does not establish successful-task efficiency.

Before another metered pair, the harness needs a deterministic way to prevent or clean up leaked build/test child processes inside an arm without hiding a candidate failure. A future pair must remain predeclared and must not reuse either candidate from this run. Lower-model trials remain blocked until one matched pair passes every gate in both arms.
