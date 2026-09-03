# Benchmark token telemetry evidence

## Finding and correction

The trial completion script accepted manual token totals without checking captured usage. The suite collector summed unavailable zero sentinels as though they represented measured zero consumption. Those paths could imply a token comparison without measurements.

Phase 66 adds a dependency-free PowerShell parser for one completed Codex JSONL turn, records input/cached-input/output counters, and reparses the hash-bound agent log during verification. Total is input plus output. Unbound manual totals are rejected before completion artifacts are written. New unavailable values are null; old unavailable zero values remain readable but do not count as measurements. An arm containing any unavailable trial has a null aggregate total. The suite schema advances to 3; trial results advance to 2 without changing canonical project entities.

## Local proof

- `pwsh -NoProfile -File scripts/test-engineering-benchmark-telemetry.ps1`: passes synthetic usage parsing, cached-input accounting, result tampering, incomplete/malformed/duplicate/error events, invalid counters, integer overflow, absent telemetry, mixed aggregates, and explicit zero checks.
- `pwsh -NoProfile -File scripts/test-engineering-benchmark-completion.ps1`: passes with real isolated Git candidates and .NET evaluators; rejects manual totals before result creation, rejects result/log tampering, and preserves a no-candidate run with unavailable usage.
- `pwsh -NoProfile -File scripts/test-engineering-benchmark-suite.ps1`: passes matched preparation and incomplete-suite rejection.
- `dotnet test ArifCE.slnx --configuration Release --no-restore --disable-build-servers --maxcpucount:1`: 83 passed, 0 failed.

The first parser regression exposed a Windows file-handle leak when throwing while enumerating `File.ReadLines`. Explicit `StreamReader` disposal in `finally` corrected it. The test now rewrites the same fixture after each rejected input, exercising that failure path.

## Limits

All new usage events here are synthetic protocol fixtures, not model execution measurements. No model was invoked, no new benchmark outcome was produced, and no token-saving percentage is justified. The first real ten-pair result remains permission-confounded.

Only one thread and one completed turn are supported. Failed/error or unsupported logs can still be retained with unavailable usage. Other hosts, multi-turn aggregation, and active-time measurement remain deferred. Existing duration includes waiting and evaluation. The legacy imported-row validator checks shape rather than log provenance and is not a substitute for the protected completion/collection path.

Log hashing and reparsing detect inconsistent artifacts, not a dishonest operator replacing both log and metadata. Raw logs need privacy review before publication. Host protocol reference: [OpenAI non-interactive mode](https://learn.chatgpt.com/docs/non-interactive-mode).

## Remote proof

Pending. Local success is not represented as cross-platform CI success.
