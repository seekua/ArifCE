# Verification

Verification starts with deterministic checks. Build, test, Git, and search evidence is preferred to semantic opinion. Risk policy then determines whether independent review or human approval is also required.

Blind review separates independent inspection from reconciliation with the builder claim. Reviewer agreement remains evidence, not truth. Critical work cannot be verified without recorded human approval.

Freshness propagates through the trust lifecycle. When repository content no longer matches an evidence snapshot, `arifce trust refresh` marks the claim `STALE` and any linked accepted record `NEEDS_REVIEW`. A stale claim must be re-verified; the refresh never silently promotes stale knowledge back to current. Handoffs include the resulting warnings.
