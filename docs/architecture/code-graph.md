# Deterministic code graph

ArifCE builds a disposable structural graph at `.arifce/index/code-graph.json`. Canonical project intelligence never depends on this file; deleting it and running `arifce codegraph build` recreates it from repository source and project files.

The first implementation deliberately uses the .NET base class library and adds no parser or embedding dependency. It records C# files, test files, type and method declarations, projects, project references, symbol references, and related-test candidates.

Every relationship carries a confidence label:

- `EXACT`: filesystem or MSBuild project-reference structure.
- `STRUCTURAL`: deterministic C# declaration scanning.
- `HEURISTIC`: identifier occurrence that may indicate a reference or related test.

Heuristic edges are impact candidates, not proof. They must not independently verify a claim or accept a change. Future language-specific adapters may replace heuristic edges with parser-backed caller, callee, symbol-reference, and test-discovery relationships while preserving the derived graph contract.

Use `arifce codegraph query <symbol>` to inspect matching declarations and their incoming or outgoing relationships. The output remains explainable and names its confidence level.
