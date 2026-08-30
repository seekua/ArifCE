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
- [Verification](concepts/verification.md)
- [Continuity](concepts/continuity.md)
- [Handoffs](concepts/handoffs.md)
- [Comparison](COMPARISON.md)
- [Product evidence](EVIDENCE.md)
- [NativeAOT distribution plan](release/native-aot-distribution.md)
- [Semantic embeddings plan](release/semantic-embeddings-plan.md)
- [Incremental index plan](release/incremental-index-plan.md)
- [Agent hooks plan](release/agent-hooks-plan.md)
- [Refactor campaigns](concepts/refactor-campaigns.md)

## Build and integrate

- [Architecture overview](architecture/overview.md)
- [Domain model](architecture/domain-model.md)
- [Verification pipeline](architecture/verification-pipeline.md)
- [Storage](architecture/storage.md)
- [Local project workspace](architecture/local-project-workspace.md)
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
