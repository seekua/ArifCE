# Repeatable model-study gate

## Verdict

TASK-0028 is complete. The engineering benchmark harness now prepares ten task categories in two fresh sessions per category and two arms per session: 20 matched trial pairs and 40 isolated agent runs. Repetitions are reported as repetitions, not misrepresented as unique tasks.

## Enforced controls

- Every result carries a trial number and a non-secret permission-profile identifier.
- Baseline and ArifCE arms must use the same fixture tree, model, token budget, trial number, and permission profile.
- The product-study manifest requires at least two repetitions and 20 matched pairs.
- Product-study collection rejects unavailable token telemetry or missing captured host-process timing.
- Existing single-trial manifests remain readable through the earlier schema path.
- Existing trial directories and host captures cannot be overwritten.

## Evidence

Commit `b201762d84b068f0e21ad587848102fe9553847a` passed [GitHub Actions run 34285263692](https://github.com/seekua/ArifCE/actions/runs/34285263692):

- build, test, secret scan, documentation gates, benchmark isolation, telemetry and package smoke checks on Ubuntu, macOS and Windows;
- the new repeated matched-trial and permission-profile smoke on Ubuntu;
- completion provenance and all ten good/bad evaluator calibration suites on Ubuntu;
- self-contained binary smoke checks for Windows x64, Linux x64/ARM64 and macOS x64/ARM64.

## Limits

The permission profile is a hashable study label, not an operating-system sandbox proof. The execution operator must still provision identical permissions before starting either arm. Host timing measures the host process rather than pure model thinking time. Token totals require a supported provider or agent-host event format. No model trial ran in this phase, and no correctness, time, token, or product-effectiveness improvement is claimed.
