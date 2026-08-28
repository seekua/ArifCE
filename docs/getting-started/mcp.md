# Local MCP server

ArifCE 0.3 includes an optional stdio MCP server. It is local-only: no cloud account, network access, or vendor credential is required.

Build and start it from the repository root:

```bash
dotnet run --project src/ArifCE.Mcp/ArifCE.Mcp.csproj
```

Configure a coding agent to launch the same command with the project repository as its working directory. If the agent launches from another directory, set `ARIFCE_PROJECT_ROOT` to the repository path.

V0.4 also provides the optional local dashboard tool:

```bash
dotnet tool install --global ArifCE.Dashboard --version 0.5.0
arifce-dashboard
```

The server advertises six initial tools: `arifce_status`, `arifce_search`, `arifce_checkpoint`, `arifce_handoff`, `arifce_refactor_status`, and `arifce_refactor_verify`. They operate on the same canonical `.arifce/` files used by the CLI. Shell command execution, external review invocation, and cloud synchronization are intentionally not exposed.
