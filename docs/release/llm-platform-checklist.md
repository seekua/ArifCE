# Local LLM platform verification

Status: **implemented and CI-verified** on `master`.

| Capability | Evidence |
| --- | --- |
| OpenAI, Anthropic, Gemini, OpenRouter, Ollama, LM Studio adapters | `LlmProviders.cs` and provider contract tests |
| Local profiles, API-key isolation, connection tests | `LocalLlmSettingsStore`, CLI provider commands, MCP provider listing |
| Routing, fallback, local/cloud selection, token/cost accounting | `LlmRouting.cs`, `LlmRuntimeSelector.cs`, routing tests |
| Canonical LLM evidence and journal events | `LlmOrchestration.cs`, persistence test |
| Explicit reviewer approval and local policy | `LlmReviewerWorkflow.cs`, `LocalPolicy.cs`, approval tests |
| Embedding selection and benchmark metrics | `Embedding.cs`, `LlmBenchmark`, quality tests |
| Dashboard model/token/cost activity | `/api/overview` LLM projection and dashboard activity card |
| MCP and IDE boundaries | `arifce_llm_providers`, `arifce_llm_run`, `integrations/ide/arifce.local.json` |
| Local A2A and multi-project workspace | `LocalA2AOrchestrator`, `WorkspaceRegistry` |
| Remote CI | [Run 33308852224](https://github.com/seekua/ArifCE/actions/runs/33308852224), conclusion `success`, commit `e6afd00` |

Hosted vector databases, vendor notification channels, issue/PR automation, and full IDE-native extensions remain opt-in integrations outside the local platform.
