# Repository freshness remediation and evaluator calibration

## Finding and reproduction

Phase 71's new regression tests found a production correctness defect, not merely an evaluator gap. FINDING-0006 records the critical false-CURRENT risk under TASK-0021.

Against the pre-fix implementation at `f6667a4`, three new tests failed on Windows:

- Editing the bytes of `new-code/nested/implementation.txt` left GitSnapshot.Digest unchanged. Default Git status collapsed the directory; the snapshotter hashed a missing-file marker rather than the file's contents. EvidenceEvaluator would therefore still report CURRENT.
- `café-evidence.txt` was reported with Git's quoted byte escapes and treated as a literal Windows path, causing DirectoryNotFoundException.
- Deleting a tracked file caused ResolveLinkTarget to throw FileNotFoundException before the intended missing marker could be used.

The four replacement evaluator tests, their helpers and production fix are pinned at `d040501021a44c82b7c5bbfe643f1c6aa45e88a7`. Reproduce the regression by injecting BenchmarkFreshnessTests.cs from this commit into an isolated export of the pre-fix commit; never overwrite a working repository to reproduce it.

## Implementation

Git status uses porcelain v1 with NUL delimiters and individual untracked-file enumeration. Rename/copy destination and source records are consumed separately. Literal path characters are retained rather than trimmed, split on arrow text, or interpreted as quoted display output. This follows [Git's machine-readable status contract](https://git-scm.com/docs/git-status#_porcelain_format_version_1).

Missing files are handled before resolving file links. Git-reported directories such as embedded repositories fail closed with a directory diagnostic; their contents are not silently represented as missing. Both redirected Git streams are drained. No dependency, canonical schema, CLI command or generic-command approval policy was added or weakened.

Historical snapshots remain unchanged. Affected old snapshots can become stale after this correction and require re-verification. Normal ordinary-file hash framing is retained. ChangedFiles now represents both rename endpoints as literal paths.

## Independent evaluator

The previous evaluator required a legacy unapproved shell command and a historical evidence kind unrelated to dirty-file correctness. It is replaced with a whole pinned fixture selected by fully qualified method names. The same public contract is supplied to both arms.

Coverage includes unchanged clean/dirty CURRENT, edited tracked/untracked bytes STALE, missing-digest UNKNOWN, deletion, staged rename endpoints, branch switch, detached HEAD, internal metadata exclusion, non-repository capture failure, and rejection of unexpanded Git directories. Unicode and spaces are tested everywhere; Unix additionally exercises quotes, embedded newlines, arrow text and edge whitespace.

## Verification status

All 92 local product test records pass. Manifest, registry, assessment, release-policy and secret-scan checks pass. Independent completion integration passes with the actual pinned source. On Windows at `d040501021a44c82b7c5bbfe643f1c6aa45e88a7`, good code passed and all six deliberately wrong variants failed their executed assertions. No compiler or runner error was counted as a caught mutant.

Run `./scripts/test-engineering-benchmark-freshness-calibration.ps1 -SourceCommit <commit>` to export an isolated copy and test these controls: good code; path-only fingerprints; always-CURRENT; always-STALE; ignored untracked files; internal metadata included; and directories treated as missing. The good code must pass and each of the six wrong variants must produce executed assertion failures. Compiler/runner errors are not accepted as mutant detection. Failed calibration copies are retained, successful copies are cleaned up.

## Limits and remaining work

### Additional P0 discovered during CI review

Reviewing the previous phase's later closure CI exposed FINDING-0007: Windows run 33735216710 retained only 29/30 tasks despite successful worker exits. Windows run 33736561125 at `8d17abe` reproduced exactly the same loss before the ID fix. The [storage report](benchmark-storage-calibration-2026-09-03.md) records the stale ID scan/reservation race. Phase 71 therefore also rechecks canonical ID availability after acquiring the reservation and extends storage calibration with a forced-stale-scan positive control plus the same control without the recheck (must fail). ATTEMPT-0013 preserves the failed earlier confidence assumption. Fix `28ea327b3e3f50a3dd7818bb11e516b0a55ec1cd` passes all 92 local tests. Its Windows storage calibration passed both positive controls and rejected all five wrong variants. The prior successful CI must not be used to dismiss the later loss.

## Remote closure

Commit `c7dbdefdaccf959207a1b764706abc61075f1847` passed [CI run 33737427375](https://github.com/seekua/ArifCE/actions/runs/33737427375). All eight jobs succeeded: Windows/macOS/Ubuntu build-test-package and five self-contained binaries. Each OS test log reports 92/92. Ubuntu passed the pinned independent integrations, both positive storage controls and five storage mutants, and the good/six-mutant freshness calibration. FINDING-0006 and FINDING-0007 are resolved against this evidence. TASK-0021 closes Phase 71 and the Phase 70 storage follow-up; FINDING-0005 remains open for the six remaining evaluator objectives.

This is not complete Git correctness. Ignored files, non-UTF8 filenames, all submodule configurations, rebase/merge/shallow-clone matrices, staged-only content changes, parent-symlink/path races, and edits during snapshot capture are not established by this fixture. Explicit dependency scopes use a separate hashing path. Snapshot capture deliberately rejects Git-reported directories instead of claiming recursive submodule support.

FINDING-0005 remains open and productClaimEligible remains false. After this phase, six evaluator objectives still require strengthening/calibration: stale propagation, deterministic code graph, change contracts, flight recorder, MCP validation and unfinished-verification policy. A fresh, permission-matched repeated model benchmark remains separate work. No speed, token saving or effectiveness percentage is established here.
