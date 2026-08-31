# ArifCE
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>

[English](README.md) · [简体中文](docs/locales/README.zh-CN.md) · [繁體中文](docs/locales/README.zh-TW.md) · [한국어](docs/locales/README.ko.md) · [Deutsch](docs/locales/README.de.md) · [Español](docs/locales/README.es.md) · [Français](docs/locales/README.fr.md) · [Italiano](docs/locales/README.it.md) · [Dansk](docs/locales/README.da.md) · [日本語](docs/locales/README.ja.md) · [Polski](docs/locales/README.pl.md) · [Русский](docs/locales/README.ru.md) · [Bosanski](docs/locales/README.bs.md) · [العربية](docs/locales/README.ar.md) · [Norsk](docs/locales/README.no.md) · [Português (Brasil)](docs/locales/README.pt-BR.md) · [ไทย](docs/locales/README.th.md) · [Türkçe](docs/locales/README.tr.md) · [Українська](docs/locales/README.uk.md) · [বাংলা](docs/locales/README.bn.md) · [Ελληνικά](docs/locales/README.el.md) · [Tiếng Việt](docs/locales/README.vi.md)


**Agents change. Your project should not forget.**

[![CI](https://github.com/seekua/ArifCE/actions/workflows/ci.yml/badge.svg)](https://github.com/seekua/ArifCE/actions/workflows/ci.yml) [![Latest release](https://img.shields.io/github/v/release/seekua/ArifCE?cacheSeconds=300)](https://github.com/seekua/ArifCE/releases/latest) [![License](https://img.shields.io/github/license/seekua/ArifCE?cacheSeconds=300)](LICENSE)

ArifCE is a local-first project intelligence and continuity layer for AI-assisted software development. It keeps context, decisions, failed attempts, evidence, refactoring state, and handoff information with the repository so Codex, Claude Code, OpenCode, and future agents can continue the same engineering story.

> The repository owns the context. The agent only borrows it.

## Why ArifCE exists

Software teams lose time and confidence when important context lives only in chat history, individual memory, or a tool that the next contributor cannot inspect. ArifCE exists to make engineering continuity part of the project itself.

The goal is not to make agents sound more certain. The goal is to help every contributor understand what the team is trying to accomplish, why a decision was made, what has actually been verified, and where uncertainty remains. When that story stays with the repository, teams can move faster without giving up traceability, ownership, or trust.

ArifCE turns continuity into a shared engineering practice: focused context for the next task, explicit evidence for important claims, and honest handoffs when work is incomplete.

## Who it is for

ArifCE is for AI-assisted engineering teams, developers who work with coding agents, and maintainers who need project context to survive beyond one person, chat, or session. It is especially useful when several contributors share a repository and need a clear record of decisions, verification, and unfinished work.

## How ArifCE works

```mermaid
flowchart LR
    A[Agent starts] --> B[Read protocol and current state]
    B --> C[Retrieve task-specific context]
    C --> D[Change the code]
    D --> E[Record claim and evidence]
    E --> F{Verification passes?}
    F -- Yes --> G[Checkpoint and handoff]
    F -- No --> H[Record finding or failed attempt]
    H --> C
    G --> I[Next agent continues]
```

## Explore the project

Run the local dashboard to get a visual overview of project health, recent records, and searchable context:

```powershell
$env:ARIFCE_PROJECT_ROOT = (Get-Location).Path
dotnet run --project src/ArifCE.Dashboard/ArifCE.Dashboard.csproj
```

Then open <http://127.0.0.1:5180/>. For the complete product handbook, see the [ArifCE documentation hub](docs/README.md).

This workflow keeps project knowledge in the repository and makes progress inspectable. The practical advantages are:

- Faster onboarding: the next agent reads a focused current state instead of reconstructing a long transcript.
- Safer changes: claims are linked to deterministic evidence and become stale when Git state changes.
- Better continuity: decisions, failed attempts, checkpoints, and handoffs survive agent or session changes.
- Controlled refactors: invariants, inventory, guards, and safe points make incomplete work visible.
- Local-first operation: canonical files remain usable without a cloud service or vendor-specific runtime.

## Not just memory

ArifCE tracks what the task was, what changed, why it changed, what an agent claims it completed, what evidence supports that claim, what a reviewer found, what remains unfinished, and what the next agent needs to know. Agent statements are claims, not facts; deterministic build, test, Git, and search evidence is preferred.

Technical verification and product acceptance are separate: acceptance records identify who approved a claim and which current evidence supported that decision.

## V0.1 workflow

```text
arifce init
arifce task create "Fix permission cache race"
arifce checkpoint --summary "Reproduction added"
arifce context "finish the permission cache fix" --budget 16000
arifce claim create "Permission cache race is fixed"
arifce verify CLAIM-0001
arifce handoff
```

Canonical Markdown, YAML, JSON, and JSONL live under `.arifce/`. SQLite is a disposable derived index: deleting `.arifce/index/` and running `arifce rebuild` must preserve project intelligence.

## Architecture

The core separates domain rules, canonical storage and indexing, Git observation, retrieval, verification, refactoring, security, and the CLI. Vendor instruction files are small adapters; they never become the canonical memory store. See [architecture overview](docs/architecture/overview.md), [domain model](docs/architecture/domain-model.md), and [V0.1 specification](docs/SPECIFICATION-v0.1.md).

## Installation and quick start

V0.7.0 is published as a cross-platform .NET global tool. See [installation](docs/getting-started/installation.md) and the [quick start](docs/getting-started/quick-start.md). From source:

```bash
git clone https://github.com/seekua/ArifCE.git
cd ArifCE
dotnet restore ArifCE.slnx
dotnet build ArifCE.slnx --configuration Release --no-restore
dotnet test ArifCE.slnx --configuration Release --no-build --no-restore
```

The optional local MCP adapter is documented in [MCP setup](docs/getting-started/mcp.md).

For a complete installation and feature walkthrough, see the [User Guide](docs/USER-GUIDE.md) and [Documentation Policy](docs/DOCUMENTATION-POLICY.md).

### 60-second quick start

```bash
dotnet tool install --global ArifCE.Cli --version 0.7.0
mkdir my-project && cd my-project
git init
arifce init
arifce task create "Ship the first change"
arifce checkpoint --summary "Project context initialized"
arifce handoff
```

You now have a repository-local project state, a task, a checkpoint, and a semantic handoff ready for the next contributor.

### Local LLM workflows

ArifCE can use local or cloud-capable providers without moving project memory out of the repository. Configure a provider through an environment variable or stdin, preview bounded context, and run an evidence-backed task:

```bash
arifce llm provider add ollama Ollama llama3 --endpoint http://127.0.0.1:11434
arifce llm provider test ollama
arifce llm context "review the migration" --budget 2000
arifce llm run review "Check the migration for data-loss risk" --with-context --claim CLAIM-0001
```

Reviewer execution requires explicit approval. Provider fallback, token/cost accounting, canonical evidence, embeddings, benchmark metrics, MCP tools, and the local dashboard are documented in the [LLM provider reference](docs/reference/LLM-PROVIDERS.md).

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ArifCE.Cli -- init
```

Run `init` in a new Git repository or `adopt` in an existing one. Both are non-destructive and idempotent. `adopt` records observed structure and labels unknown historical rationale as unknown.

## Continuity, verification, and refactors

- A fresh agent reads `AGENTS.md`, `.arifce/PROTOCOL.md`, and `.arifce/CURRENT.md`, then requests task-specific context instead of bulk-loading history.
- Claims link to repository-scoped evidence. Evidence becomes stale when the relevant repository state changes.
- Refactor campaigns track invariants, inventory, guards, progress, and checkpoints. Blocking guards prevent completion.
- Handoffs summarize current engineering state rather than dumping transcripts.

## Security and limitations

Raw transcripts are untrusted and are never bulk-loaded or executed. Import paths redact common secrets; credentials and machine authentication data do not belong in `.arifce/`. V0.1 does not guarantee correctness, token savings, or better review quality. It has no cloud service, UI, vector database, autonomous swarm, or production cross-agent invocation.

See [ROADMAP.md](ROADMAP.md), [SECURITY.md](SECURITY.md), and [CONTRIBUTING.md](CONTRIBUTING.md). The exact implemented command syntax is documented in the [CLI reference](docs/reference/cli.md).

## License

ArifCE is licensed under the [Apache License 2.0](LICENSE).
