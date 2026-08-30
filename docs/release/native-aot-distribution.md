# NativeAOT distribution plan

NativeAOT is a planned distribution channel for the CLI, not a claim that the current package is already AOT-compatible.

## Acceptance criteria

- Produce self-contained single-file binaries for Windows, Linux, and macOS.
- Run `arifce init`, `status`, `search`, `context`, and `doctor` from each binary without a .NET runtime installed.
- Preserve local-only storage, SQLite FTS5 behavior, secret redaction, and exit-code semantics.
- Publish checksums and a reproducible release command.
- Keep the .NET global tool as a supported fallback.

## Compatibility checks before enabling AOT

1. Audit reflection and JSON converter paths, especially flexible enum deserialization.
2. Verify `Microsoft.Data.Sqlite` native assets for each target runtime.
3. Add a publish matrix to CI without replacing the normal build/test job.
4. Run the packaged binary smoke test against a temporary Git repository.
5. Document any trimming roots or source-generation changes required.

The current repository has not enabled `PublishAot`; no release binary is advertised until these checks pass.
