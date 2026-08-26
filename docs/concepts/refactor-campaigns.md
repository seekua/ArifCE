# Refactor Campaigns

Large migrations are first-class campaigns with objectives, invariants, remaining inventory, deterministic guards, workstreams, findings, checkpoints, and safe points. Completion is a guarded transition rather than an agent statement.

Workstreams record owners and path scopes but do not launch agents or create worktrees. Safe points capture Git state but do not execute rollback. Inventory and blocking guards must pass before `finish` succeeds.
