# Luna medium core lifecycle matched-pair result

This is a preserved ineligible comparison, not a token-savings or product-effectiveness claim. Both arms were freshly prepared from the same fixture, public contracts, token budget, and permission profile. No retry was performed.

The baseline passed repository tests, the public API gate, and the pinned independent evaluator with 83,342 measured primary tokens. The ArifCE host completed its workflow and repository tests with 97,842 measured primary tokens. Before its independent evaluator could be accepted, final provenance verification rejected the ArifCE result with `Token measurement mismatch: churnRatio`.

The mismatch was traced to insignificant trailing-zero formatting of the derived floating-point `churnRatio`, not a disagreement in raw input, cache, or output counters. The verifier now recomputes and rounds that derived value while retaining exact fail-closed checks for every raw counter. After that correction, provenance verification passed and the ArifCE independent evaluator ran.

The final ArifCE evaluator failed `Explicit_task_links_precede_lexical_results_and_foreign_links_are_rejected` because the required foreign-link exclusion content was not present. The pair therefore remains ineligible for a real behavioral reason. Neither arm enters successful-task comparison, and no retry was made.

## Fixed conditions

- Task: `task-bounded-context`
- Model/reasoning: `gpt-5.6-luna` / `medium`
- Fixture: `d9fee6d137d7355b24c287f6f976b44400d80b38`
- Token budget: 50,000 cache-excluded input plus output, diagnostic rather than an eligibility criterion
- Permission profile: `preauthorized-write-build-v1`
- Baseline ran before ArifCE; both arms were prepared before either host result was inspected.

Raw captures remain local because they contain transient runtime material. This record contains no local paths, prompts, host logs, or account identifiers.
