# Serialized core lifecycle matched-pair result

This is a preserved negative benchmark result, not an effectiveness or token-savings claim. Both candidates passed repository tests, the public API gate, and the pinned independent evaluator. The baseline passed every eligibility gate. The ArifCE arm did not complete the required evidence-based workflow, so the pair is ineligible and was not retried.

## Raw pair table

| Metric | Baseline | ArifCE |
| --- | ---: | ---: |
| Independent evaluator | PASSED | PASSED |
| Repository tests | PASSED | PASSED |
| Public API gate | PASSED | PASSED |
| Arm eligible | YES | NO |
| Cache-excluded input + output | 89,177 | 111,955 |
| Context Amplification Factor | 30.338 | 59.891 |
| Useful Context Ratio | 0.032962 | 0.016697 |
| Non-cached input | 75,888 | 95,564 |
| Output | 13,289 | 16,391 |
| Cache-included total | 1,960,025 | 3,194,451 |
| Churn ratio | 21.979042 | 28.533348 |
| Model/tool rounds | 33 | 54 |
| File-read token estimate | 27,922 | 34,160 |
| Repeated file-context estimate | 0 | 0 |
| Search token estimate | 36,246 | 18,904 |
| Edit/retry token estimate | 15,073 | 10,530 |
| Build/test token estimate | 1,438 | 2,105 |
| ArifCE visible overhead estimate | 0 | 5,930 |
| Captured host elapsed | 635,108 ms | 807,219 ms |

No successful-task token delta or prevented-cost estimate is reported because the ArifCE arm failed its workflow gate. Codex Plus five-hour usage moved from 5% to 32% across the pair; weekly usage moved from 29% to 33%. These are account-level integer snapshots and cannot be attributed solely to either arm. The free reset credit was not used.

## Fixed identity

- Product HEAD at preparation: `2f41c82157aafa5823388717f347a2d4b6076846`
- Fixture: `d9fee6d137d7355b24c287f6f976b44400d80b38`
- Isolated commit in both arms: `06a59f2399de2b8cca06dc42437922c30f159cf0`
- Fixture tree in both arms: `2dfce65d28daab23e00a203edf28f2fc0af1ae1f`
- Task: `task-bounded-context`
- Model/reasoning: `gpt-5.6-terra` / `medium`
- Permission profile: `preauthorized-write-build-v1`
- Evaluator registry SHA-256: `7b4425580a6b96fae3edb94274b9a605188fe75c86bc0cc36f8f13dfe01846d9`
- Trial harness SHA-256: `4f107830a23449ee1ef1e73e070f2081e1ee48d4f5a7e49cea406dbaa55f0039`
- Pair prepared: `2026-09-22T08:07Z`
- Preserved raw report: `%TEMP%/arifce-core-pair-serialized-20260922-1107/pair-report.json`

## Exact workflow failure

The treatment prompt incorrectly said to repeat PowerShell's `-Path` parameter once per changed path. PowerShell rejected the first evidence command because a named parameter cannot be specified more than once. The agent retried with an array value while another long check was still active; that verification action ended with host exit `-1` and produced no evidence. The candidate nevertheless passed the external repository suite, public API gate, and both pinned behavior tests. Its canonical task stayed open, its claim stayed unverified, and its handoff correctly reported `Completion: INCOMPLETE`. The harness therefore classified `arifceWorkflowPassed=false`.

The wrapper now accepts a single comma-separated `-PathCsv` value, validates at least one changed path, and the treatment contract makes the evidence-producing test the only final full-suite run. It explicitly requires the agent to wait for that action's exit code before any further tool action. Prompt, telemetry, isolation, and completion-provenance regressions cover the corrected command shape. A fresh metered pair must wait for another clean window; this run is immutable and remains ineligible.
