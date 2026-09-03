# Cross-process storage and disposable-index evaluator

## Scope and source

Phase 70 replaces the canonical-concurrency evaluator's single-process Task.WhenAll check with two independently injected assertions. The source, including its child-process worker and synchronization helpers, is pinned to `2850fd56eaa6bfab84ee4f8418c52679a371e3d9`. Production storage code and canonical schemas are unchanged.

The public task contract now discloses the required APIs, process exclusion, successful write cleanup, and index reconstruction checks equally to both benchmark arms. Historical results are not rescored with this new registry.

## Tested behavior

- Three `dotnet vstest` child hosts run the pinned worker entrypoint. Their actual host PIDs must be distinct from each other and the parent. One process holds an update callback while the others attempt entry; after release, all three evidence links must survive.
- The processes create thirty task records. All must deserialize, have distinct titles and IDs consistent with their filenames, and leave no reservation/temporary files after success.
- A real Git fixture contains a task, decision, failed attempt, claim and linked evidence. Each has a known retrieval marker that must be found **before** deletion, preventing an empty-before/empty-after false pass.
- The entire disposable `.arifce/index` directory is deleted. A new IndexStore must rebuild identical ordered search paths, snippets and scores. SHA-256 snapshots of every non-index/non-cache file under `.arifce`, including the JSONL journal, must remain unchanged. Claim links and failed-attempt status are reloaded afterward.

The normal suite has 88 passing test records locally, including a worker-host entrypoint that performs no standalone assertions when not launched by its parent. The new coverage is two substantive parent tests, not three independent product scenarios. No model or external provider is invoked.

## Calibration procedure

Run `./scripts/test-engineering-benchmark-storage-calibration.ps1 -SourceCommit <commit>`. The script exports that commit to an isolated temporary directory and transforms only the pinned fixture's namespace/class. Each mutation applies exclusively to this copy. Unique anchors are required; executed TRX assertions are scored separately from compilation/runner errors.

Controls are: current code (must pass); remove the update lock; discard the updater callback; omit failed attempts from rebuilding search; and rewrite canonical claim bytes during index rebuild (must all fail). Passing controls are removed after the complete run; failed calibration artifacts remain for diagnosis. These controls are finite calibration, not an effectiveness benchmark.

## Verification status

Local product suite: 88/88. Manifest, registry rejection, assessment and secret-scan checks pass. The independent completion integration passes using the actual pinned evaluator source in an isolated candidate checkout.

All five Windows calibration controls at `2850fd56eaa6bfab84ee4f8418c52679a371e3d9` produced the required outcome:

| Control | Observed result |
| --- | --- |
| Current code | PASSED |
| Unlocked update | FAILED: competing callback entered while A held its update |
| Discard updater callback | FAILED: required worker callback was never entered |
| Omit failed attempts from search | FAILED: expected attempt retrieval missing |
| Rewrite canonical bytes during rebuild | FAILED: canonical hash snapshot changed |

Every mutant result came from an executed failing assertion, not a compiler/restore error. Successful calibration copies were removed.

## Remote proof

Commit `4fbf61b96778faac85409e9832ae7331a3b4ace1` passed [GitHub Actions run 33725187026](https://github.com/seekua/ArifCE/actions/runs/33725187026). All eight jobs succeeded: three OS build/test/package jobs and five self-contained binary targets. The test logs explicitly report 88/88 on Windows, macOS and Ubuntu. Ubuntu additionally passed independent completion integration and all five storage calibration controls against that commit, retaining the same fixture source as the pinned `2850fd5` revision. Phase 70 is closed against this implementation commit; the evaluator registry remains pinned to the original fixture commit.

## Limits and next work

### Follow-up failure: stale ID reservation scan

The later documentation closure commit `f6667a4` failed Windows [CI run 33735216710](https://github.com/seekua/ArifCE/actions/runs/33735216710): all worker processes exited, but the assertion found only 29 of 30 tasks. The earlier successful run is historical evidence, not proof that this race cannot occur. FINDING-0007 and ATTEMPT-0013 record the discovered data-loss defect under the Phase 71 follow-up.

NextId could calculate an old maximum, then reserve an ID whose previous writer had already committed its canonical file and removed the reservation. The fix checks the canonical target again while owning the new reservation, releasing it and advancing if the ID is already used. The assertion is retained unchanged. Calibration now also forces the scanned maximum to zero: corrected code must still pass, while removing the target recheck must fail. This scheduling control is injected only into temporary exported sources, not through a production test hook. Follow-up proof is tracked in the freshness remediation report.

The exclusion observation uses a bounded wait and is not a scheduler-independent proof; content assertions separately check successful completion. This does not establish crash/power-loss durability, killed-writer recovery, distributed/network-filesystem locking, concurrent index rebuild/read correctness, every canonical entity, or schema migrations. DATABASE_MIGRATION remains an index-rebuild proxy rather than an actual SQL schema migration task.

FINDING-0005 remains open. Seven other evaluator objectives still need strengthening/calibration, beginning with repository freshness. No product speed, token saving or effectiveness claim follows from these synthetic controls; `productClaimEligible=false` remains unchanged. A fresh, permission-matched, repeated A/B study is still required.
