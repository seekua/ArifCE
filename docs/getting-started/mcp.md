# Local MCP server

ArifCE 0.3 includes an optional stdio MCP server. It is local-only: no cloud account, network access, or vendor credential is required.

The server also exposes `arifce_context`, `arifce_llm_providers`, `arifce_llm_run`, and `arifce_llm_review`. The execution and review tools require an explicit `approved: true` argument; review additionally requires a claim, reviewer, and rationale. Successful calls record provider/model/token/cost evidence. API keys are never returned by provider listing.

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

The server exposes read tools plus typed canonical write tools: `arifce_status`, `arifce_search`, `arifce_context`, `arifce_checkpoint`, `arifce_task_create`, `arifce_decision_create`, `arifce_attempt_record`, `arifce_claim_create`, `arifce_finding_create`, `arifce_review_record`, `arifce_acceptance_create`, `arifce_handoff`, and refactor inspection tools. They operate on the same canonical `.arifce/` files used by the CLI. Shell command execution and cloud synchronization are intentionally not exposed; approved LLM execution uses configured local profiles and the same redaction and canonical-domain rules as the CLI.

The MCP boundary enforces a 256 KB request limit, bounded string arguments, strict enum values, numeric budget/limit ranges, known argument names, and repository entity ID syntax. Path-like IDs such as `../../outside` are rejected before storage access. JSON schemas describe the same limits, but server-side validation remains authoritative. Multiple MCP processes rely on the canonical cross-process mutation locks, so an ID check never substitutes for atomic storage behavior.
