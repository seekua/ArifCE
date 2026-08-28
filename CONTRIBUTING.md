# Contributing

Use .NET 10 and preserve the dependency direction from CLI/adapters through infrastructure to core. Before submitting a change, run `dotnet restore`, `dotnet build ArifCE.slnx`, and `dotnet test ArifCE.slnx`. Add behavior-focused tests for user-visible changes.

Do not silently change canonical schemas, invent project rationale during adoption, treat agent statements as facts, or make SQLite authoritative. Schema or CLI compatibility changes require a documented decision and migration plan. Security reports follow `SECURITY.md` rather than public issues.

## Documentation and translations

The English `README.md` is canonical. Changes to commands, links, badges, Mermaid diagrams, security language, limitations, or release guidance must be reflected in every `README.*.md` file. Keep translated prose in the selected language, preserve executable snippets exactly, and run `./scripts/check-readme-locales.ps1` before opening a pull request. Record any translation review deferral in [`docs/TRANSLATION-STATUS.md`](docs/TRANSLATION-STATUS.md) and `ROADMAP.md`; never remove content to make a translation appear complete.
