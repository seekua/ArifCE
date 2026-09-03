# ArifCE documentation

ArifCE documentation is organized like a product handbook: start with the goal, follow a guided first run, then go deeper into concepts, architecture, and reference material.

## Start here

1. [Why ArifCE exists](../README.md#why-arifce-exists)
2. [Installation](getting-started/installation.md)
3. [Quick start](getting-started/quick-start.md)
4. [Your first verification](getting-started/first-verification.md)
5. [Your first handoff](getting-started/first-handoff.md)

## Understand the model

- [Project intelligence](concepts/project-intelligence.md)
- [Project memory](concepts/project-memory.md)
- [Claims and evidence](concepts/claims-and-evidence.md)
- [Knowledge conflicts](concepts/knowledge-conflicts.md)
- [Verification](concepts/verification.md)
- [Continuity](concepts/continuity.md)
- [Handoffs](concepts/handoffs.md)
- [Comparison](COMPARISON.md)
- [Product evidence](EVIDENCE.md)
- [Benchmark protocol, V0.7 smoke evidence, and the first inconclusive matched run](research/benchmarks.md)
- [First complete engineering benchmark result](evidence/engineering-benchmark-results-2026-09-02.md)
- [Deterministic code-graph precision audit](evidence/code-graph-precision-2026-09-02.md)
- [Contract-linked dependency closure evidence](evidence/contract-closure-2026-09-02.md)
- [Task-risk parsing integrity evidence](evidence/task-risk-parsing-2026-09-02.md)
- [Path-qualified code-graph target evidence](evidence/path-qualified-code-graph-targets-2026-09-02.md)
- [Fail-closed Git snapshot evidence](evidence/git-snapshot-fail-closed-2026-09-02.md)
- [Heuristic C# call-candidate evidence](evidence/heuristic-call-candidates-2026-09-03.md)
- [C# type-member ownership evidence](evidence/type-member-ownership-2026-09-03.md)
- [Change-contract relationship confidence evidence](evidence/contract-relationship-confidence-2026-09-03.md)
- [Benchmark token telemetry evidence and limits](evidence/benchmark-token-telemetry-2026-09-03.md)
- [Benchmark host timing evidence and limits](evidence/benchmark-host-timing-2026-09-03.md)
- [NativeAOT distribution plan](release/native-aot-distribution.md)
- [Dependency policy](architecture/dependency-policy.md)
- [Binary releases](release/binary-releases.md)
- [V0.8 release checklist](release/v0.8-checklist.md)
- [Semantic embeddings plan](release/semantic-embeddings-plan.md)
- [Incremental index plan](release/incremental-index-plan.md)
- [Agent hooks plan](release/agent-hooks-plan.md)
- [Dashboard asset refactor plan](release/dashboard-assets-plan.md)
- [Refactor campaigns](concepts/refactor-campaigns.md)

## Build and integrate

- [Architecture overview](architecture/overview.md)
- [Domain model](architecture/domain-model.md)
- [Verification pipeline](architecture/verification-pipeline.md)
- [Storage](architecture/storage.md)
- [Local project workspace](architecture/local-project-workspace.md)
- [Deterministic code graph](architecture/code-graph.md)
- [Change Impact Contracts](concepts/change-contracts.md)
- [Agent Flight Recorder](concepts/agent-flight-recorder.md)
- [Context retrieval](architecture/context-retrieval.md)
- [Agent adapters](architecture/agent-adapters.md)
- [MCP integration](getting-started/mcp.md)

## Reference

- [CLI reference](reference/cli.md)
- [Schemas](reference/schemas.md)
- [Entity statuses](reference/entity-statuses.md)
- [Events](reference/events.md)
- [File layout](reference/file-layout.md)
- [Configuration](reference/configuration.md)
- [Documentation policy](DOCUMENTATION-POLICY.md)

## Product principles

ArifCE is local-first, evidence-oriented, and explicit about uncertainty. The repository is the source of truth; the dashboard is a read-only cockpit for understanding that truth. Every user-visible change must update the relevant guide and reference page in the same change.
