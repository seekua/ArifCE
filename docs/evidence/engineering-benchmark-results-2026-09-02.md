# Engineering benchmark: first complete matched run

Date: 2026-09-02  
Status: **complete execution; inconclusive product comparison**

## Executive result

The first complete engineering benchmark ran all ten task pairs and retained all twenty trials. The independent evaluators passed three baseline candidates and four ArifCE-assisted candidates.

That raw `3/10` versus `4/10` count is **not evidence that ArifCE improves agent performance**. The only differing pass was `canonical-concurrency`, where the baseline agent's attempted patch was denied by the execution environment while the ArifCE arm was allowed to write. Three other ArifCE arms were also denied a patch. The write-permission asymmetry is a material confound, so no comparative effect size, percentage improvement, or product claim is reported.

The useful outcome of this run is diagnostic: it proves the end-to-end benchmark pipeline can preserve negative results and it identifies tasks, evaluator expectations, and execution controls that need improvement before a credible effectiveness study.

## Run identity

| Field | Value |
| --- | --- |
| Fixture commit | `be05904` |
| Model label | `gpt-5.6-sol/current-session` |
| Nominal budget | 50,000 tokens per trial |
| Tasks | 10 matched pairs |
| Trials | 20 |
| Baseline independent passes | 3 |
| ArifCE independent passes | 4 |
| Token source | unavailable |
| Evaluator registry SHA-256 | `6501ec37ab71b57297af36e4c2b62a4a65986a71c8cc08858ce1ac6eef62e108` |

Both arms used history-free repositories exported from the same pinned tree. Each repository had one synthetic initial commit, no remote, and a neutral `AGENTS.md`. The baseline arm was prohibited from reading `.arifce`; the assisted arm received the repository-owned ArifCE context. Candidate commits, trees, patches, prompts, logs, evaluator sources, evaluator projects, and evaluator outputs were hash-bound before collection.

## Task outcomes

`Repository checks` means the candidate's full solution check completed successfully. `Independent` means the withheld, task-specific evaluator passed. These states are intentionally separate.

| Task | Baseline candidate | Baseline repository checks | Baseline independent | ArifCE candidate | ArifCE repository checks | ArifCE independent | Interpretation |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `trust-dirty-content` | changed | fail | pass | changed | fail | pass | Both satisfied the targeted evaluator; full-suite failures prevent a clean repository result. |
| `acceptance-risk-policy` | changed | pass | pass | changed | pass | pass | Matched success. |
| `llm-secret-boundary` | changed | pass | pass | changed | pass | pass | Matched success. |
| `canonical-concurrency` | no change | fail | fail | changed | pass | pass | Invalid as comparative evidence: the baseline patch was denied by the execution environment. |
| `stale-propagation` | changed | pass | fail | changed | pass | fail | Both candidates missed the evaluator's required API/behavior. |
| `deterministic-code-graph` | changed | pass | fail | changed | pass | fail | Both candidates missed the evaluator's required graph contract. |
| `change-impact-contract` | changed | pass | fail | no change | fail | fail | Baseline missed required contracts; ArifCE patch was denied. |
| `structured-flight-recorder` | changed | fail | fail | no change | fail | fail | Baseline implemented a different shape; ArifCE patch was denied. |
| `mcp-validation` | changed | pass | fail | no change | fail | fail | Baseline did not satisfy the withheld boundary tests; ArifCE patch was denied. |
| `unfinished-verification-policy` | changed | pass | fail | changed | fail | fail | Neither candidate exposed the evaluator's required verification policy contract. |

## What this run proves

- The suite can collect a complete matched set without dropping failures or unchanged candidates.
- Independent evaluator outcomes are derived from pinned test sources that candidates do not receive during implementation.
- Collection revalidates the fixture, model label, budget, candidate provenance, evaluator registry, injected evaluator source, project, and output hashes.
- Existing repository checks and task-specific correctness can disagree; the report preserves both instead of presenting either as a universal success flag.

## What this run does not prove

- It does not prove that ArifCE improves correctness, speed, token use, handoff recovery, or context reconstruction.
- It does not provide a trustworthy comparison of elapsed task time. Trial duration includes queueing, interruptions, and approval delays rather than only active agent work.
- It does not provide token consumption. The host did not expose provider-reported token counts, and the harness correctly recorded the source as `unavailable` instead of estimating it.
- It does not isolate the ArifCE treatment from filesystem-approval variance. Four trials produced no candidate after a denied patch: baseline `canonical-concurrency`, and ArifCE `change-impact-contract`, `structured-flight-recorder`, and `mcp-validation`.
- It is one run per arm and only ten matched tasks. The evidence policy requires at least twenty matched tasks before any aggregate product claim.
- It does not measure irrelevant reads, repeated investigation, or repeated failed approaches with a normalized machine-derived metric. Agent logs contain narrative observations, but those are not comparable measurements.

## Negative findings

1. The runner must provide identical, pre-authorized write and build permissions to every arm. An approval decision cannot be allowed to decide a benchmark task.
2. Token and active-work telemetry must come from the execution host. Zero with `tokenSource: unavailable` must never be interpreted as zero consumption.
3. The deterministic code graph, stale propagation, change contract, flight recorder, MCP validation, and verification-policy tasks all exposed gaps between candidate interpretations and the pinned evaluators.
4. A passing full repository check did not predict independent task success in five baseline trials and two ArifCE trials. Task correctness must continue to use withheld evaluators.
5. The next study needs repeated seeds and at least twenty matched tasks before reporting an aggregate effect.

## Next experiment requirements

- Run every trial in the same pre-authorized writable sandbox with dependencies restored before timing begins.
- Capture provider-reported input/output tokens and active execution time, excluding queue and approval delay.
- Record normalized file reads, repeated reads, failed approaches, verification attempts, and clarification requests from structured host events.
- Repeat each task with multiple fresh agent sessions and randomize arm order.
- Keep the current provenance and withheld-evaluator controls.
- Publish negative, null, and favorable results together.

## Evidence location

The normalized suite result and raw per-trial artifacts are retained locally under `artifacts/engineering-benchmark/`. They are intentionally not committed because logs and full trial checkouts are large and may contain environment-specific data. The public repository contains the reproducible manifests, evaluator registry, runner, collector, validation scripts, and this result summary. A future public raw-data release requires a deliberate redaction and packaging step.
