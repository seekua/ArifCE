# Benchmark public-contract and scoring audit

## Verdict

Historical audit snapshot: registry at `24d80d9`. Phase 69 [replaces and calibrates the secret-boundary and acceptance evaluators](benchmark-safety-calibration-2026-09-03.md); the remaining gaps below are not automatically closed by that work.

Phase 70's [storage evaluator report](benchmark-storage-calibration-2026-09-03.md) tracks the subsequent cross-process/index replacement. The table below remains the historical audit, not a claim that newer tests still have the same coverage.

The first suite is not suitable for product-effectiveness claims. In addition to permission variance, pinned tests contain undisclosed API/message dependencies and partial behavioral coverage. Compilation or restore failures were previously collapsed into the same boolean as failed assertions. The historical report is retained, not retrospectively rewritten as a valid experiment.

This audit reads the exact source commits and methods in `benchmarks/evaluators.json`, not current tests with similar names. Exposing their compatibility requirements fixes a fairness defect, but does not repair weak assertions. The original engineering objectives remain in the manifest; limitations do not redefine them downward.

## Per-task evidence

| Task | What the pinned evaluator actually checks | Remaining gap |
| --- | --- | --- |
| trust-dirty-content | One untracked-file byte change and stale comparison, with legacy generic-command verification setup | Setup includes unrelated command-status expectations; no full Git failure matrix |
| acceptance-risk-policy | High-risk acceptance throws after generic evidence only | Reject-all implementation can pass; no successful acceptance or evidence-kind matrix |
| llm-secret-boundary | ExecuteAsync throws for a secret-bearing prompt in a directory with an empty .git folder | No provider-call counter or response-persistence assertion; unrelated repository errors can satisfy the assertion |
| canonical-concurrency | Twenty Task.WhenAll updates preserve evidence links | One process only; no process contention, crashes or index rebuild |
| stale-propagation | Specific refresh counters, enum states and handoff phrases | API/message expectations were unstated; metadata-only freshness not asserted |
| deterministic-code-graph | CodeGraphStore methods, graph shapes, edge names and a fixed cache path | Heuristic confidence and rebuild equivalence not fully asserted |
| change-impact-contract | Required service method, ID prefixes, selected paths/history/invariants and verification strings | No full lifecycle-duplication or impact-completeness proof |
| structured-flight-recorder | Specific run API, step types, failure promotion, one redaction path and handoff inclusion | No size bounds or full secret-field coverage |
| mcp-validation | JSON-RPC codes, three exact message substrings and one oversized line | Canonical side effects and pre-access rejection not inspected |
| unfinished-verification-policy | Command classification and two rejected calls | Original claim instance inspected instead of reloaded storage; unsafe-success status not checked |

These are findings about evaluator strength, not evidence that the corresponding product implementations are broken. Except for the supported requirements now disclosed, the original evaluator sources remain pinned and unchanged.

## Implemented corrections

- Manifest schema 2 publishes acceptanceContract and evaluationLimitations for all ten tasks. Both arms receive exactly the same text; a deterministic SHA-256 digest is stored in session metadata and checked during collection. Schema 1 remains preparable for historical reproduction, without fabricating a contract.
- The independent evaluator requests a TRX test-result artifact. Scoring requires exactly the pinned test identities, each executed once with Passed or Failed, and a compatible process exit. Missing/malformed/skipped/duplicate results and compilation/restore/runner failures yield ERROR with null taskPassed, not a scored failure.
- Collection verifies TRX hashes, reparses assessments and rejects unscorable runs. Their artifacts are retained; the operator must report errors rather than silently dropping them. Exit-only historical evaluations cannot be collected as modern scored evidence.
- Suite schema 5 sets productClaimEligible to false. Passing pinned assertions is diagnostic, not complete task correctness or product value.

## Tests and proof

Local contract/assessment fixtures pass malformed XML, disabled external entities, missing/duplicate/skipped tests, wrong identity, exit-code disagreement, blank contracts and legacy schema cases. Trial isolation tests verify both arms receive the same contract and digest. All 83 behavior tests pass; completion integration passes with a real isolated Git/.NET candidate and a hashed TRX artifact containing the expected executed test. Registry and suite-rejection checks also pass. The classifier's failure-mode cases use synthetic TRX fixtures; they are not a live model study.

Implementation `24d80d9` passed [GitHub Actions run 33713426843](https://github.com/seekua/ArifCE/actions/runs/33713426843): three OS build/test/package jobs and all five self-contained binary targets succeeded. Phase 68's disclosure/scoring safeguards are closed against this commit. Coverage remediation remains OPEN as FINDING-0005; successful CI does not close that finding.

## Next remediation before a real comparative rerun

1. Replace false-positive-prone secret-boundary coverage with a real Git fixture, provider invocation counters, and persisted response inspection.
2. Add positive and negative acceptance-policy controls, plus real multi-process canonical concurrency and index-rebuild coverage.
3. Strengthen the remaining assertions to cover their stated objectives, and distinguish required public compatibility from replaceable implementation details.
4. Calibrate each evaluator with a known-good candidate and deliberately incorrect controls before fixing the next manifest/registry revision.
5. Only then run equal-permission, matched fresh sessions with captured usage and host timing. Active-work timing is still unavailable; do not call process elapsed time active work.

No new model was invoked and no new A/B result was generated by this audit. Hashes and TRX consistency are not protection against an operator controlling all artifacts or a compromised test toolchain.
