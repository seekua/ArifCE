# Context Retrieval

Context assembly is deterministic before optional provider invocation. `LlmContextComposer` updates the disposable SQLite projection, extracts up to sixteen distinct alphanumeric task terms, and obtains at most fifty FTS5 candidates. Raw transcripts remain excluded from the index.

Candidates then pass through a shared assembly pipeline:

```text
lexical candidates
→ record-type priority
→ trust-state inspection
→ stable ordering
→ rendered token estimate
→ budget selection
→ context plus explanation telemetry
```

`CURRENT.md`, active change contracts, known attempts, tasks, claims, decisions, refactors, findings, and evidence receive explicit deterministic priorities. FTS score and path provide stable tie-breaking. This ranking is a documented heuristic, not proof of relevance.

Typed canonical records receive a trust projection before selection. Stale evidence and claims, superseded decisions, non-current acceptances, and malformed records are rejected. Unverified or disputed claims may remain in context only with their state rendered as a warning. Supported claims are rejected when their evidence is missing, malformed, or no longer matches the current repository snapshot.

Every included block renders its path, kind, freshness, and inclusion reason. Every rejected candidate remains in the assembly result with a budget or trust reason. Telemetry reports candidate/selected/rejected records and tokens, plus stale, superseded, invalid, and budget rejection counts. `arifce context explain` exposes this projection; CLI LLM context and MCP use the same composer.

Token cost is estimated as `ceil(rendered characters / 4)` and includes the metadata supplied to the agent. It is conservative but not tokenizer-exact. The selected estimate does not exceed the requested positive budget.

Current freshness remains repository-snapshot scoped. File/symbol dependency invalidation and conflict detection are separate future work and must not be inferred from this pipeline. No token-reduction or effectiveness claim follows from candidate reduction alone.
