# README translation status

The English `README.md` is canonical. All 21 localized README files retain the complete canonical sections, commands, links, badges, license, safety notes, slogan, and context quote. The parity check is enforced by `scripts/check-readme-locales.ps1` in CI.

Latest successful remote CI evidence: [workflow run 33269324169](https://github.com/seekua/ArifCE/actions/runs/33269324169), validating commit `1bdbcdc`.

| File | Scope status | Human translation review |
| --- | --- | --- |
| `README.ar.md` | Full Arabic draft (commands/links preserved) | Machine-checked; human review pending |
| `README.bn.md` | Complete canonical reference | Machine-checked; human review pending |
| `README.bs.md` | Complete canonical reference | Machine-checked; human review pending |
| `README.da.md` | Full Danish draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.de.md` | Full German draft (commands/links preserved) | Machine-checked; human review pending |
| `README.el.md` | Full Greek draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.es.md` | Full Spanish draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.fr.md` | Full French draft (commands/links preserved) | Machine-checked; human review pending |
| `README.it.md` | Full Italian draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.ja.md` | Full Japanese draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.ko.md` | Full Korean draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.no.md` | Full Norwegian draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.pl.md` | Full Polish draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.pt-BR.md` | Full Brazilian Portuguese draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.ru.md` | Full Russian draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.th.md` | Complete canonical reference | Machine-checked; human review pending |
| `README.tr.md` | Complete canonical reference | Machine-checked; human review pending |
| `README.uk.md` | Full Ukrainian draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.vi.md` | Full Vietnamese draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.zh-CN.md` | Full Simplified Chinese draft (commands/links/diagram preserved) | Machine-checked; human review pending |
| `README.zh-TW.md` | Full Traditional Chinese draft (commands/links/diagram preserved) | Machine-checked; human review pending |

The repository includes `scripts/translate-readme-locales.ps1`, which performs paragraph-level DeepL web translation in batches while protecting commands, URLs, HTML, and Markdown links. The public endpoint currently rate-limits automated requests; the script retries and leaves the existing file unchanged when a batch cannot be translated.

Localized README files mirror the canonical structure: the logo, language selector, translated slogan, context quote, badges, product reference, diagrams, commands, safety notes, and license are kept in the same order. Executable commands, links, and badge URLs remain unchanged while surrounding prose is translated.

DeepL's public web endpoint does not expose Bengali (`README.bn.md`), Bosnian (`README.bs.md`), or Thai (`README.th.md`) targets. These three files therefore remain canonical-reference files until a provider supporting those languages is explicitly selected. No language is marked `Reviewed` until its complete translation is verified in CI.

Human translation work must preserve executable commands, relative links, badges, Mermaid syntax, security language, and explicit deferrals. Each reviewed language should be marked `Reviewed` here and verified in CI.
