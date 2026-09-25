# Terra low core lifecycle matched-pair result

This is a preserved ineligible model comparison, not a token-savings or product-effectiveness claim. Both arms were freshly prepared from the same fixture, public contracts, token budget, and permission profile after the eligible Terra/medium pair. The host processes both exited normally. No retry was performed.

## Exact gate failures

The baseline candidate passed its repository suite and public API gate, but failed the pinned independent behavior evaluator `Explicit_task_links_precede_lexical_results_and_foreign_links_are_rejected`. Its bounded context did not contain the required excluded foreign-link item.

The ArifCE host exited normally but left its checkout dirty: source changes and canonical task, claim, evidence, and journal artifacts were not committed. The completion gate rejects dirty candidate state before repository tests or independent evaluation. This is an honest workflow failure, not an infrastructure failure.

Because neither arm satisfies every gate, neither token total enters successful-task comparison. The raw captures remain local; this document intentionally contains no local paths, prompts, host logs, or account identifiers.

## Fixed conditions

- Task: `task-bounded-context`
- Model/reasoning: `gpt-5.6-terra` / `low`
- Fixture: `d9fee6d137d7355b24c287f6f976b44400d80b38`
- Token budget: 50,000 cache-excluded input plus output, diagnostic rather than an eligibility criterion
- Permission profile: `preauthorized-write-build-v1`
- Baseline ran before ArifCE; both arms were prepared before either host result was inspected.

The Terra/medium eligible result remains the only valid successful-task comparison. Any Luna/medium study must use fresh predeclared matched arms and cannot reinterpret this ineligible run as a model-quality result.
