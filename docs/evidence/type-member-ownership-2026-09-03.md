# C# type-member ownership evidence

## Scope

Phase 64 adds structural `CONTAINS` edges from each declared C# type to its directly nested types, methods, and constructors. Its initial generator version 5 is superseded by version 6, which also corrects same-line declaration identities and invocation ownership. Earlier derived documents rebuild automatically.

## Guarantees

- Parentage comes from the Roslyn syntax tree rather than name matching.
- Nested types point to their lexical parent type.
- Methods and constructors point to their declaring type.
- Every `CONTAINS` edge has `STRUCTURAL` confidence.
- Source-position IDs preserve separate declarations even when names and signatures repeat on the same line. Calls bind to their containing declaration's span rather than the first node on its line.
- The relationship is graph structure only; it does not assert a semantic dependency and does not widen trusted evidence paths beyond the source file already represented by declaration structure.

## Proven check

`Code_graph_records_exact_type_member_ownership_for_nested_declarations` proves ownership edges for a type, its constructor, method, nested type, and nested method.

Two additional regression tests first failed against the initial implementation: four same-line type declarations collapsed to two nodes, and two invocations were attributed to one wrong method. `Code_graph_preserves_same_line_declarations_with_distinct_lexical_owners` and `Code_graph_calls_belong_to_the_exact_same_line_method_or_constructor` pass after the fix. The complete local suite passes 82 tests. The failed approach is recorded in `ATTEMPT-0009`.

The packaged CLI smoke fixture now places an idle method and a caller on the same line and checks the source of the emitted call and its owning type.

Initial commit `5f6772e` passed [CI run 33687064063](https://github.com/seekua/ArifCE/actions/runs/33687064063). This result predates the two adversarial regressions. Correction `297a6a9` passed [CI run 33687771905](https://github.com/seekua/ArifCE/actions/runs/33687771905): Windows, Ubuntu, and macOS build/test/package jobs and all five self-contained binary targets succeeded. Phase 64 is closed against this corrected implementation.
