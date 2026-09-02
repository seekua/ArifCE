# Knowledge Conflicts

Canonical records can be individually valid while collectively inconsistent. `arifce knowledge audit` reads canonical decisions and claims directly, without trusting SQLite, and reports deterministic indicators in four groups:

- duplicate active decisions with normalized matching titles and outcomes;
- conflicting active decisions with matching titles but different outcomes;
- equivalent claim statements with duplicate or opposing trust states;
- malformed records and broken decision supersession links.

The audit is intentionally conservative. It does not infer which conflicting statement is true, delete history, or silently rewrite canonical records. Blocking conflicts make the CLI command fail after printing every issue; duplicates remain warnings.

Normal decision creation is serialized and rejects a normalized title that already belongs to an active decision, whether the new outcome duplicates or conflicts with it. The audit remains necessary for older repositories, imports, manual edits, and merge results that did not pass through the domain service.

Use `arifce decision supersede <old-id> --by <active-id>` only after reviewing provenance. Supersession preserves the old decision, marks it `SUPERSEDED`, and links it to the active replacement. Context assembly rejects redundant duplicate decisions, labels unresolved conflicting records as `CONFLICT`, and handoffs include a Knowledge Warnings section.

Normalization is lexical and deterministic, not semantic AI judgment. Differently worded contradictions may remain undetected; the audit is a guardrail, not proof of global consistency.
