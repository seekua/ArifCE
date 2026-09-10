# Benchmark harness reliability gate — 2026-09-10

## Outcome

The harness now separates public compilation requirements from hidden behavioral assertions. Every engineering task has a public C# API probe bound into both trial prompts and sessions by SHA-256. A cheap compile-only gate runs before the independent evaluator; missing types, properties, methods, or overloads can no longer appear as a hidden evaluator surprise.

Windows trials are rejected when their output root is nested under the source repository. Both arms receive the same structured-edit rule and compact build/test wrapper. Large source payloads embedded in shell commands, exact unchanged full-read/search repeats, direct unbounded build/test output, and repeated failures without replanning are recorded as harness-policy violations.

The ArifCE arm must exercise context, canonical search, task/claim, evidence, and handoff operations. Reading only `PROTOCOL.md` or `CURRENT.md` is not a valid treatment. The baseline receives the identical task, public contract, fixture, check wrapper, model, reasoning setting, token target, and permission profile, without ArifCE memory access.

## Metrics

Captured Codex JSONL now records:

- non-cached input and output as the primary task-cost metric;
- cache-included total and churn ratio;
- visible file-read, repeated-file, search, edit/retry, build/test, and ArifCE workflow estimates;
- model/tool rounds;
- approximate Useful Context Ratio and Context Amplification Factor;
- failed-run tokens separately from successful-task comparisons.

The 50,000 primary-token value remains diagnostic until evaluator-passing matched runs establish whether it is realistic. It does not currently turn a correct run into a failed task.

## Local verification

- All ten public API probes compile against the current reference implementation.
- API contract tampering is rejected.
- Context-efficiency fixtures detect repeated reads/searches, shell-embedded source, compact checks, and replanning signals.
- Trial isolation, suite rejection, token parsing, and completion provenance smoke tests pass.

These are harness tests, not evidence that ArifCE improves task correctness, tokens, or elapsed time. A fresh matched model run is still required; `productClaimEligible` remains false.
