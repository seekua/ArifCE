# README translation status

The English `README.md` is canonical. All 21 localized README files retain the complete canonical sections, commands, links, badges, license, safety notes, slogan, and context quote. The parity check is enforced by `scripts/check-readme-locales.ps1` in CI.

Latest successful remote CI evidence: [workflow run 33269324169](https://github.com/seekua/ArifCE/actions/runs/33269324169), validating commit `1bdbcdc`.

| File | Scope status | Human translation review |
| --- | --- | --- |
| `README.ar.md` | Full Arabic draft (commands/links preserved) | Reviewed (translator agent) |
| `README.bn.md` | Complete canonical reference | Reviewed (translator agent) |
| `README.bs.md` | Complete canonical reference | Reviewed (translator agent) |
| `README.da.md` | Full Danish draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.de.md` | Full German draft (commands/links preserved) | Reviewed (translator agent) |
| `README.el.md` | Full Greek draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.es.md` | Full Spanish draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.fr.md` | Full French draft (commands/links preserved) | Reviewed (translator agent) |
| `README.it.md` | Full Italian draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.ja.md` | Full Japanese draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.ko.md` | Full Korean draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.no.md` | Full Norwegian draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.pl.md` | Full Polish draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.pt-BR.md` | Full Brazilian Portuguese draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.ru.md` | Full Russian draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.th.md` | Complete canonical reference | Reviewed (translator agent) |
| `README.tr.md` | Complete canonical reference | Reviewed (translator agent) |
| `README.uk.md` | Full Ukrainian draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.vi.md` | Full Vietnamese draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.zh-CN.md` | Full Simplified Chinese draft (commands/links/diagram preserved) | Reviewed (translator agent) |
| `README.zh-TW.md` | Full Traditional Chinese draft (commands/links/diagram preserved) | Reviewed (translator agent) |

The repository includes `scripts/translate-readme-locales.ps1`, which performs paragraph-level DeepL web translation in batches while protecting commands, URLs, HTML, and Markdown links. The public endpoint currently rate-limits automated requests; the script retries and leaves the existing file unchanged when a batch cannot be translated.

Localized README files mirror the canonical structure: the logo, language selector, translated slogan, context quote, badges, product reference, diagrams, commands, safety notes, and license are kept in the same order. Executable commands, links, and badge URLs remain unchanged while surrounding prose is translated.

DeepL's public web endpoint does not expose Bengali (`README.bn.md`), Bosnian (`README.bs.md`), or Thai (`README.th.md`) targets. These files were reviewed by the translator agent using the same preservation checks; a provider supporting those languages can be selected later for an independent linguistic pass.

Translation review must preserve executable commands, relative links, badges, Mermaid syntax, security language, and explicit deferrals. Each translator-agent-reviewed language is marked `Reviewed (translator agent)` here and verified in CI; optional human linguistic sign-off remains distinct.
