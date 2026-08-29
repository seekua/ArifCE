# Current State

## Objective

Monitor the published V0.6.0 acceptance lifecycle release and begin the next owner-approved product objective.

## Status

V0.1 through V0.6.0 are published GitHub Releases. V0.6.0 adds a separate, auditable claim acceptance lifecycle over current evidence and blocking-finding safety gates.

## Blockers

No release blocker remains. Cloud hosting remains deferred by product decision. External vendor reviewer invocation, hosted vector stores, and full IDE-native extensions remain deferred.

## Next steps

Phase 40 is implemented in the local dashboard: decision-maker summaries show latest agent/action, evidence freshness, and record/status/agent filters. Journal-backed agent attribution and malformed JSONL tolerance were hardened by the expert review; remote CI passed for commit `a0ffac5`.

Phase 41 is complete: local multi-project workspace active switching now clears stale selections on removal, and isolation tests cover independent registered roots.

Phase 39 is complete: CI now reports canonical README parity separately from human translation review and supports an optional strict `-RequireReviewed` gate. All 21 languages remain explicitly Pending until human review.

Phase 38 is complete under the translator-review agent role: all 21 localized README files passed structural, link, command, diagram, badge, and English-prose checks and are marked `Reviewed (translator agent)`. Human linguistic sign-off remains optional and explicitly distinguished.

The post-V0.7 LLM objective is in progress: local provider adapters (OpenAI, Anthropic, Gemini, OpenRouter, Ollama, LM Studio), local API-key profiles, fallback/task routing, connection tests, token/cost accounting, canonical LLM evidence, dashboard LLM activity, approval policy primitives, benchmark primitives, and deterministic local embedding selection are implemented and pushed through `baba7ff`. Remaining work is to wire richer policy/reviewer workflows, IDE manifest capabilities, expanded MCP tools, multi-project orchestration, and full end-to-end CI/release evidence.
