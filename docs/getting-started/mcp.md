# Local MCP server

ArifCE 0.3 includes an optional stdio MCP server. It is local-only: no cloud account, network access, or vendor credential is required.

Build and start it from the repository root:

```bash
dotnet run --project src/ArifCE.Mcp/ArifCE.Mcp.csproj
```

Configure a coding agent to launch the same command with the project repository as its working directory. If the agent launches from another directory, set `ARIFCE_PROJECT_ROOT` to the repository path.

The server advertises four initial tools: `arifce_status`, `arifce_search`, `arifce_checkpoint`, and `arifce_handoff`. They operate on the same canonical `.arifce/` files used by the CLI. Command execution, external review invocation, and cloud synchronization are intentionally not exposed.
