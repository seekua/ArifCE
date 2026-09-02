# Deterministic code graph

ArifCE builds a disposable structural graph at `.arifce/index/code-graph.json`. Canonical project intelligence never depends on this file; deleting it and running `arifce codegraph build` recreates it from repository source and project files.

The derived document records a SHA-256 digest over normalized `.cs` and `.csproj` paths and contents plus a graph-generator version. Every graph read checks both. Source edits, additions, deletions, renames, and scanner upgrades trigger an automatic atomic rebuild before a query or change contract can consume the graph. Legacy documents without a digest/version and malformed derived JSON are rebuilt rather than trusted. A build compares the source digest before and after scanning and retries a bounded number of times; continuously changing input fails explicitly instead of publishing a mixed snapshot.

The first implementation deliberately uses the .NET base class library and adds no parser or embedding dependency. It records C# files, test files, type and method declarations, projects, project references, symbol references, and related-test candidates.

Every relationship carries a confidence label:

- `EXACT`: filesystem or MSBuild project-reference structure.
- `STRUCTURAL`: deterministic C# declaration scanning.
- `HEURISTIC`: identifier occurrence that may indicate a reference or related test.

Heuristic edges are impact candidates, not proof. They must not independently verify a claim or accept a change. The method scanner requires an ordinary declaration boundary, return type, name, and parameter list; this rejects observed invocation chains, async lambdas, and pattern keywords. It does not claim compiler completeness: constructors, operators, tuple-return signatures, explicit-interface methods, overload identity, and non-C# languages are incomplete. Future language-specific adapters may replace heuristic edges with parser-backed caller, callee, symbol-reference, and test-discovery relationships while preserving the derived graph contract.

`verify --contract` is the only automatic graph-to-evidence expansion. It resolves an exact target, walks declaration/file edges in either direction, and walks reverse `PROJECT_REFERENCE` edges transitively so exact dependent projects participate. It stores a closure digest plus content digests for every resulting path. `REFERENCES` and `RELATED_TEST` edges are never traversed for freshness because they are heuristic.

Use `arifce codegraph query <symbol>` to inspect matching declarations and their incoming or outgoing relationships. When the same name exists in more than one file, use `arifce codegraph query <path>::<symbol>` and pass the same selector to `contract create`, for example `src/Payments/PaymentService.cs::Calculate`. The query output includes each stable graph node ID for inspection. A path-qualified selector keeps trusted closure limited to declarations in that file; it does not yet distinguish overloads on different lines of the same file.

Graph freshness does not make heuristic edges proof. ArifCE does not yet use heuristic graph closure to expand evidence scope or invalidate claims transitively; that requires an explicit policy so false-positive identifier matches cannot silently invalidate accepted knowledge.
