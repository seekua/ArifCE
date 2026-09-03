# Secret-boundary and acceptance evaluator calibration

## Scope

Phase 69 replaces two weak benchmark evaluators, not the production implementation. The old secret test could pass on an unrelated exception and did not count provider calls. The old acceptance test could pass an implementation that rejected every request. Their replacements are pinned to `2e8b7418391022997b95295369c0aa9766306593` in `benchmarks/evaluators.json`.

Both task prompts were updated to disclose the new requirements equally. The original engineering objectives are unchanged. The Phase 68 audit remains a historical description of its registry revision.

## What is now tested

- Secret boundary: a real initialized Git repository; a successful clean request and persisted evidence; zero additional provider calls/evidence writes for a blocked password-bearing prompt; safe text retained from a secret-bearing response while its secret and RawResponse are removed; reloaded evidence JSON and the journal inspected on disk.
- High-risk acceptance: a successful accepted record reloaded from storage, plus independent rejection for missing build, tests or review, failed build/tests, disagreeing review, and changed source after evidence. Rejections must not leave acceptance JSON behind.

The acceptance records are explicitly synthetic policy fixtures, not claims that a build/test process actually ran. The provider is deterministic and local; no paid API or real model is used.

## Calibration

The calibration script exports a pinned Git tree into a validated temporary directory. It applies each mutation only there, transforms the same pinned safety test source used by the independent evaluator, and scores TRX assertions rather than compiler exit alone. Mutation anchors must match exactly once; drift fails closed.

| Control | Observed local outcome |
| --- | --- |
| Current implementation | PASSED |
| Reject all LLM requests | FAILED as required |
| Check secret only after provider call | FAILED as required |
| Return unredacted provider response | FAILED as required |
| Reject all acceptances | FAILED as required |
| Skip build requirement | FAILED as required |
| Skip test requirement | FAILED as required |
| Skip review requirement | FAILED as required |

All eight controls produced their expected outcomes on Windows against `2e8b7418391022997b95295369c0aa9766306593`. A build/restore/runner error would be ERROR and would fail calibration; it is not counted as detecting a mutant. Successful temporary fixtures are cleaned up; failed fixtures are retained for diagnosis.

All 85 product behavior tests pass locally. Manifest, assessment and registry checks also pass. Both safety tasks pass independent completion integration through isolated Git/.NET trials and TRX scoring. CI's trusted source checkout fetches history so safety integration resolves the actual pinned commit; candidate checkouts still have isolated history.

The first CI run, 33722871828, rejected the intentionally synthetic password fixture because its new test path was not in the scanner's existing fixture policy. ATTEMPT-0012 records the failure. The correction adds only the same known synthetic value/pattern for BenchmarkSafetyTests.cs. A new regression confirms that a different value in that file and the same value in another file are still rejected; the complete local secret scan passes. No broad scanner rule was disabled.

## Remote proof

Commit `41b4fed` passed [GitHub Actions run 33723265873](https://github.com/seekua/ArifCE/actions/runs/33723265873). All three OS build/test/package jobs and five self-contained binary targets succeeded. Ubuntu independently ran both pinned safety evaluators and the good/seven-mutant calibration. Windows, macOS and Ubuntu ran all 85 product tests and the bounded scanner-fixture regression. Phase 69 is closed against this commit; the registry's safety source pin remains `2e8b7418391022997b95295369c0aa9766306593`.

## Limits and next work

This is finite mutation calibration, not exhaustive correctness or a model A/B experiment. Password assignment coverage does not establish every secret format or metadata field. The acceptance matrix does not establish Critical-risk policy, reviewer identity independence, evidence issuer authenticity, or every dependency freshness case.

FINDING-0005 remains open: real cross-process/index-rebuild coverage and the remaining eight task evaluators still need strengthening/calibration. Collection continues to mark productClaimEligible=false. Previous benchmark results are not rescored under the new registry; a new study must use fresh trials and matching public contracts.
