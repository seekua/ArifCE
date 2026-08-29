# ArifCE
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>

[English](README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [한국어](README.ko.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Dansk](README.da.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [Русский](README.ru.md) · [Bosanski](README.bs.md) · [العربية](README.ar.md) · [Norsk](README.no.md) · [Português (Brasil)](README.pt-BR.md) · [ไทย](README.th.md) · [Türkçe](README.tr.md) · [Українська](README.uk.md) · [বাংলা](README.bn.md) · [Ελληνικά](README.el.md) · [Tiếng Việt](README.vi.md)

[![CI](https://github.com/seekua/ArifCE/actions/workflows/ci.yml/badge.svg)](https://github.com/seekua/ArifCE/actions/workflows/ci.yml) [![Latest release](https://img.shields.io/github/v/release/seekua/ArifCE?cacheSeconds=300)](https://github.com/seekua/ArifCE/releases/latest) [![License](https://img.shields.io/github/license/seekua/ArifCE?cacheSeconds=300)](LICENSE)

ArifCE je lokalni sloj projektne inteligencije i kontinuiteta za razvoj softvera uz pomoć AI-ja. Čuva kontekst, odluke, neuspjele pokušaje, dokaze, stanje refaktorisanja i informacije o primopredaji u repozitoriju kako bi Codex, Claude Code, OpenCode i budući agenti nastavili istu inženjersku priču.

> Repozitorij posjeduje kontekst. Agent ga samo posuđuje.

## Zašto ArifCE postoji

Softverski timovi gube vrijeme i povjerenje kada važan kontekst postoji samo u historiji chata, ličnom sjećanju ili alatu koji sljedeći saradnik ne može pregledati. ArifCE čini inženjerski kontinuitet dijelom samog projekta.

Cilj nije da agenti zvuče sigurnije, već da svaki saradnik razumije šta tim želi postići, zašto je odluka donesena, šta je zaista provjereno i gdje ostaje neizvjesnost. Kada priča ostane u repozitoriju, tim može brže napredovati bez gubitka sljedivosti, odgovornosti ili povjerenja.

ArifCE pretvara kontinuitet u zajedničku inženjersku praksu: usmjeren kontekst za sljedeći zadatak, jasne dokaze za važne tvrdnje i iskrene primopredaje kada posao nije završen.

## Kome je namijenjen

ArifCE je namijenjen inženjerskim timovima uz AI, programerima koji rade s kodnim agentima i održavaocima kojima kontekst projekta treba preživjeti jednu osobu, chat ili sesiju. Posebno je koristan kada više saradnika dijeli repozitorij.

## Kako ArifCE radi

```mermaid
flowchart LR
    A[Agent počinje] --> B[Čita protokol i trenutno stanje]
    B --> C[Preuzima kontekst zadatka]
    C --> D[Mijenja kod]
    D --> E[Bilježi tvrdnju i dokaz]
    E --> F{Provjera prolazi?}
    F -- Yes --> G[Kontrolna tačka i primopredaja]
    F -- No --> H[Bilježi nalaz ili neuspjeli pokušaj]
    H --> C
    G --> I[Sljedeći agent nastavlja]
```

## Istražite projekat

Pokrenite lokalnu nadzornu ploču za pregled zdravlja projekta, nedavnih zapisa i pretraživog konteksta:

```powershell
$env:ARIFCE_PROJECT_ROOT = (Get-Location).Path
dotnet run --project src/ArifCE.Dashboard/ArifCE.Dashboard.csproj
```

Zatim otvorite <http://127.0.0.1:5180/>. Kompletan priručnik proizvoda nalazi se u [ArifCE centru dokumentacije](docs/README.md).

This workflow keeps project knowledge in the repository and makes progress inspectable. The practical advantages are:

- Brže uključivanje: sljedeći agent čita fokusirano trenutno stanje umjesto da obnavlja dugu transkripciju.
- Sigurnije promjene: tvrdnje su povezane s determinističkim dokazima i zastarijevaju promjenom Git stanja.
- Bolji kontinuitet: odluke, neuspjeli pokušaji, kontrolne tačke i primopredaje preživljavaju promjene agenta ili sesije.
- Kontrolisani refaktoring: invarijante, inventar, zaštite i sigurne tačke čine nedovršen rad vidljivim.
- Lokalni rad: kanonske datoteke ostaju upotrebljive bez cloud usluge ili runtimea dobavljača.

## Više od memorije

ArifCE prati šta je zadatak bio, šta se promijenilo i zašto, šta agent tvrdi da je završio, koji dokazi podržavaju tvrdnju, šta je recenzent pronašao, šta je nedovršeno i šta sljedeći agent treba znati. Izjave agenata su tvrdnje, ne činjenice; prednost imaju deterministički dokazi iz builda, testova, Gita i pretrage.

Tehnička verifikacija i prihvatanje proizvoda su odvojeni: zapisi prihvatanja navode ko je odobrio tvrdnju i koji su aktuelni dokazi podržali odluku.

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

Kanonski Markdown, YAML, JSON i JSONL nalaze se u `.arifce/`. SQLite je izvedeni indeks koji se može obrisati; brisanje `.arifce/index/` i pokretanje `arifce rebuild` mora sačuvati inteligenciju projekta.

## Arhitektura

Jezgro odvaja pravila domena, kanonsko skladištenje i indeksiranje, posmatranje Gita, dohvat, verifikaciju, refaktorisanje, sigurnost i CLI. Datoteke uputa dobavljača su mali adapteri i nikada ne postaju kanonsko spremište memorije.

## Installation and quick start

V0.2.0 je objavljen kao višeplatformski .NET globalni alat. Pogledajte [instalaciju](docs/getting-started/installation.md) i [brzi početak](docs/getting-started/quick-start.md). Iz izvornog koda:

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

## Continuity, verification, and refactors

- A fresh agent reads `AGENTS.md`, `.arifce/PROTOCOL.md`, and `.arifce/CURRENT.md`, then requests task-specific context instead of bulk-loading history.
- Claims link to repository-scoped evidence. Evidence becomes stale when the relevant repository state changes.
- Refactor campaigns track invariants, inventory, guards, progress, and checkpoints. Blocking guards prevent completion.
- Handoffs summarize current engineering state rather than dumping transcripts.

## Sigurnost i ograničenja

Sirovi transkripti nisu pouzdani i nikada se ne učitavaju niti izvršavaju skupno. Uvozni putevi uklanjaju uobičajene tajne; vjerodajnice i podaci za autentifikaciju mašine ne pripadaju u `.arifce/`. V0.1 ne garantuje ispravnost, uštedu tokena ni bolji kvalitet pregleda; nema cloud uslugu, UI, vektorsku bazu, autonomni roj ni produkcijske pozive između agenata.

See [ROADMAP.md](ROADMAP.md), [SECURITY.md](SECURITY.md), and [CONTRIBUTING.md](CONTRIBUTING.md). The exact implemented command syntax is documented in the [CLI reference](docs/reference/cli.md).

## Licenca

ArifCE je licenciran pod [Apache License 2.0](LICENSE).
<p align="center"><img src="assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>






