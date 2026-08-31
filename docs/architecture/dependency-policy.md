# Dependency policy

ArifCE treats its dependency graph as part of the trust boundary. The canonical project memory must remain readable and rebuildable with the repository, Git, the .NET base class library, and SQLite. A package failure must never make the repository's source-of-truth records unreadable.

## Core rules

- Keep `ArifCE.Core` free of third-party packages.
- Keep infrastructure dependencies small and explicit. `Microsoft.Data.Sqlite` is the current runtime package because it provides the local derived index; canonical Markdown/JSONL state does not depend on it.
- Keep provider SDKs, embedding engines, vector stores, and vendor clients outside the core. Integrations must implement a narrow adapter contract and be removable without changing canonical schemas.
- Do not add a framework merely to hide a small protocol. Prefer BCL HTTP, process, serialization, and filesystem APIs when they are sufficient.
- Pin package versions and review transitive changes in CI before merging.

## Review checklist

Every new direct dependency must document:

1. Why the BCL or an existing package cannot provide the behavior.
2. Whether canonical data or only a disposable projection depends on it.
3. The replacement/removal path if the package becomes unavailable.
4. License, maintenance, vulnerability, and platform implications.
5. The test proving the fallback and rebuild behavior.

The test project may use test-only packages; those do not become runtime requirements. Optional LLM, IDE, MCP, and vector integrations must remain opt-in adapters. The supported global tool and future self-contained binaries must not require users to install Node, Python, Docker, a vector database, or an embedding server.

## Distribution principle

The implementation language is an internal detail. The preferred user experience is `arifce init`, backed by the .NET global tool today and self-contained platform binaries as they pass compatibility checks. NativeAOT is tracked separately in the [distribution plan](../release/native-aot-distribution.md); it is not advertised until its reflection and SQLite diagnostics are resolved.
