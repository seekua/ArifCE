# UI, IDE, and A2A boundaries

V0.3 defines integration boundaries without shipping a cloud service or autonomous orchestration.

## UI and IDE

Future interfaces must call the same application services as the CLI and MCP adapter. They may render status, tasks, decisions, evidence, findings, checkpoints, handoffs, and refactor campaigns. They must not maintain a parallel project memory or write derived SQLite records as authority.

## A2A and multi-worktree

Future coordination may exchange task identity, owner, path scope, Git snapshot, checkpoint, and safe-point metadata. Creating worktrees, assigning agents, merging changes, and rollback execution remain explicit operations requiring policy and human approval. No autonomous swarm is implied by the metadata model.
