# Contributing

Use .NET 10 and preserve the dependency direction from CLI/adapters through infrastructure to core. Before submitting a change, run `dotnet restore`, `dotnet build ArifCE.slnx`, and `dotnet test ArifCE.slnx`. Add behavior-focused tests for user-visible changes.

Do not silently change canonical schemas, invent project rationale during adoption, treat agent statements as facts, or make SQLite authoritative. Schema or CLI compatibility changes require a documented decision and migration plan. Security reports follow `SECURITY.md` rather than public issues.
