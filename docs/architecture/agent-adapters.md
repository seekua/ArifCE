# Agent Adapters

Canonical knowledge stays in `.arifce/`. `AGENTS.md`, `CLAUDE.md`, and `opencode.json` are concise routers that point compatible agents to `PROTOCOL.md` and `CURRENT.md`.

Adapters instruct agents to retrieve relevant memory, avoid bulk-reading `.arifce/raw/`, record meaningful decisions and failed attempts, and treat completion statements as claims. They do not duplicate the project memory or contain credentials.

The semantic-review boundary is `ISemanticReviewAdapter`. V0.1 defines the typed boundary and blind-review protocol but provides no external vendor invocation. Filesystem and CLI operation do not depend on any coding-agent SDK.
