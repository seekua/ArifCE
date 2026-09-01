# Agent Flight Recorder

The Agent Flight Recorder stores a bounded engineering narrative rather than a raw conversation transcript. A run records its provider, agent, goal, optional task, repository snapshot, status, and structured steps.

Supported step kinds are `INVESTIGATION`, `ATTEMPT`, `EVIDENCE`, `DECISION`, and `RESULT`. Summaries are redacted and limited before persistence. Prompts, chat transcripts, chain-of-thought, and provider raw responses are not flight-recorder records.

```text
arifce run start "Investigate payment calculation" --provider codex --agent builder --task TASK-0001
arifce run event RUN-0001 --kind INVESTIGATION --summary "Traced the calculation call path"
arifce run event RUN-0001 --kind ATTEMPT --summary "Tried cached totals" --outcome FAILED --exit-code 1
arifce run finish RUN-0001 --summary "Fallback implementation passed"
arifce run status RUN-0001
```

When a failed `ATTEMPT` belongs to a tracked task, ArifCE also creates the normal canonical `AttemptRecord` and links it to the run step. Existing search, context, and handoff behavior can therefore warn a future agent before the same approach is repeated.

Automation must submit a concise structured summary. It must not silently infer a decision, claim, or successful result from raw activity. Provider integrations remain opt-in and use the same canonical domain methods as the CLI.
