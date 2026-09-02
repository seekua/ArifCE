# Roslyn code-graph evidence

**Status:** implemented and verified locally and remotely.

The disposable C# code graph uses `Microsoft.CodeAnalysis.CSharp` for declaration parsing. Constructors, overload-specific graph IDs, interface declarations, and explicit-interface methods are structural nodes. Phase 62 generator version 3 rebuilt old regex-derived graph caches automatically; the current generator version is 4 after Phase 63 added syntax call candidates. Heuristic reference edges remain outside trusted contract closure.

[GitHub Actions run 33668317643](https://github.com/seekua/ArifCE/actions/runs/33668317643) passed Windows, Ubuntu, macOS, and all five self-contained CLI targets.
