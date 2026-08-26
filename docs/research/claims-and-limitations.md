# Claims and Limitations

## Supported product claims

ArifCE is designed to keep project context with a repository; preserve decisions and failed attempts; link claims to evidence; support cross-agent handoff and independent review records; retrieve selected context under an explicit budget; track refactors explicitly; and link Git state to rationale.

## Claims intentionally not made

ArifCE does not guarantee correctness, prevent hallucinations, make generated code safe, or guarantee that cross-agent review improves quality. It publishes no token-reduction percentage. Those outcomes depend on task, evidence, policy, model, and repository, and require benchmarks.

## V0.1 limitations

Ranking is lexical and deterministic rather than semantic-vector based. Token counts are estimates. Git dirty-state fingerprints detect change but do not prove semantic equivalence. Secret redaction is defense in depth, not a substitute for preventing credentials from entering transcripts. External-agent review is represented but not automatically invoked. SQLite FTS behavior may vary with tokenizer and platform build.

## Benchmark policy

Continuity, context efficiency, verification accuracy, refactor completion, and verification overhead will be measured against fixed fixtures. Results must report fixture, commit, commands, environment, input budget, outcome, and failures. Failed or inconclusive runs remain visible; results are never invented.
