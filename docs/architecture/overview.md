# Architecture Overview

ArifCE has one dependency direction: user interfaces and adapters depend inward on application services and domain types. Canonical storage, Git inspection, indexing, retrieval, verification, redaction, and vendor adapters implement narrow application boundaries.

```text
CLI / future MCP
       |
Application services
       |
Core domain and contracts
       ^
Storage | Git | Search | Verification | Security | Agent adapters
```

Canonical files under `.arifce/` are authoritative. The append-only event journal records operations for timeline and recovery. SQLite provides lookup and FTS5 but is replaceable and rebuilt from canonical material. Commands mutate canonical data first and update the index second; an interrupted index update is repaired by `rebuild`.

Retrieval collects candidates, applies explainable lexical relevance and metadata weights, estimates tokens, then selects within a budget. Verification gathers deterministic evidence before any optional semantic review. Refactor completion is a domain transition guarded by invariant and inventory checks.

The V0.1 solution favors a small number of cohesive assemblies over one project per noun. This is a deliberate deviation from the illustrative seed layout: boundaries with distinct infrastructure dependencies remain separate; closely related pure domain behavior stays together.
