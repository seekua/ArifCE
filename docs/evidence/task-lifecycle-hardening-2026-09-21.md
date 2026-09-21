# Task lifecycle hardening — local verification

This change strengthens the existing `TASK → CONTEXT → WORK → CLAIM → VERIFY → HANDOFF` path. It does not establish product-effectiveness or token-savings claims.

## Implemented and locally checked

- A task may carry an objective, scope, invariants, and ordered `done_when` criteria while older task JSON remains readable.
- A task-linked claim and current, successful, kind-matching, claim-owned evidence are required for contracted completion. Claim risk cannot be lower than task risk. Existing acceptance policy applies to higher-risk completion.
- Completion stores a digest of the engineering contract. Relevant evidence changes or contract edits change `task check` from `VERIFIED` to `NEEDS_REVERIFY`; new evidence may renew completion.
- `context --task` pins the contract and current completion state, then deterministically considers task-linked failed attempts, claims and evidence, unresolved findings, and the latest task handoff before using lexical retrieval for the remaining estimated-token budget. Records linked to another task are marked `OUT_OF_SCOPE` when encountered. Every excluded snippet is blank in serialized results.
- `handoff --task` emits a compact objective, scope, invariants, linked claims/evidence, failed attempts, unresolved findings, repository state and next action. It is not a transcript.
- CLI and MCP use the same `ProjectService` contract and completion checks.

Local verification on 2026-09-21: `dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj -c Release --disable-build-servers -m:1 --nologo --verbosity quiet` passed 117/117; CLI and MCP Release builds passed with zero warnings and zero errors. Targeted regressions cover foreign/stale evidence, contract changes, renewed verification, lower-risk claims, path-like IDs, context budget exclusion and MCP tool registration. `scripts/check-version-consistency.ps1` passed. `scripts/package-smoke.ps1 -Configuration Release` also packed and installed the local tool into a disposable Git repository, then completed the contracted task, task-aware context, linked claim/evidence, completion check and task handoff flow before exercising the existing package scenarios.

## Not proven by these checks

- Textual criterion meaning and invariant wording are not formally verified. A matching evidence kind alone does not prove that the chosen check is sufficient for the stated engineering goal.
- Estimated tokens use a character-based approximation, not a model tokenizer.
- The task-aware bounded context path has not yet shown measurable reduction in rediscovery or equal-or-better task success against a matched baseline.
- No fresh metered baseline/ArifCE pair, handoff recovery study, or external CI run was performed for this change. Phase 79 and the V1 effectiveness gate remain open.

The bounded-context evaluator now has a [fixed local calibration](core-lifecycle-calibration-2026-09-21.md): the known-good production patch passes while the unchanged pre-feature fixture fails. The next validation step is a matched model pair on identical checkout/task/evaluator conditions, reporting failures as well as successes and comparing token efficiency only if both arms pass. Do not infer a percentage benefit from deterministic controls.

## Benchmark harness integration

The ArifCE arm now instructs a participant to create a fresh four-field task contract, obtain `context --task`, search canonical memory, create a task-linked claim, persist named test evidence, complete and check the task, and finish with `handoff --task`. The result collector requires separate successful host command events for every one of those stages; generic commands or failed command text no longer satisfy the workflow gate. The trial-isolation smoke test and completion/provenance smoke test pass locally. This proves harness enforcement, not an ArifCE effectiveness advantage; no metered model pair was run.
