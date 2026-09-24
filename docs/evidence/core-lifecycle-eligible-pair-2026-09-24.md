# Eligible core lifecycle matched-pair result

This is the first fully eligible matched `task-bounded-context` comparison after the corrected harness. It is a valid measurement, not a product-effectiveness claim beyond this one task. Both candidates passed every predeclared completion and quality gate. ArifCE used more measured primary tokens than the baseline, so this result does not support a token-savings claim.

## Raw pair table

| Metric | Baseline | ArifCE |
| --- | ---: | ---: |
| Independent evaluator | PASSED | PASSED |
| Repository tests | PASSED | PASSED |
| Public API gate | PASSED | PASSED |
| Requirements complete | YES | YES |
| No regression | YES | YES |
| ArifCE workflow | N/A | PASSED |
| Infrastructure/policy clean | YES | YES |
| Comparison eligible | YES | YES |
| Cache-excluded input + output | 90,935 | 154,458 |
| Context Amplification Factor | 52.792 | 61.677 |
| Useful Context Ratio | 0.018942 | 0.016214 |
| Non-cached input | 75,607 | 131,287 |
| Output | 15,328 | 23,171 |
| Cache-included total | 2,038,583 | 4,214,362 |
| Churn ratio | 22.418024 | 27.284841 |
| Model/tool rounds | 39 | 64 |
| File-read token estimate | 22,628 | 46,718 |
| Repeated file-context estimate | 0 | 0 |
| Search token estimate | 15,697 | 21,236 |
| Edit/retry token estimate | 6,112 | 320 |
| Build/test token estimate | 2,189 | 27,788 |
| ArifCE visible overhead estimate | 0 | 38,397 |
| Captured host elapsed | 674,612 ms | 919,782 ms |

The successful-task primary-token delta is `+63,523` for ArifCE (`154,458 - 90,935`). The conservative prevented-cost estimate is `0`: ArifCE consumed more primary tokens even before any claim of avoided rediscovery could be made.

Codex Plus five-hour usage moved from 2% to 15% across the pair. This is an account-level integer snapshot, not a causal allocation to either arm. The free reset credit was not used.

## Fixed identity

- Product HEAD at preparation: `250469c8d96d0d9e1ea214d2797437e281257a03`
- Fixture: `d9fee6d137d7355b24c287f6f976b44400d80b38`
- Isolated commit in both arms: `ca5b9e45007bf9b1eff44481acb84b0896b959fe`
- Fixture tree in both arms: `44aa8ffb10272bf68a6a22ef7b6724df04f4c7bb`
- Task: `task-bounded-context`
- Model/reasoning: `gpt-5.6-terra` / `medium`
- Token budget: 50,000 cache-excluded input plus output, diagnostic rather than an eligibility criterion
- Permission profile: `preauthorized-write-build-v1`
- Acceptance contract SHA-256: `00918ceecb0da76d67ed0e39fb1cc55a1ac6e35fa3b5720c0d0464b61e563d3a`
- API contract SHA-256: `568078c00bb47bedc9737f699a25f75a4779ea799174b6a56b842aaf7b84cd8b`
- Baseline and ArifCE arms were prepared before either host result was inspected. Baseline preceded ArifCE.

## Eligibility and limitations

Both arms have committed candidates, verified provenance, passing repository suites, passing API gates, passing independent evaluators, and no context-efficiency policy violations. The ArifCE arm also completed the required `TASK → CONTEXT → WORK → CLAIM → VERIFY → HANDOFF` workflow. Raw host captures remain local because they contain transient runtime material; this evidence intentionally contains no local paths, prompts, logs, or account identifiers.

The reported file, search, edit, and build figures are visible-JSONL estimates at four characters per token. Useful Context Ratio counts first unique file-read and search output as useful; semantic necessity cannot be observed from host telemetry. One eligible task is insufficient to claim general performance, product value, or an ArifCE advantage. Lower-model trials are now permitted by the gate, but must remain separately predeclared and use fresh matched arms.
