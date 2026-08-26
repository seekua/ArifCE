# Design Decisions

Key V0.1 decisions are intentionally conservative:

- Canonical Markdown/JSON/JSONL and Git remain authoritative; SQLite is derived.
- JSON is used for machine records to keep dependencies minimal.
- Retrieval is lexical and explainable; vector storage is deferred.
- Verification starts deterministic; semantic review is policy-driven and vendor-neutral.
- Blind review separates inspection from reconciliation to reduce anchoring.
- Refactor coordination stores workstream metadata but does not orchestrate autonomous agents.
- Repair is explicit and backup-first; diagnosis is read-only.

Project-specific decisions are stored under `.arifce/decisions/`. This research document explains product-level choices and does not invent rationale for adopted repositories.
