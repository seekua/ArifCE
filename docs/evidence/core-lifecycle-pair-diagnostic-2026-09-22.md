# Core lifecycle matched-pair diagnostic

This is a preserved negative benchmark result, not an effectiveness claim. Both candidates passed repository tests, the public API gate, and the pinned independent evaluator, but the ArifCE treatment used an incorrect CLI assembly path supplied by the harness. The pair is therefore ineligible for successful-task token comparison.

## Raw pair table

| Metric | Baseline | ArifCE |
| --- | ---: | ---: |
| Independent evaluator | PASSED | PASSED |
| Repository tests | PASSED | PASSED |
| Public API gate | PASSED | PASSED |
| Cache-excluded input + output | 66,422 | 119,923 |
| Context Amplification Factor | 16.615 | 75.235 |
| Useful Context Ratio | 0.060185 | 0.013292 |
| Non-cached input | 57,666 | 103,029 |
| Output | 8,756 | 16,894 |
| Cache-included total | 765,558 | 3,065,203 |
| Churn ratio | 11.525669 | 25.559759 |
| Model/tool rounds | 14 | 53 |
| File-read token estimate | 21,364 | 27,440 |
| Repeated file-context estimate | 0 | 0 |
| Search token estimate | 24,184 | 13,077 |
| Edit/retry token estimate | 51 | 921 |
| Build/test token estimate | 4,270 | 3,006 |
| Captured host elapsed | 300,333 ms | 652,865 ms |

Codex Plus five-hour usage moved from 0% to 38% across the complete pair. This is an account-level integer snapshot and is not attributable solely to either arm. Weekly usage moved from 10% to 16%. The free reset credit was not used.

## Fixed identity

- Product HEAD at preparation: `d1359df1e4ed10e73d3a4ba82f8f32d4344eb514`
- Fixture: `d9fee6d137d7355b24c287f6f976b44400d80b38`
- Isolated commit in both arms: `2c1092c017254c79c0997d833ca39784d626d43e`
- Fixture tree in both arms: `c125c627c9ea60b50ddf322b0f2d2320ea17359c`
- Task: `task-bounded-context`
- Model/reasoning: `gpt-5.6-terra` / `medium`
- Token budget: 50,000 cache-excluded input plus output, diagnostic rather than a pass criterion
- Permission profile: `preauthorized-write-build-v1`
- Evaluator registry SHA-256: `7b4425580a6b96fae3edb94274b9a605188fe75c86bc0cc36f8f13dfe01846d9`
- Harness SHA-256 at start: `e6c0a9898699ed7359985f1a73740bb0f46803887eeed5f0250dbeb7b0050a08`
- Manifest SHA-256: `845b9fb5330532d429b63c5e4ec16e0b87ee998235fbf95157fa04df403cbe5c`
- Codex host: `codex-cli 0.155.0-alpha.9.2`
- Pair preparation started: `2026-09-21T21:47:57.8873605Z`

The arms were prepared together and run back-to-back. The ArifCE settings were not changed after observing the baseline result.

## Why the pair is ineligible

The CLI project declares `<AssemblyName>arifce</AssemblyName>`, so its framework-dependent output is `arifce.dll`. The treatment prompt instead instructed the agent to invoke `ArifCE.Cli.dll`. That first command failed. The agent then inspected build output and project configuration to discover the real assembly. The visible outputs from that failed lookup sequence alone are approximately 4,690 estimated tokens; the resulting model replay and reasoning amplification cannot be isolated from host telemetry.

Two parser defects then hid the completed workflow:

1. workflow validation recognized `ArifCE.Cli.dll` but not the real `arifce.dll`;
2. context policy treated the words `dotnet test` inside task-contract text, an `rg` search, and `arifce verify --command` as direct unbounded test execution.

The original immutable result therefore reports `arifceWorkflowPassed=false`, `UNBOUNDED_BUILD_TEST_OUTPUT`, and `comparisonEligible=false`. The raw report is retained unchanged. Running the corrected parser post-hoc over the immutable agent log finds 14 successful ArifCE command events, all nine required workflow stages, no context-policy violation, and 10,224 estimated ArifCE command-output tokens. Those post-hoc values diagnose the parser only; they do not repair the unfair prompt or make the pair eligible.

## Remediation and next gate

The harness now uses the actual `arifce.dll` path, accepts both historical and current assembly names when reading older logs, counts current CLI output as ArifCE overhead, distinguishes an `arifce verify` argument from direct `dotnet test` execution, and retains direct-build rejection at shell command boundaries. Context-efficiency, trial-isolation, and completion/provenance regression tests pass.

Do not run another metered pair in the already-used window. The next pair must begin after a clean five-hour reset, use the corrected prompt from the outset, and retain the same model, reasoning, task, fixture, contracts, evaluator, order, and permission profile. Only if both arms pass every gate may their token difference be reported as a successful-task comparison.
