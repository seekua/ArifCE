# Deterministic code graph

ArifCE builds a disposable structural graph at `.arifce/index/code-graph.json`. Canonical project intelligence never depends on this file; deleting it and running `arifce codegraph build` recreates it from repository source and project files.

The derived document records a SHA-256 digest over normalized `.cs` and `.csproj` paths and contents plus a graph-generator version. Every graph read checks both. Source edits, additions, deletions, renames, and scanner upgrades trigger an automatic atomic rebuild before a query or change contract can consume the graph. Legacy documents without a digest/version and malformed derived JSON are rebuilt rather than trusted. A build compares the source digest before and after scanning and retries a bounded number of times; continuously changing input fails explicitly instead of publishing a mixed snapshot.

The C# declaration adapter uses `Microsoft.CodeAnalysis.CSharp` only inside this disposable graph layer. It records C# files, test files, types, methods, constructors, exact lexical type-member containment, projects, project references, symbol references, and related-test candidates. Canonical repository records and trust policy do not depend on Roslyn.

Declaration IDs include the identifier's source offset so identical names and signatures on one line remain separate. Invocation ownership uses the containing declaration's exact syntax span. These are source-snapshot identifiers: edits can change them, and they are not persistent semantic symbol IDs. `CONTAINS` records only direct lexical parentage, including nested types; it is not inheritance or runtime dispatch.

Every relationship carries a confidence label:

- `EXACT`: filesystem or MSBuild project-reference structure.
- `STRUCTURAL`: deterministic C# declaration scanning and lexical `CONTAINS` ownership.
- `HEURISTIC`: identifier occurrence or parser-backed invocation candidate that may indicate a reference, related test, or call.

Heuristic edges are impact candidates, not proof. They must not independently verify a claim or accept a change. The parser-backed declaration adapter recognizes constructors, overload-specific graph IDs, explicit-interface methods, and simple invocation targets. `CALLS` edges identify the declaring method and matching same-name candidates without semantic binding; they do not resolve overloads, dynamic dispatch, extensions, or external symbols. Operators, semantic caller/callee resolution, compiler-bound symbol references, and non-C# languages remain incomplete.

`verify --contract` is the only automatic graph-to-evidence expansion. It resolves an exact target, walks declaration/file edges in either direction, and walks reverse `PROJECT_REFERENCE` edges transitively so exact dependent projects participate. It stores a closure digest plus content digests for every resulting path. `REFERENCES`, `RELATED_TEST`, and `CALLS` edges are never traversed for freshness because they are heuristic.

Use `arifce codegraph query <symbol>` to inspect matching declarations and their incoming or outgoing relationships. When the same name exists in more than one file, use `arifce codegraph query <path>::<symbol>` and pass the same selector to `contract create`, for example `src/Payments/PaymentService.cs::Calculate`. The query output includes each stable graph node ID for inspection. A path-qualified selector keeps trusted closure limited to declarations in that file; it does not yet distinguish overloads on different lines of the same file.

Graph freshness does not make heuristic edges proof. ArifCE does not yet use heuristic graph closure to expand evidence scope or invalidate claims transitively; that requires an explicit policy so false-positive identifier matches cannot silently invalidate accepted knowledge.
