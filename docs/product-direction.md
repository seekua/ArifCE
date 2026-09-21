# Product direction and feature gate

ArifCE is the repository-native trust and continuity layer for coding agents. The repository owns its engineering context; agents borrow only the context needed for the current task.

The core lifecycle is **task → context → work → claim → verify → handoff**. ArifCE owns task continuity, small engineering contracts, repository-backed facts, evidence freshness, focused context, completion state, and handoffs. It does not own an IDE, agent launcher, terminal, chat transcript warehouse, general memory platform, vector database, cloud collaboration service, or project-management suite. Existing adapters and UI are maintained, but expansion of those areas is not a V1 priority.

Before adding a feature, ask: **Does this help the next agent continue correctly with less rediscovery and more trustworthy repository state?** If the answer is not clearly yes, defer it. Prefer strengthening an existing command or canonical record over introducing a subsystem. Semantic retrieval may suggest candidates, but it must not decide what is true.

## V1 proof obligation

On ordinary repositories, a new agent must be able to read a task contract, receive a bounded and explainable context package, record a claim with evidence, check completion against current repository state, and continue from a compact task handoff. Relevant changes must turn an earlier completion into `NEEDS_REVERIFY`. Benchmark results must establish at least equal task success while measuring rediscovery, context use, and handoff recovery. Until those experiments pass, effectiveness claims remain unproven.

The owner-provided strategic direction is the decision filter for subsequent roadmap changes. This file records its operational boundary without expanding the existing architecture.
