# ArifCE Product Seed

This repository was initialized from the owner-provided **ArifCE — Master Bootstrap Prompt** on 2026-08-26. That complete prompt is the founding input for this product and was read before implementation. This checked-in seed captures its normative product contract; `docs/SPECIFICATION-v0.1.md` is the precise, implementation-facing interpretation.

ArifCE is a local-first project intelligence and continuity layer for AI-assisted software development. The repository owns context; agents borrow it. Canonical, human-readable project intelligence travels with Git while credentials and machine-specific authentication never do.

The system separates active state, long-lived knowledge, rationale, history, trust, and work. Meaningful completion statements are claims linked to evidence. Deterministic verification is preferred, evidence is repository-state scoped and can become stale, and agreement between language models is never treated as truth.

V0.1 uses .NET 10, C#, SQLite with FTS5, System.Text.Json, minimal dependencies, and a cross-platform `arifce` CLI. SQLite is derived; canonical Markdown, YAML, JSON, JSONL, and Git remain sufficient to rebuild it. Retrieval is deterministic, explainable, and budgeted without embeddings.

The first release must support safe initialization/adoption, status and diagnostics, rebuild and search, context selection, tasks and checkpoints, claims/evidence and deterministic verification, semantic handoff, provenance lookup, guarded refactor campaigns, concise agent adapters, secret redaction, and corrupt-journal recovery. Exposed commands must be useful and tested.

The release is not complete until a new or existing Git repository can be initialized, used to represent work and evidence, handed to a fresh session with targeted context, and recovered after deleting the derived index. Unsupported requirements must be recorded in `ROADMAP.md`, never implied as complete.

## Source preservation note

The original prompt arrived as an external attachment rather than an existing repository file. This file therefore records the stable product-level requirements while the detailed command, entity, lifecycle, security, test, phase, and acceptance contracts are preserved in the specification. This provenance decision is recorded as ADR-0001 once dogfooding storage exists.
