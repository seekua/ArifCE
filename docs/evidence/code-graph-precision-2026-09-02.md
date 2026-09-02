# Deterministic Code-Graph Precision Audit — 2026-09-02

This is a bounded regression audit, not a global precision claim. The same ArifCE working tree was scanned with the Phase 55 and Phase 56 method-declaration rules. A targeted list covered observed invocation/lambda/pattern false-positive names: `async`, `is`, `or`, `and`, `Where`, `Select`, `Count`, `MapGet`, `WriteAsync`, `Read`, and `Equals`.

| Scanner | Method/test nodes | Target-list candidates | Confirmed false positives |
|---|---:|---:|---:|
| Phase 55 | 435 | 52 | 43 |
| Phase 56 | 342 | 9 | 0 |

The nine remaining Phase 56 candidates were inspected at their recorded source lines and are real declarations named `Read`, `Select`, `Count`, or `WriteAsync`. Regression fixtures separately require exact output for ordinary methods and an attributed test method while rejecting invocation chains, an async lambda, and pattern keywords.

The audit does not establish recall or compiler-level accuracy. Constructors, operators, tuple-return signatures, explicit-interface methods, overload identity, caller/callee resolution, and non-C# languages remain incomplete. For that reason ArifCE continues to label identifier relationships `HEURISTIC` and does not use them as automatic verification or dependency-closure proof.
