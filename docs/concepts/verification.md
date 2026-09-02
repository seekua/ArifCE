# Verification

Verification starts with deterministic checks. Build, test, Git, and search evidence is preferred to semantic opinion. Risk policy then determines whether independent review or human approval is also required.

Blind review separates independent inspection from reconciliation with the builder claim. Reviewer agreement remains evidence, not truth. Critical work cannot be verified without recorded human approval.

Freshness propagates through the trust lifecycle. When explicitly scoped dependencies no longer match their recorded digests—or, for legacy/unscoped evidence, when the repository snapshot changes—`arifce trust refresh` marks the claim `STALE` and any linked accepted record `NEEDS_REVIEW`. A stale claim must be re-verified; the refresh never silently promotes stale knowledge back to current. Handoffs include the resulting warnings.

Evidence may also opt into a change contract with `verify --contract`. ArifCE then captures the exact target declaration/file closure and reverse transitive `PROJECT_REFERENCE` dependents, plus a digest of that closure. Only `EXACT` and `STRUCTURAL` graph relationships participate. Identifier-based `HEURISTIC` callers and related-test candidates remain review hints and cannot invalidate evidence automatically.

Re-verification appends a new immutable evidence record. If that new evidence is current, later refreshes evaluate it without requiring older historical evidence to become current again. Existing acceptances remain `NEEDS_REVIEW`; a new acceptance is required for the new trust state.
