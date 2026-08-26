# MCP Boundary

An MCP server is planned, not implemented in V0.1. The filesystem and `arifce` CLI are the complete operational boundary today.

Future MCP tools may expose status, context, search, checkpoint, handoff, claim, verification, refactor status, and provenance lookup. They must call the same application/domain behavior rather than creating a second canonical store.

MCP implementation is deferred until CLI and schema compatibility stabilize. No current documentation should imply that an MCP server can be started or that MCP authentication is configured.
