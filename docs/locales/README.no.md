# ArifCE
<p align="center"><img src="../../assets/ArifCE.svg" alt="ArifCE" width="258" height="102"></p>

**Les på et annet språk.**

[English](../../README.md) · [简体中文](README.zh-CN.md) · [繁體中文](README.zh-TW.md) · [한국어](README.ko.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Français](README.fr.md) · [Italiano](README.it.md) · [Dansk](README.da.md) · [日本語](README.ja.md) · [Polski](README.pl.md) · [Русский](README.ru.md) · [Bosanski](README.bs.md) · [العربية](README.ar.md) · [Norsk](README.no.md) · [Português (Brasil)](README.pt-BR.md) · [ไทย](README.th.md) · [Türkçe](README.tr.md) · [Українська](README.uk.md) · [বাংলা](README.bn.md) · [Ελληνικά](README.el.md) · [Tiếng Việt](README.vi.md)

**Agenter endrer seg. Prosjektet ditt bør ikke glemme.**


[![CI](https://github.com/seekua/ArifCE/actions/workflows/ci.yml/badge.svg)](https://github.com/seekua/ArifCE/actions/workflows/ci.yml) [![Latest release](https://img.shields.io/github/v/release/seekua/ArifCE?cacheSeconds=300)](https://github.com/seekua/ArifCE/releases/latest) [![License](https://img.shields.io/github/license/seekua/ArifCE?cacheSeconds=300)](../../LICENSE)

ArifCE er et lokalt først-lag for prosjektintelligens og kontinuitet i AI-assistert programvareutvikling. Det oppbevarer kontekst, beslutninger, mislykkede forsøk, bevis, refaktoreringstilstand og overleveringsinformasjon i repositoriet, slik at Codex, Claude Code, OpenCode og fremtidige agenter kan fortsette den samme utviklingshistorien.

> Repositoriet eier konteksten. Agenten låner den bare.

**Har grensen din blitt nådd? Fortsett med to kommandoer.**

Når du har fullført selve arbeidet med en eksisterende oppgave, må du registrere en overlevering før du avslutter.

```bash
arifce handoff --task TASK-0031

# When you return with Codex, Claude Code, OpenCode, or a local model:
arifce context --task TASK-0031 --budget 2000
```

Overleveringen inneholder målet, utført arbeid, verifisert dokumentasjon, uavklarte punkter, feil og neste handling. Kopier den utskrevne konteksten inn i ledeteksten (prompten) for den nye agenten; CLI-et legger ikke automatisk inn kontekst i en modelløkt.

## Installasjon og hurtigstart

Last ned det frittstående arkivet for din plattform fra [GitHub Releases](https://github.com/seekua/ArifCE/releases/tag/v0.8.1), pakk det ut, og legg til `arifce` i PATH-en din. På Linux må du sørge for at kjøretillatelsen bevares under utpakking, eller kjøre `chmod +x arifce`. Det kreves ingen separat installasjon av .NET, Node, Python, Docker eller database.

For et nytt prosjekt:

```bash
mkdir my-project && cd my-project
git init
arifce init
arifce task create "Ship the first change"
arifce handoff
```

For et eksisterende Git-depot:

```bash
cd path/to/existing-repo
arifce adopt
arifce task create "Ship the first change"
arifce handoff
```

Bruk `adopt` når depotet allerede inneholder kode: Det registrerer den observerte strukturen uten å overskrive den, og gir deretter neste agent et prosjektspesifikt utgangspunkt. [Installasjon og hurtigstart](../getting-started/installation.md).

## Hvorfor ArifCE finnes

Programvareteam mister tid og tillit når viktig kontekst bare finnes i chathistorikk, individuell hukommelse eller et verktøy neste bidragsyter ikke kan inspisere. ArifCE gjør kontinuitet i utviklingen til en del av selve prosjektet.

Målet er ikke å få agenter til å høres sikrere ut. Målet er å hjelpe alle bidragsytere med å forstå hva teamet prøver å oppnå, hvorfor en beslutning ble tatt, hva som faktisk er verifisert og hvor usikkerhet gjenstår. Når historien blir i repositoriet, kan team bevege seg raskere uten å gi avkall på sporbarhet, eierskap eller tillit.

ArifCE gjør kontinuitet til en felles ingeniørpraksis: fokusert kontekst for neste oppgave, tydelige bevis for viktige påstander og ærlige overleveringer når arbeidet er ufullstendig.

**Hvem det er for.**

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

Kjør det lokale dashbordet for en visuell oversikt over prosjektets helse, nylige poster og søkbar kontekst: Denne utviklerkommandoen bruker .NET SDK; den frittstående installasjonen som er beskrevet ovenfor, krever ikke dette.

### Forhåndsvisning av kontrollpanelet

<p align="center">
  <img src="../images/dashboard-overview.png" alt="ArifCE dashboard overview" width="100%">
  <img src="../images/dashboard-trust.png" alt="ArifCE claims, evidence, work and risk dashboard" width="49%">
  <img src="../images/dashboard-records.png" alt="ArifCE repository memory explorer" width="49%">
</p>

```powershell
$env:ARIFCE_PROJECT_ROOT = (Get-Location).Path
dotnet run --project src/ArifCE.Dashboard/ArifCE.Dashboard.csproj
```

Åpne deretter <http://127.0.0.1:5180/>. Se [ArifCE-dokumentasjonshuben](../README.md) for den komplette produkthåndboken.

Denne arbeidsflyten holder prosjektkunnskap i repositoriet og gjør fremdriften etterprøvbar. De praktiske fordelene er:

- Raskere innføring: neste agent leser en konsentrert gjeldende status i stedet for å rekonstruere en lang utskrift.
- Sikrere endringer: påstander kobles til deterministiske bevis og blir utdaterte når Git-statusen endres.
- Bedre kontinuitet: beslutninger, mislykkede forsøk, sjekkpunkter og overleveringer overlever agent- og øktbytter.
- Kontrollerte refaktoreringer: invarians, inventar, vakter og sikre punkter synliggjør uferdig arbeid.
- Lokal først-drift: kanoniske filer kan brukes uten skytjeneste eller leverandørspesifikk kjøretid.

## Mer enn bare minne

ArifCE sporer hva oppgaven var, hva som ble endret og hvorfor, hva en agent hevder å ha fullført, hvilke bevis som støtter påstanden, hva en gjennomgåer fant, hva som gjenstår og hva neste agent må vite. Agentutsagn er påstander, ikke fakta; deterministiske bygge-, test-, Git- og søkebevis foretrekkes.

Teknisk verifisering og produktgodkjenning er separate: godkjenningsposter viser hvem som godkjente en påstand og hvilke aktuelle bevis som støttet avgjørelsen.

## Grunnleggende arbeidsflyt

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

Kjernen skiller domeneregler, kanonisk lagring og indeksering, Git-observasjon, henting, verifisering, refaktorering, sikkerhet og CLI. Leverandørens instruksjonsfiler er små adaptere og blir aldri det kanoniske minnelageret. Se [arkitekturoversikten](../architecture/overview.md), [domenemodellen](../architecture/domain-model.md) og [V0.1-spesifikasjonen](../SPECIFICATION-v0.1.md).

**Utvikling fra kildekode. V0.8.1 er gjeldende versjon. For utvikling fra kildekode, se installasjonsveiledningen og hurtigstarten.** [Installasjon og hurtigstart](../getting-started/installation.md) · [Hurtigstart](../getting-started/quick-start.md).

```bash
git clone https://github.com/seekua/ArifCE.git
cd ArifCE
dotnet restore ArifCE.slnx
dotnet build ArifCE.slnx --configuration Release --no-restore
dotnet test ArifCE.slnx --configuration Release --no-build --no-restore
```

Den valgfrie lokale MCP-adapteren er dokumentert i [MCP-oppsett](../getting-started/mcp.md).

For en komplett gjennomgang av installasjon og funksjoner, se [brukerveiledningen](../USER-GUIDE.md) og [dokumentasjonspolicyen](../DOCUMENTATION-POLICY.md).

Kommandoene for installasjon og oppstart ovenfor oppretter en prosjekttilstand lokalt i depotet, en oppgave og en overlevering som er klar for neste bidragsyter.

### Fortsett en oppgave med Ollama eller LM Studio

ArifCE lagrer kanoniske prosjektoppføringer i repositoriet. Leverandøren mottar prompten og valgt kontekst; skyleverandører mottar det valgte innholdet eksternt. `--with-context` legger til prosjektoppføringene ArifCE har valgt, men leser ikke kildefiler. Eksemplene nedenfor legger uttrykkelig innholdet fra migreringsfilen inn i prompten, slik at modellen får koden den blir bedt om å undersøke. Velg eksempelet for skallet ditt.

Installer og start Ollama før du bruker et av eksemplene. Den første kommandoen laster ned modellen `llama3`; la Ollama fortsette å kjøre på det lokale endepunktet som er angitt nedenfor.

```bash
ollama pull llama3
arifce llm provider add ollama Ollama llama3 --endpoint http://127.0.0.1:11434
arifce llm provider test ollama
task_id="$(arifce task create "Review and safely update the migration")"
migration_source="$(cat path/to/migration.sql)"
prompt="Review only this SQL migration for data-loss risks. Do not infer unseen source. Suggest the smallest safe change if needed.
Migration source:
$migration_source"
arifce llm run "Review and safely update the migration" "$prompt" --with-context --budget 2000
```

```powershell
ollama pull llama3
arifce llm provider add ollama Ollama llama3 --endpoint http://127.0.0.1:11434
arifce llm provider test ollama
$taskId = arifce task create "Review and safely update the migration"
$migrationSource = Get-Content -Raw -LiteralPath "path/to/migration.sql"
$prompt = @"
Review only this SQL migration for data-loss risks. Do not infer unseen source. Suggest the smallest safe change if needed.
Migration source:
$migrationSource
"@
arifce llm run "Review and safely update the migration" $prompt --with-context --budget 2000
```

Gå gjennom modellens svar, bruk eventuelle foreslåtte endringer i utviklingsverktøyet ditt, og kjør regresjonstestene for migreringen. Når de består, registrerer du bare det testkommandoen beviser som en claim:

```bash
claim_id="$(arifce claim create "Migration regression tests pass" --task "$task_id")"
arifce verify "$claim_id" --command "dotnet test path/to/migration-tests.csproj"
arifce handoff
```

I PowerShell:

```powershell
$claimId = arifce claim create "Migration regression tests pass" --task $taskId
arifce verify $claimId --command "dotnet test path/to/migration-tests.csproj"
arifce handoff
```

Testresultatet støtter claimen om at regresjonstestene består; det beviser ikke i seg selv at modellens gjennomgang var fullstendig eller korrekt. Bytt ut eksempelstiene og testkommandoen med dem fra repositoriet ditt. For LM Studio bruker du navnet på den innlastede modellen og det OpenAI-kompatible endepunktet, vanligvis `http://127.0.0.1:1234/v1`, i `arifce llm provider add lmstudio LmStudio your-loaded-model --endpoint http://127.0.0.1:1234/v1`.

Følg deretter samme flyt for oppgaven, kildekoden, testbeviset og overleveringen.

Kjøring av reviewer krever uttrykkelig godkjenning. Leverandørreserve, token-/kostnadssporing, kanonisk evidens, embeddings, benchmarkmålinger, MCP-verktøy og lokalt dashbord er beskrevet i [LLM-leverandørreferansen](../reference/LLM-PROVIDERS.md).

Kjør `init` i et nytt Git-repositorium eller `adopt` i et eksisterende. Begge er ikke-destruktive og idempotente; `adopt` registrerer observert struktur og markerer ukjente historiske begrunnelser som ukjente.

## Kontinuitet, verifisering og refaktorering

- En ny agent leser `AGENTS.md`, `.arifce/PROTOCOL.md` og `.arifce/CURRENT.md`, og ber deretter om oppgavespesifikk kontekst i stedet for å laste inn hele historikken.
- Påstander lenker til bevis avgrenset til repositoriet. Bevis blir utdatert når relevant repository-status endres.
- Refaktoreringer sporer invarians, inventar, vakter, fremdrift og sjekkpunkter. Blokkerende vakter hindrer fullføring.
- Overleveringer oppsummerer gjeldende ingeniørstatus i stedet for å dumpe transkripsjoner.

## Sikkerhet og begrensninger

Råutskrifter anses som upålitelige og blir aldri masseimportert eller kjørt. Importstier maskerer vanlige hemmeligheter; påloggingsinformasjon og maskinautentiseringsdata hører ikke hjemme i `.arifce`. ArifCE garanterer ikke korrekthet, redusert token-bruk eller bedre kvalitet på gjennomganger. Verktøyet har ingen skytjeneste, vertbasert brukergrensesnitt, vektordatabase, autonomt agentnettverk (swarm) eller produksjonsløsninger for kall mellom agenter. Et lokalt dashbord følger med; dette er ikke en vertbasert webapplikasjon.

Se [ROADMAP.md](../../ROADMAP.md), [SECURITY.md](../../SECURITY.md) og [CONTRIBUTING.md](../../CONTRIBUTING.md). Den nøyaktige syntaksen for implementerte kommandoer er dokumentert i [CLI-referansen](../reference/cli.md).

## Lisens

ArifCE er lisensiert under [Apache License 2.0](../../LICENSE).
