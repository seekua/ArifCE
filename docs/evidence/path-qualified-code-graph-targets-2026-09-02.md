# Path-qualified code-graph target evidence

**Status:** implemented and verified locally and remotely.

## Problem

An exact simple-name graph target such as `Calculate` can still resolve to multiple structural declarations when unrelated files use the same name. Treating all of those files as one trusted closure can cause false staleness and needless re-verification.

## Implemented contract

```text
arifce codegraph query <path>::<symbol>
arifce contract create <path>::<symbol> [--risk <level>]
```

For example, `src/Payments/PaymentService.cs::Calculate` selects only declarations named `Calculate` in that repository-relative file. `verify --contract` then persists a closure that excludes an unrelated same-name declaration in another file.

Simple symbol names remain supported for discovery and backward compatibility. A path-qualified selector does not claim overload-level identity inside one file; parser-backed overload identity remains explicitly deferred.

## Local proof

- The behavior suite creates two `Calculate` declarations in separate files, verifies the path-qualified contract, and proves an edit to the other same-name file leaves the evidence current.
- `dotnet build ArifCE.slnx -c Release --no-restore -m:1` completed with 0 warnings and 0 errors.
- `dotnet test ArifCE.slnx -c Release --no-build --disable-build-servers -m:1` passed.
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/package-smoke.ps1` passed. Its packed-tool fixture creates two same-name declarations, queries the qualified target, verifies a qualified contract, and rejects a closure that contains the unrelated file.
- [GitHub Actions run 33627400656](https://github.com/seekua/ArifCE/actions/runs/33627400656) passed the Windows, Ubuntu, and macOS build/test/package jobs and all five self-contained native CLI targets for commit `2fd47ca`.
