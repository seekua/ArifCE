# ArifCE
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>


[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [한국어](README.ko.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Dansk](README.da.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [Русский](README.ru.md) · [Bosanski](README.bs.md) · [العربية](README.ar.md) · [Norsk](README.no.md) · [Português (Brasil)](README.pt-BR.md) · [ไทย](README.th.md) · [Türkçe](README.tr.md) · [Українська](README.uk.md) · [বাংলা](README.bn.md) · [Ελληνικά](README.el.md) · [Tiếng Việt](README.vi.md)

**Les agents changent. Votre projet n’oublie rien.** ArifCE est une couche locale d’intelligence et de continuité pour le développement assisté par IA. Les décisions, preuves, tentatives échouées et transmissions restent dans le dépôt.

## Démarrage rapide

```powershell
dotnet tool install --global ArifCE.Cli
cd your-project
arifce init
arifce status
```

## Référence produit complète

L’introduction traduite ci-dessus est suivie de la référence produit canonique complète. Ainsi, chaque commande, lien, badge, note de sécurité et limitation reste disponible pendant la finalisation de la traduction humaine.

**Les agents changent. Votre projet n’oublie rien.**

[![CI](https://github.com/seekua/ArifCE/actions/workflows/ci.yml/badge.svg)](https://github.com/seekua/ArifCE/actions/workflows/ci.yml) [![Latest release](https://img.shields.io/github/v/release/seekua/ArifCE?cacheSeconds=300)](https://github.com/seekua/ArifCE/releases/latest) [![License](https://img.shields.io/github/license/seekua/ArifCE?cacheSeconds=300)](LICENSE)

ArifCE est une couche locale d’intelligence et de continuité du projet pour le développement logiciel assisté par IA. Elle conserve le contexte, les décisions, les tentatives échouées, les preuves, l’état du refactoring et les informations de passation dans le dépôt, afin que Codex, Claude Code, OpenCode et les futurs agents poursuivent la même histoire d’ingénierie.

> Le dépôt possède le contexte. L’agent ne fait que l’emprunter.

## Pourquoi ArifCE existe

Les équipes logicielles perdent du temps et de la confiance lorsque le contexte important ne vit que dans l’historique des discussions, la mémoire d’une personne ou un outil que le prochain contributeur ne peut pas examiner. ArifCE fait de la continuité d’ingénierie une partie intégrante du projet.

L’objectif n’est pas de faire paraître les agents plus sûrs d’eux. Il est d’aider chaque contributeur à comprendre ce que l’équipe cherche à accomplir, pourquoi une décision a été prise, ce qui a réellement été vérifié et où subsiste l’incertitude. Lorsque cette histoire reste dans le dépôt, les équipes avancent plus vite sans renoncer à la traçabilité, à la responsabilité ni à la confiance.

ArifCE transforme la continuité en pratique d’ingénierie partagée : un contexte ciblé pour la prochaine tâche, des preuves explicites pour les affirmations importantes et des passations honnêtes lorsque le travail est incomplet.

## À qui s’adresse ArifCE

ArifCE s’adresse aux équipes d’ingénierie assistées par IA, aux développeurs qui travaillent avec des agents de codage et aux mainteneurs qui ont besoin que le contexte du projet survive à une personne, une discussion ou une session. Il est particulièrement utile lorsque plusieurs contributeurs partagent un dépôt et doivent conserver une trace claire des décisions, de la vérification et du travail inachevé.

## Comment fonctionne ArifCE

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

## Explorer le projet

Lancez le tableau de bord local pour obtenir une vue visuelle de la santé du projet, des enregistrements récents et du contexte consultable :

```powershell
$env:ARIFCE_PROJECT_ROOT = (Get-Location).Path
dotnet run --project src/ArifCE.Dashboard/ArifCE.Dashboard.csproj
```

Ouvrez ensuite <http://127.0.0.1:5180/>. Pour le guide produit complet, consultez le [centre de documentation ArifCE](docs/README.md).

This workflow keeps project knowledge in the repository and makes progress inspectable. The practical advantages are:

- Faster onboarding: the next agent reads a focused current state instead of reconstructing a long transcript.
- Safer changes: claims are linked to deterministic evidence and become stale when Git state changes.
- Better continuity: decisions, failed attempts, checkpoints, and handoffs survive agent or session changes.
- Controlled refactors: invariants, inventory, guards, and safe points make incomplete work visible.
- Local-first operation: canonical files remain usable without a cloud service or vendor-specific runtime.

## Plus qu’une mémoire

ArifCE tracks what the task was, what changed, why it changed, what an agent claims it completed, what evidence supports that claim, what a reviewer found, what remains unfinished, and what the next agent needs to know. Agent statements are claims, not facts; deterministic build, test, Git, and search evidence is preferred.

Technical verification and product acceptance are separate: acceptance records identify who approved a claim and which current evidence supported that decision.

## Flux de travail V0.1

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

## Installation et démarrage rapide

V0.2.0 is published as a cross-platform .NET global tool. See [installation](docs/getting-started/installation.md) and the [quick start](docs/getting-started/quick-start.md). From source:

The optional local MCP adapter is documented in [MCP setup](docs/getting-started/mcp.md).

For a complete installation and feature walkthrough, see the [User Guide](docs/USER-GUIDE.md) and [Documentation Policy](docs/DOCUMENTATION-POLICY.md).

### 60-second quick start

```bash
dotnet tool install --global ArifCE.Cli --version 0.2.0
mkdir my-project && cd my-project
git init
arifce init
arifce task create "Ship the first change"
arifce checkpoint --summary "Project context initialized"
arifce handoff
```

You now have a repository-local project state, a task, a checkpoint, and a semantic handoff ready for the next contributor.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ArifCE.Cli -- init
```

Run `init` in a new Git repository or `adopt` in an existing one. Both are non-destructive and idempotent. `adopt` records observed structure and labels unknown historical rationale as unknown.

## Continuité, vérification et refactorings

- A fresh agent reads `AGENTS.md`, `.arifce/PROTOCOL.md`, and `.arifce/CURRENT.md`, then requests task-specific context instead of bulk-loading history.
- Claims link to repository-scoped evidence. Evidence becomes stale when the relevant repository state changes.
- Refactor campaigns track invariants, inventory, guards, progress, and checkpoints. Blocking guards prevent completion.
- Handoffs summarize current engineering state rather than dumping transcripts.

## Sécurité et limitations

Raw transcripts are untrusted and are never bulk-loaded or executed. Import paths redact common secrets; credentials and machine authentication data do not belong in `.arifce/`. V0.1 does not guarantee correctness, token savings, or better review quality. It has no cloud service, UI, vector database, autonomous swarm, or production cross-agent invocation.

See [ROADMAP.md](ROADMAP.md), [SECURITY.md](SECURITY.md), and [CONTRIBUTING.md](CONTRIBUTING.md). The exact implemented command syntax is documented in the [CLI reference](docs/reference/cli.md).

## Licence

ArifCE est distribué sous [licence Apache 2.0](LICENSE).
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>
