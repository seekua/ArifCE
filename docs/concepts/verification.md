# Verification

Verification starts with deterministic checks. Build, test, Git, and search evidence is preferred to semantic opinion. Risk policy then determines whether independent review or human approval is also required.

Blind review separates independent inspection from reconciliation with the builder claim. Reviewer agreement remains evidence, not truth. Critical work cannot be verified without recorded human approval.

Freshness propagates through the trust lifecycle. When explicitly scoped dependencies no longer match their recorded digests—or, for legacy/unscoped evidence, when the repository snapshot changes—`arifce trust refresh` marks the claim `STALE` and any linked accepted record `NEEDS_REVIEW`. A stale claim must be re-verified; the refresh never silently promotes stale knowledge back to current. Handoffs include the resulting warnings.

Repository-wide snapshots enumerate individual untracked files and read NUL-separated Git status paths, preserving literal Unicode, whitespace and rename endpoints. Deleted files contribute an explicit missing marker. Internal `.arifce/` metadata is excluded so recording knowledge does not invalidate itself. Ignored files remain outside this Git-visible scope; explicitly scoped evidence has its own dependency hashing rules. Git-reported directories such as unexpanded embedded repositories/submodules stop snapshot capture with an error rather than being treated as unchanged missing files. Full submodule content tracking is not implemented.

Existing canonical snapshots are not rewritten. Re-verification is required if the corrected handling of previously collapsed/quoted paths changes their digest. This is conservative invalidation, not loss of canonical evidence.

Evidence may also opt into a change contract with `verify --contract`. ArifCE then captures the exact target declaration/file closure and reverse transitive `PROJECT_REFERENCE` dependents, plus a digest of that closure. Only `EXACT` and `STRUCTURAL` graph relationships participate. Identifier-based `HEURISTIC` callers and related-test candidates remain review hints and cannot invalidate evidence automatically.

Re-verification appends a new immutable evidence record. If that new evidence is current, later refreshes evaluate it without requiring older historical evidence to become current again. Existing acceptances remain `NEEDS_REVIEW`; a new acceptance is required for the new trust state.
