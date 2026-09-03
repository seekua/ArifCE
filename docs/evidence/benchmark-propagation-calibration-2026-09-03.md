# Acceptance-basis propagation remediation and evaluator calibration

## Finding and reproduction

TASK-0022 / FINDING-0008 records a false-ACCEPTED risk found while replacing the stale-propagation evaluator. New current sibling evidence kept a claim supported and incorrectly protected an earlier acceptance whose own evidence was stale. Missing/foreign evidence or an orphaned claim could also leave accepted records current. NeedsReview warnings disappeared on later refreshes. New acceptance creation allowed a disputed claim or foreign evidence.

Against the pre-fix implementation at `2b09254d8d0a9e86efa2867cce0d6152f6c5c426`, three of the initial four lifecycle tests failed; the subsequently added fifth test also failed because creation did not throw on foreign evidence. Reproduce by injecting the five tests from `f6c35003242d87806ee3002e70497adbba6dffd9` into an isolated export of the pre-fix source. Never overwrite a working repository to reproduce a regression.

## Implementation and independent contract

Refresh now checks an acceptance's original evidence IDs, their membership in the claim, ownership and freshness, and the claim's eligibility. Fresh sibling evidence may support a claim but cannot renew old approval. Missing, malformed, foreign or unknown-freshness evidence fails the basis check. Already-NeedsReview warnings repeat in handoffs; revoked records are untouched. Creation shares the owned-evidence check and permits only Supported, PartiallyVerified or Verified claims.

The implementation does not rewrite old evidence or create a new lifecycle/schema. Stale claims do not automatically regain trust when source bytes are restored. Explicit re-verification and new acceptance remain separate actions.

The whole five-test fixture is pinned at `f6c35003242d87806ee3002e70497adbba6dffd9`, transformed only to the independent test namespace/class, and selected by fully qualified method names. The public task contract discloses scoped positive/current cases, metadata exclusion, stale transitions, repeated refresh, mixed old/new evidence, missing/malformed/foreign records, unknown freshness, contradicted/disputed claims, explicit replacement acceptance, revocation and persistent canonical handoffs. No legacy generic-shell approval is a prerequisite. Both benchmark arms receive the same contract.

## Calibration

Run `./scripts/test-engineering-benchmark-propagation-calibration.ps1 -SourceCommit <commit>` to export an isolated source copy and check seven controls:

| Control | Required executed-test result |
| --- | --- |
| Unmodified good implementation | PASSED |
| Never stale a claim | FAILED |
| Always stale a claim | FAILED |
| Skip acceptance's own basis when its claim remains current | FAILED |
| Forget record-specific NeedsReview warnings | FAILED |
| Skip evidence ownership | FAILED |
| Skip handoff trust refresh | FAILED |

Compiler or runner errors do not count as caught mutations. Every expected test must execute. Failed calibration copies retain logs; successful copies are cleaned up.

ATTEMPT-0014 records the first calibration failure: the never-stale mutation left `hasCurrentEvidence` unused and compilation failed with CS0219. Assessment returned ERROR, not FAILED, and correctly stopped calibration. The corrected temporary mutation explicitly consumes the variable; product code, warnings-as-errors and test assertions were not weakened. The first failed copy retains its logs locally.

## Verification status

All 97 local product tests pass after the fix. Manifest, evaluator registry, executed-assessment and secret-scan checks pass. Local calibration and independent completion integration are in progress; remote closure is pending. This report deliberately does not mark Phase 72 closed before those results exist.

## Limits and remaining work

The fixture uses real Git repositories and synthetic Low-risk FIXTURE evidence. It does not prove actual build/review execution or evidence issuer authenticity. Fixed `## Trust Warnings` / `## Knowledge Warnings` headings delimit the assertions and are a disclosed compatibility requirement. OLD_DECISION remains a lifecycle proxy, not a measured historical-decision revisit by a model.

Full decision/invariant graph transitivity, concurrent refresh transaction isolation, every malformed schema shape, and policy re-evaluation after configuration changes remain unproven. These are not silently claimed covered by this patch. There is no new dependency, canonical schema migration or weakened generic-command policy.

FINDING-0005 remains open and productClaimEligible remains false. Four of ten objectives were previously calibrated; this phase addresses the fifth. The remaining five objectives are deterministic code graph, change contracts, flight recorder, MCP validation and unfinished-verification policy. Fresh, permission-matched repeated model trials are separate work; no speed, token-saving or effectiveness percentage is established here.
