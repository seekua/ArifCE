# Structured flight-recorder evaluator calibration

## Verdict and scope

TASK-0025 addresses the eighth evaluator objective in FINDING-0005. The previous pinned test covered one task-linked failed attempt, summary redaction, successful finish and handoff inclusion. It did not check other persisted free-text fields, invalid links, terminal mutation, failure completion or record growth.

Inspection found two product defects rather than evaluator weakness alone. Provider, agent and outcome values bypassed `SecretRedactor`; a run also accepted an unbounded number of structured steps. FINDING-0009 and FINDING-0010 record those defects. Production code now redacts and truncates all persisted run text fields, accepts only bounded repository-shaped related IDs, reserves the terminal Result slot and enforces a 64-step maximum.

## Pinned public contract

The independent evaluator source is pinned at `064321b424f9c28706c8cfe5838fc815c1406026`. Its source transformer changes only the namespace, class and constructor names. Evaluation selects all three registered methods.

- Start a task-linked run with synthetic secrets in provider, agent and goal; record Investigation, Evidence, Decision and failed Attempt steps; assert truncation, redaction, link de-duplication, canonical failed-attempt promotion and absence of those values from run JSON and journal JSONL.
- Finish successful and failed runs, assert Result metadata and terminal state, reject missing tasks and malformed related IDs without canonical side effects, and reject post-terminal recording or repeated finish.
- Record 63 non-terminal steps, reject a 64th non-terminal step without canonical mutation, then finish with the reserved Result slot for exactly 64 steps and a bounded canonical file.

The fixture initializes a real temporary Git repository because run records capture repository state. It does not rely on mocks for canonical storage, journaling, Git capture or failed-attempt promotion.

## Calibration controls

Run `./scripts/test-engineering-benchmark-flight-recorder-calibration.ps1 -SourceCommit <commit>`. The isolated calibration requires these executed-test results:

| Control | Required result |
| --- | --- |
| Unmodified implementation | PASSED |
| Persist the provider without redaction | FAILED |
| Persist the outcome without redaction | FAILED |
| Suppress failed-attempt promotion | FAILED |
| Remove terminal-state mutation guards | FAILED |
| Remove the structured step budget | FAILED |
| Accept a malformed related ID | FAILED |

Each expected method must execute exactly once. Restore, compile and runner errors are ERROR, never successful mutant detection. Failed temporary fixtures remain available for diagnosis; a successful calibration removes only its validated temporary directory.

## Local verification

- All 108 product tests pass with zero failures and zero skips.
- The pinned good control passes and all six deliberately incorrect variants fail through executed assertions.
- Independent benchmark completion injects the pinned `flight-recorder` fixture into an isolated candidate checkout and passes provenance, telemetry and tamper checks.
- The evaluator registry accepts the new fixture and retains rejection checks for missing and unsafe evaluator definitions.
- The repository secret scan passes. Synthetic values are allow-listed only by exact path, pattern and value; the scanner's changed-value and wrong-path controls still reject.

## Remote closure

Published commit `dadddc3cdd2b477f5e804ff9043b231bce3d01fb` passed the three-OS quality matrix, Linux independent-completion provenance and flight-recorder good/bad controls, and five self-contained package smoke checks in [GitHub Actions run 34215866974](https://github.com/seekua/ArifCE/actions/runs/34215866974). TASK-0025 is completed. The remote result establishes the tested evaluator boundary; it is not a product-effectiveness result.

## Limits and remaining work

The 64-step bound limits each run record, not total journal growth; journal rotation remains a separate repository-level control. Common assignment and bearer-credential patterns are tested, not every possible secret encoding. Related IDs are shape-validated but are not required to resolve because a step may refer to an entity written by another concurrent participant.

The tests do not establish process-crash recovery between failed-attempt promotion and run update, simultaneous finish/failed-attempt atomicity, automatic extraction from an external agent host, semantic summary quality or useful ranking of every failed attempt. Structured summaries remain explicit inputs; raw transcripts are neither accepted nor claimed to be automatically converted.

All ten evaluator objectives are now strengthened, calibrated and remotely executed. FINDING-0005 remains open as the guard against premature product-effectiveness claims; `productClaimEligible` remains false until fresh, repeated, permission-matched model trials are available. No time, token-saving or product-effectiveness percentage is established here.
