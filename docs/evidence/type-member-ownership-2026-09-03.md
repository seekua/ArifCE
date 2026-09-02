# C# type-member ownership evidence

## Scope

Phase 64 adds structural `CONTAINS` edges from each declared C# type to its directly nested types, methods, and constructors. The graph-generator version advances to 5, rebuilding prior derived documents.

## Guarantees

- Parentage comes from the Roslyn syntax tree rather than name matching.
- Nested types point to their lexical parent type.
- Methods and constructors point to their declaring type.
- Every `CONTAINS` edge has `STRUCTURAL` confidence.
- The relationship is graph structure only; it does not assert a semantic dependency and does not widen trusted evidence paths beyond the source file already represented by declaration structure.

## Proven check

`Code_graph_records_exact_type_member_ownership_for_nested_declarations` proves ownership edges for a type, its constructor, method, nested type, and nested method.

Remote CI evidence is appended after the implementation commit is pushed.
