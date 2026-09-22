# Clean core lifecycle matched-pair result

This is a preserved negative benchmark result, not an effectiveness or token-savings claim. The baseline arm passed every predeclared eligibility gate. The ArifCE arm passed the repository suite, public API gate, independent evaluator, requirements check, and recorded workflow check, but it violated the context-efficiency retry policy. The pair is therefore ineligible and was not retried.

## Raw pair table

| Metric | Baseline | ArifCE |
| --- | ---: | ---: |
| Independent evaluator | PASSED | PASSED |
| Repository tests | PASSED | PASSED |
| Public API gate | PASSED | PASSED |
| ArifCE workflow | N/A | PASSED |
| Arm eligible | YES | NO |
| Cache-excluded input + output | 80,983 | 126,307 |
| Context Amplification Factor | 30.485 | 92.344 |
| Useful Context Ratio | 0.032803 | 0.010829 |
| Non-cached input | 69,196 | 108,356 |
| Output | 11,787 | 17,951 |
| Cache-included total | 1,443,415 | 3,388,515 |
| Churn ratio | 17.823679 | 26.827611 |
| Model/tool rounds | 25 | 60 |
| File-read token estimate | 17,332 | 14,407 |
| Repeated file-context estimate | 0 | 0 |
| Search token estimate | 29,629 | 22,093 |
| Edit/retry token estimate | 6,274 | 12,661 |
| Build/test token estimate | 5,604 | 682 |
| ArifCE visible overhead estimate | 0 | 6,551 |
| Captured host elapsed | 489,673 ms | 902,616 ms |

No successful-task token delta, prevented-cost estimate, or product-effectiveness conclusion is reported because both arms must be eligible before a comparison is valid. Codex Plus five-hour usage moved from 5% to 8% across the pair; weekly usage was 59% at the post-run snapshot. These are account-level integer snapshots and cannot be attributed solely to either arm. The free reset credit was not used.

## Fixed identity

- Product HEAD at preparation: `d3137f88f5c52dbc668f444198247527d6d16ade`
- Fixture: `d9fee6d137d7355b24c287f6f976b44400d80b38`
- Task: `task-bounded-context`
- Model/reasoning: `gpt-5.6-terra` / `medium`
- Token budget: 50,000 cache-excluded input plus output, diagnostic rather than a pass criterion
- Permission profile: `preauthorized-write-build-v1`
- Evaluator registry SHA-256: `7b4425580a6b96fae3edb94274b9a605188fe75c86bc0cc36f8f13dfe01846d9`
- Baseline preceded ArifCE, and both isolated arms were prepared before either result was inspected.

## Exact eligibility failure

The ArifCE participant's first attempt to stage and commit its candidate failed because Git could not create the checkout index lock. It then issued the same `git add` and `git commit` action again without an intervening re-plan message. The retry succeeded, but the policy deliberately rejects an exact repeated failed command without a re-plan. This prevents a transient host condition from being silently converted into an apparently efficient agent action.

This failure does not invalidate the external evaluator pass or hide a product defect: it makes the measured run ineligible. The raw artifacts are retained locally for audit, while this evidence deliberately excludes local paths, prompts, host logs, and private runtime data.

## Prospective harness correction

The rejection policy is unchanged. The neutral participant instructions now require the participant to inspect every failed tool action and, before repeating a materially similar action, write an explicit re-plan naming a changed hypothesis or diagnostic/wait action. They explicitly apply the rule to transient lock errors. Regression coverage asserts that every isolated arm receives this instruction; the existing policy regression continues to reject an exact repeated failed command without a re-plan.

A future matched pair must use newly prepared arms and remain subject to all current evaluator, workflow, provenance, and context-efficiency gates. Lower-model trials remain blocked until one fully eligible Terra/medium pair exists.
