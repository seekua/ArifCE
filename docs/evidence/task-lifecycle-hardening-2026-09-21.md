# Task lifecycle hardening — local verification

This change strengthens the existing `TASK → CONTEXT → WORK → CLAIM → VERIFY → HANDOFF` path. It does not establish product-effectiveness or token-savings claims.

## Implemented and locally checked

- A task may carry an objective, scope, invariants, and ordered `done_when` criteria while older task JSON remains readable.
- A task-linked claim and current, successful, kind-matching, claim-owned evidence are required for contracted completion. Claim risk cannot be lower than task risk. Existing acceptance policy applies to higher-risk completion.
- Completion stores a digest of the engineering contract. Relevant evidence changes or contract edits change `task check` from `VERIFIED` to `NEEDS_REVERIFY`; new evidence may renew completion.
- `context --task` pins the contract and current completion state, then fills the remaining estimated-token budget using the existing deterministic context composer. Excluded snippets are blank in serialized results.
- `handoff --task` emits a compact objective, scope, invariants, linked claims/evidence, failed attempts, unresolved findings, repository state and next action. It is not a transcript.
- CLI and MCP use the same `ProjectService` contract and completion checks.

Local verification on 2026-09-21: `dotnet test tests/ArifCE.Tests/ArifCE.Tests.csproj -c Release --disable-build-servers -m:1 --nologo --verbosity quiet` passed 117/117; CLI and MCP Release builds passed with zero warnings and zero errors. Targeted regressions cover foreign/stale evidence, contract changes, renewed verification, lower-risk claims, path-like IDs, context budget exclusion and MCP tool registration. `scripts/check-version-consistency.ps1` passed. `scripts/package-smoke.ps1 -Configuration Release` also packed and installed the local tool into a disposable Git repository, then completed the contracted task, task-aware context, linked claim/evidence, completion check and task handoff flow before exercising the existing package scenarios.

## Not proven by these checks

- Textual criterion meaning and invariant wording are not formally verified. A matching evidence kind alone does not prove that the chosen check is sufficient for the stated engineering goal.
- Estimated tokens use a character-based approximation, not a model tokenizer.
- The task-aware lexical context path has not yet shown measurable reduction in rediscovery or equal-or-better task success against a matched baseline.
- No fresh metered baseline/ArifCE pair, handoff recovery study, or external CI run was performed for this change. Phase 79 and the V1 effectiveness gate remain open.

The next validation step is to run the existing matched benchmark harness on identical checkout/task/evaluator conditions, report failures as well as successes, and only compare token efficiency for evaluator-passing pairs. Do not infer a percentage benefit from these deterministic tests.
