# Contract-Linked Dependency Closure Evidence — 2026-09-02

Phase 57 adds an opt-in freshness scope; it does not change existing evidence implicitly. `arifce verify ... --contract CONTRACT-NNNN` requires the contract's linked claim and records the contract ID, a trusted-closure digest, and content digests for the resolved paths.

The trusted closure is deliberately an under-approximation:

- exact symbol matches are required;
- declaration-to-file `STRUCTURAL` edges are followed in either direction;
- reverse `EXACT` project references are followed transitively;
- `HEURISTIC` references and related-test edges are excluded.

The deterministic behavior suite proves that editing a heuristic caller does not create a false stale result, while editing the target file does. A separate project fixture proves that an unrelated project edit remains current and that adding a new exact transitive dependent makes the prior evidence stale. It also proves that a contract cannot scope evidence for a different claim and that partial symbol names cannot create contracts.

Local evidence: 74/74 Release tests and the packaged CLI smoke test pass. The package smoke creates a contract, verifies its linked claim with `--contract`, and reads the canonical evidence file to confirm the closure mode, contract ID, and target path.

Remote evidence: [GitHub Actions run 33621238337](https://github.com/seekua/ArifCE/actions/runs/33621238337) passed at commit `b9ea930` on Windows, Ubuntu, and macOS. All five self-contained targets—Windows x64, Linux x64/ARM64, and macOS x64/ARM64—also published and smoke-tested successfully.

This is not symbol-level invalidation. A C# target currently scopes its complete declaration file, so an unrelated edit in that same file still requires re-verification. Caller/callee resolution, semantic dependency inference, heuristic expansion, and non-C# symbol closure remain deferred.
