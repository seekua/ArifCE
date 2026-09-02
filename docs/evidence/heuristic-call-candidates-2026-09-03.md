# Heuristic C# call-candidate evidence

## Scope

Phase 63 adds parser-backed `CALLS` edges to the disposable C# code graph and advances its generator version to 4 so version-3 graph caches rebuild. An edge begins at the method or constructor containing an invocation and points to declarations with the same called name. It is an investigation aid, not semantic binding.

## Guarantees

- Invocation ownership and called member names are read from the Roslyn syntax tree.
- Every emitted `CALLS` edge is marked `HEURISTIC`.
- `TrustedClosureAsync` traverses only `DECLARES` structural edges and reverse exact `PROJECT_REFERENCE` edges; it never traverses `CALLS`.
- Contract verification and acceptance therefore cannot become fresh, supported, or accepted merely because an invocation candidate exists.

## Proven checks

`Code_graph_emits_parser_backed_call_candidates_without_trusting_them_for_closure` creates a caller and a target, proves the `CALLS`/`HEURISTIC` edge exists, then proves the caller path is absent from the target's trusted closure.

The packaged smoke script creates the same fixture and requires `arifce codegraph query Target` to report `CALLS HEURISTIC`.

## Limits

This layer does not resolve overloads, polymorphism, extension methods, dynamic dispatch, aliases, external assemblies, or compiler symbols. A query result is a candidate for review, never verification proof. Remote CI evidence is appended after the Phase 63 implementation commit is pushed.
