# Change-contract evaluator calibration

## Verdict and scope

TASK-0024 addresses the seventh evaluator objective in FINDING-0005. The previous pinned test inspected selected contract fields and a linked claim, without asserting persisted risk/confidence, rejection side effects, scoped evidence or acceptance transitions.

Four replacement tests pass against the existing product implementation. No production-code rewrite, new dependency or canonical schema change was needed. These results establish bounded lifecycle behavior, not complete API-change correctness or product effectiveness.

## Pinned public contract

Source and fixture helpers are pinned at `5db75ef1e9443615188d0390979aaba9c98874b3`. Independent evaluation transforms only namespace/class names and selects all four registered methods. The same public acceptance contract is provided to both benchmark arms.

- Create exactly one normal Unverified claim per Open contract, preserving risk, invariants, lexical decision history and relationship confidence. Reload the contract and check all four risk levels' stated verification requirements. No evidence or acceptance is created prematurely.
- Reject blank/partial/missing targets, missing/foreign contract links and escaping additional paths without modifying canonical bytes (including journal) or executing the marker-writing command.
- Verify a qualified method with an explicitly approved `echo contract-probe > contract-probe.txt` probe. Persist contract ID, closure digest and additive content scopes. Metadata, heuristic caller edits, unrelated same-name declarations and graph-cache deletion preserve freshness; target/additional-scope edits invalidate it. Refresh updates the existing claim and acceptance without rewriting the original contract.
- Project-linked evidence captures reverse transitive exact dependents, ignores unrelated project content, invalidates on dependent content/new dependents and appends new evidence when verification is repeated.

## Calibration controls

Run `./scripts/test-engineering-benchmark-contract-calibration.ps1 -SourceCommit <commit>`. It exports an isolated source tree and requires these executed-test results:

| Control | Required result |
| --- | --- |
| Unmodified implementation | PASSED |
| Allow a contract to verify another claim | FAILED |
| Promote heuristic impact confidence to EXACT | FAILED |
| Discard explicit invariants | FAILED |
| Omit the Critical human-acceptance requirement | FAILED |
| Ignore explicit additional scope paths | FAILED |
| Omit the trusted-closure digest | FAILED |

Each expected test must execute exactly once. Compile/restore/runner errors are ERROR, not successful mutant detection. Failed temporary copies retain logs; successful copies are removed after bounded path validation.

## Verification status

All 105 local product tests pass, with zero failures/skips. Calibration, independent completion integration and remote closure are pending. The product implementation is unchanged; no production bug is claimed solely because older evaluator assertions were weak.

## Limits and remaining work

Real Git and explicitly approved platform-shell echo/redirection probes are used. They are Supported command evidence, not successful BUILD/TEST_RUN evidence or proof that an invariant holds. This explicit approval/command compatibility requirement is disclosed equally to both arms. Critical risk requirement text is checked; real human/reviewer identity and full Critical acceptance enforcement are separate concerns.

Atomic recovery after mid-creation failure, simultaneous writers, arbitrary command execution policy, every historical memory type, complete semantic impact, and malicious canonical/cache edits are not established. Contract status remains a pre-change Open record; acceptance lives on the linked claim and does not automatically complete the contract. API_CHANGE remains a lifecycle proxy rather than a real measured API modification.

Six evaluator objectives were previously calibrated; this phase addresses the seventh. Flight recorder, MCP validation and unfinished-verification policy remain before fresh permission-matched repeated model runs with captured usage and host timing. FINDING-0005 remains open and productClaimEligible remains false. No time/token-saving percentage is claimed.
