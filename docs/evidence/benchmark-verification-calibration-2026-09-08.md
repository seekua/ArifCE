# Unfinished-verification evaluator calibration

TASK-0027 closes the tenth local evaluator remediation objective in FINDING-0005. A real false-verified defect was fixed: `dotnet test --help` produced a `TEST_RUN` kind without a parsed result summary and could therefore verify a low-risk claim. Missing build/test summaries now produce `UNVERIFIED_COMMAND` evidence and at most `SUPPORTED` trust.

The pinned evaluator source is `5a1d7254e2390937ccddf43012aa1252e8e807d6`. It reloads persisted claims/evidence and verifies that a named help command is not VERIFIED; secret or unapproved unsafe commands leave canonical bytes unchanged; and an approved unsafe success remains `UNSAFE_COMMAND`/`SUPPORTED` after reload.

`./scripts/test-engineering-benchmark-verification-calibration.ps1 -SourceCommit <commit>` passes good code and rejects four executed mutants: treating help output as tests, promoting unsafe success to VERIFIED, executing a secret-bearing command, and accepting unsafe execution without approval. All 113 local product tests, registry rejection and independent completion provenance controls pass.

Published commit `dadddc3cdd2b477f5e804ff9043b231bce3d01fb` passed the three-OS quality matrix, Linux verification controls, and five self-contained package smoke checks in [GitHub Actions run 34215866974](https://github.com/seekua/ArifCE/actions/runs/34215866974). TASK-0027 is completed.

This does not prove every localization/output format, process-crash recovery, all named-command semantics or multi-user authorization. It establishes the tested false-trust boundary only. All ten evaluator objectives are now remotely calibrated; a fresh permission-matched repeated model study remains required. No product-effectiveness claim is established.
