# Local LLM providers

ArifCE keeps provider configuration on the local machine. API keys are never written to the repository, journal, or canonical evidence summary.

## Supported adapters

- OpenAI and OpenAI-compatible endpoints
- Anthropic
- Google Gemini
- OpenRouter
- Ollama
- LM Studio

Provider profiles are stored at `%LOCALAPPDATA%/ArifCE/llm-providers.json` on Windows (the platform local application-data directory on other systems). Use an environment variable or stdin for secrets:

Profiles are validated before they are saved. IDs and model names are required, costs cannot be negative, and cloud providers require an API key. Local Ollama/LM Studio profiles can omit a key.

```text
arifce llm provider add openai OpenAI gpt-4o-mini --api-key-env OPENAI_API_KEY
arifce llm provider add ollama Ollama llama3 --endpoint http://127.0.0.1:11434
arifce llm provider add lmstudio LmStudio local-model --endpoint http://127.0.0.1:1234/v1
arifce llm provider list
arifce llm provider test ollama
arifce llm provider remove openai
```

## Routing and evidence

`llm run <task> <prompt>` tries enabled profiles in configuration order and records the successful provider, model, token usage, estimated cost, and repository snapshot as a canonical `llm-response` evidence record. The response body is printed to the terminal but is not copied into the journal. Use `--claim` to associate the evidence with a claim.

```text
arifce llm run review "Check the migration for data-loss risk" --claim CLAIM-0007
```

Task routes can prefer a provider while preserving fallback behavior through `LlmTaskRouter`. Local policy evaluation can require human approval, restrict providers, or cap estimated cost before a caller executes a route. `LlmBenchmark` provides deterministic token, latency, cost, and lexical `TokenRecall` measurements. `Passed` means only that at least 80% of expected words were present; it is a smoke-test signal, not semantic quality or an effectiveness claim.

## Boundaries

Vector/embedding providers, hosted vector databases, Slack/Teams notifications, GitHub/GitLab issue automation, full IDE extensions, and cloud hosting remain separate integrations. They are intentionally not implied by the local provider adapters.
