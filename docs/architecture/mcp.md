# MCP Boundary (V0.3)

V0.3 introduces an optional local-first MCP adapter. The CLI and filesystem remain complete without it; MCP is an additional protocol boundary for coding agents.

The server will use stdio JSON-RPC transport and resolve the project root from its configured working directory. It will call the same application services as the CLI and will never create a second canonical store. Canonical Markdown/JSON/JSONL under `.arifce/` remains authoritative; SQLite remains derived and rebuildable.

Initial tools are intentionally narrow: `arifce_status`, `arifce_search`, `arifce_checkpoint`, `arifce_handoff`, `arifce_refactor_status`, and `arifce_refactor_verify`. Shell-backed verification and refactor mutation tools require explicit follow-up acceptance criteria because they can execute commands or change lifecycle state.

Safety boundaries:

- no network access, cloud account, or vendor credential is required;
- tool arguments are validated before dispatch and unknown tools fail closed;
- command execution is not exposed through the initial MCP surface;
- malformed requests produce JSON-RPC errors without mutating project state;
- the adapter reports capabilities honestly and does not imply external reviewer invocation.
