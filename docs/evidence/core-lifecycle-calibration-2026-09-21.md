# Core lifecycle benchmark calibration

This calibration prepares one narrow matched task for the owner-approved V1 lifecycle. It does not replace the ten-task product study and does not establish an effectiveness or token-saving claim.

## Fixed task and fixture

- Task: `task-bounded-context`
- Fixture: `d9fee6d137d7355b24c287f6f976b44400d80b38`
- Public API contract: `benchmarks/api-contracts/task-bounded-context.cs`
- Trial manifest: `benchmarks/core-lifecycle-calibration.json`
- Pinned evaluator source: `6caca33ca8e5d213ceaa36527575e0f3fffe3275`
- Evaluator registry: `benchmarks/core-lifecycle-evaluators.json`

The fixture already contains the contracted CLI workflow required by the ArifCE arm, but predates deterministic task-link retrieval. Both arms therefore start with the same real missing behavior. The public contract discloses the required API and behavior without disclosing evaluator implementation.

## Local controls

The public compile/API gate passed both controls.

- Known-good control: applying only the production `LlmContextComposer` change from `04a957744aec4c0f225bd5be78288443421dae62` produced a clean candidate. Product tests passed and the two pinned independent evaluator methods reported `PASSED`.
- Negative control: leaving the fixture unchanged produced no candidate. Existing product tests and the API gate still passed, while the independent evaluator reported `FAILED` for the missing behavior.

The first known-good evaluator attempt exposed a harness source-extraction defect: when the last selected test was followed by a public `Dispose` member, that member was copied into a scaffold that already supplied `Dispose`, causing evaluator compilation to be classified as `ERROR`. The extractor now stops at the next top-level public, internal, protected, private, or `[Fact]` member. A fresh known-good control then passed. The errored attempt is not treated as product evidence.

## What the evaluator proves

The bounded fixtures establish that task-aware context:

- pins the task contract;
- selects explicitly task-linked failed attempts, claims/evidence, unresolved findings, and the latest task handoff without requiring lexical overlap;
- excludes matching records owned by another task, including derived evidence and handoffs, with `OUT_OF_SCOPE` reasons and empty rejected snippets;
- respects the estimated-token budget, emits no partial contract, avoids duplicate sources, and reports complete task-assembly time.

## What remains unproven

No model was invoked during these controls. Useful Context Ratio, Context Amplification Factor, non-cached input/output, cache-inclusive processing, elapsed host time, and Codex usage must come from a fresh matched baseline/ArifCE pair. The pair must use the same fixture, task, model, reasoning, permission profile, evaluator, and settings. Both arms must pass product tests, the API gate, and the independent evaluator before token efficiency is compared.
