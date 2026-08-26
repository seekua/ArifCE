# Context Budget

An explicit budget limits the estimated context selected for a task:

```bash
arifce context "finish cache migration" --budget 6000
```

ArifCE reports each included source, its estimate, and the inclusion reason. V0.1 estimates tokens from character count; it is not tokenizer-exact. A smaller estimate is useful only when task performance remains equivalent or better.
