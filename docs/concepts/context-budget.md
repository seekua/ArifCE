# Context Budget

An explicit budget limits the estimated context selected for a task:

```bash
arifce context "finish cache migration" --budget 6000
arifce context explain "finish cache migration" --budget 6000
```

ArifCE reports candidate, selected, rejected, and token totals. Explain mode reports every candidate's kind, priority, freshness, score, estimated cost, disposition, and reason. Budget overflow rejects lower-ranked candidates without hiding them from diagnostics. Trust-rejected records do not consume the selected budget.

The estimate includes rendered source metadata and content and uses character count rather than a provider tokenizer. A smaller estimate is useful only when task performance remains equivalent or better; the benchmark does not yet prove that outcome.
