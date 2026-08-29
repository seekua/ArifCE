# ArifCE
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [한국어](README.ko.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Dansk](README.da.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [Русский](README.ru.md) · [Bosanski](README.bs.md) · [العربية](README.ar.md) · [Norsk](README.no.md) · [Português (Brasil)](README.pt-BR.md) · [ไทย](README.th.md) · [Türkçe](README.tr.md) · [Українська](README.uk.md) · [বাংলা](README.bn.md) · [Ελληνικά](README.el.md) · [Tiếng Việt](README.vi.md)

[![CI](https://github.com/seekua/ArifCE/actions/workflows/ci.yml/badge.svg)](https://github.com/seekua/ArifCE/actions/workflows/ci.yml) [![Latest release](https://img.shields.io/github/v/release/seekua/ArifCE?cacheSeconds=300)](https://github.com/seekua/ArifCE/releases/latest) [![License](https://img.shields.io/github/license/seekua/ArifCE?cacheSeconds=300)](LICENSE)

ArifCE er et lokalt først-lag for prosjektintelligens og kontinuitet i AI-assistert programvareutvikling. Det oppbevarer kontekst, beslutninger, mislykkede forsøk, bevis, refaktoreringstilstand og overleveringsinformasjon i repositoriet, slik at Codex, Claude Code, OpenCode og fremtidige agenter kan fortsette den samme utviklingshistorien.

> Repositoriet eier konteksten. Agenten låner den bare.

## Hvorfor ArifCE finnes

Programvareteam mister tid og tillit når viktig kontekst bare finnes i chathistorikk, individuell hukommelse eller et verktøy neste bidragsyter ikke kan inspisere. ArifCE gjør kontinuitet i utviklingen til en del av selve prosjektet.

Målet er ikke å få agenter til å høres sikrere ut. Målet er å hjelpe alle bidragsytere med å forstå hva teamet prøver å oppnå, hvorfor en beslutning ble tatt, hva som faktisk er verifisert og hvor usikkerhet gjenstår. Når historien blir i repositoriet, kan team bevege seg raskere uten å gi avkall på sporbarhet, eierskap eller tillit.

ArifCE gjør kontinuitet til en felles ingeniørpraksis: fokusert kontekst for neste oppgave, tydelige bevis for viktige påstander og ærlige overleveringer når arbeidet er ufullstendig.

## Hvem det er for

ArifCE er for AI-assisterte ingeniørteam, utviklere som arbeider med kodeagenter og vedlikeholdere som trenger at prosjektkontekst overlever én person, chat eller økt. Det er spesielt nyttig når flere bidragsytere deler et repository og trenger en tydelig oversikt over beslutninger, verifisering og uferdig arbeid.

## Slik fungerer ArifCE

```mermaid
flowchart LR
    A[Agenten starter] --> B[Les protokoll og gjeldende status]
    B --> C[Hent oppgavespesifikk kontekst]
    C --> D[Endre koden]
    D --> E[Registrer påstand og bevis]
    E --> F{Består verifiseringen?}
    F -- Ja --> G[Sjekkpunkt og overlevering]
    F -- Nei --> H[Registrer funn eller mislykket forsøk]
    H --> C
    G --> I[Neste agent fortsetter]
```

## Utforsk prosjektet

Kjør det lokale dashbordet for en visuell oversikt over prosjektets helse, nylige poster og søkbar kontekst:

```powershell
$env:ARIFCE_PROJECT_ROOT = (Get-Location).Path
dotnet run --project src/ArifCE.Dashboard/ArifCE.Dashboard.csproj
```

Åpne deretter <http://127.0.0.1:5180/>. Se [ArifCE-dokumentasjonshuben](docs/README.md) for den komplette produkthåndboken.

Denne arbeidsflyten holder prosjektkunnskap i repositoriet og gjør fremdriften etterprøvbar. De praktiske fordelene er:

- Faster onboarding: the next agent reads a focused current state instead of reconstructing a long transcript.
- Safer changes: claims are linked to deterministic evidence and become stale when Git state changes.
- Better continuity: decisions, failed attempts, checkpoints, and handoffs survive agent or session changes.
- Controlled refactors: invariants, inventory, guards, and safe points make incomplete work visible.
- Local-first operation: canonical files remain usable without a cloud service or vendor-specific runtime.

## Mer enn bare minne

ArifCE sporer hva oppgaven var, hva som ble endret og hvorfor, hva en agent hevder å ha fullført, hvilke bevis som støtter påstanden, hva en gjennomgåer fant, hva som gjenstår og hva neste agent må vite. Agentutsagn er påstander, ikke fakta; deterministiske bygge-, test-, Git- og søkebevis foretrekkes.

Teknisk verifisering og produktgodkjenning er separate: godkjenningsposter viser hvem som godkjente en påstand og hvilke aktuelle bevis som støttet avgjørelsen.

## V0.1-arbeidsflyt

```text
arifce init
arifce task create "Fix permission cache race"
arifce checkpoint --summary "Reproduction added"
arifce context "finish the permission cache fix" --budget 16000
arifce claim create "Permission cache race is fixed"
arifce verify CLAIM-0001
arifce handoff
```

Kanoniske Markdown-, YAML-, JSON- og JSONL-filer ligger under `.arifce/`. SQLite er en avledet indeks som kan slettes: sletting av `.arifce/index/` og kjøring av `arifce rebuild` skal bevare prosjektintelligensen.

## Arkitektur

The core separates domain rules, canonical storage and indexing, Git observation, retrieval, verification, refactoring, security, and the CLI. Vendor instruction files are small adapters; they never become the canonical memory store. See [architecture overview](docs/architecture/overview.md), [domain model](docs/architecture/domain-model.md), and [V0.1 specification](docs/SPECIFICATION-v0.1.md).

## Installasjon og hurtigstart

V0.2.0 er publisert som et plattformuavhengig .NET-globalverktøy. Se [installasjon](docs/getting-started/installation.md) og [hurtigstart](docs/getting-started/quick-start.md). Fra kildekode:

Den valgfrie lokale MCP-adapteren er dokumentert i [MCP-oppsett](docs/getting-started/mcp.md).

For en komplett gjennomgang av installasjon og funksjoner, se [brukerveiledningen](docs/USER-GUIDE.md) og [dokumentasjonspolicyen](docs/DOCUMENTATION-POLICY.md).

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

Du har nå en repository-lokal prosjektstatus, en oppgave, et sjekkpunkt og en semantisk overlevering klar for neste bidragsyter.

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/ArifCE.Cli -- init
```

Run `init` in a new Git repository or `adopt` in an existing one. Both are non-destructive and idempotent. `adopt` records observed structure and labels unknown historical rationale as unknown.

## Kontinuitet, verifisering og refaktorering

- A fresh agent reads `AGENTS.md`, `.arifce/PROTOCOL.md`, and `.arifce/CURRENT.md`, then requests task-specific context instead of bulk-loading history.
- Claims link to repository-scoped evidence. Evidence becomes stale when the relevant repository state changes.
- Refactor campaigns track invariants, inventory, guards, progress, and checkpoints. Blocking guards prevent completion.
- Handoffs summarize current engineering state rather than dumping transcripts.

## Sikkerhet og begrensninger

Rå transkripsjoner er upålitelige og lastes eller kjøres aldri i bulk. Importbaner redigerer vanlige hemmeligheter; legitimasjon og maskinautentisering hører ikke hjemme i `.arifce/`. V0.1 garanterer ikke korrekthet, tokenbesparelser eller bedre gjennomgangskvalitet. Det finnes ingen skytjeneste, UI, vektordatabase, autonom sverm eller produksjonskall mellom agenter.

Se [ROADMAP.md](ROADMAP.md), [SECURITY.md](SECURITY.md) og [CONTRIBUTING.md](CONTRIBUTING.md). Den nøyaktige syntaksen for implementerte kommandoer er dokumentert i [CLI-referansen](docs/reference/cli.md).

## Lisens

ArifCE er lisensiert under [Apache License 2.0](LICENSE).
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>
