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

Every mutant result came from an executed failing assertion, not a compiler/restore error. Successful calibration copies were removed. Remote matrix proof remains pending; Phase 70 is not closed by local results alone.

## Limits and next work

The exclusion observation uses a bounded wait and is not a scheduler-independent proof; content assertions separately check successful completion. This does not establish crash/power-loss durability, killed-writer recovery, distributed/network-filesystem locking, concurrent index rebuild/read correctness, every canonical entity, or schema migrations. DATABASE_MIGRATION remains an index-rebuild proxy rather than an actual SQL schema migration task.

FINDING-0005 remains open. Seven other evaluator objectives still need strengthening/calibration, beginning with repository freshness. No product speed, token saving or effectiveness claim follows from these synthetic controls; `productClaimEligible=false` remains unchanged. A fresh, permission-matched, repeated A/B study is still required.
