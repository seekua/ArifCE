# README translation status

The English `README.md` is canonical. Every localized README currently retains the complete canonical sections, commands, links, badges, license, and safety notes. The parity check is enforced by `scripts/check-readme-locales.ps1` in CI.

| File | Scope status | Human translation review |
| --- | --- | --- |
| `README.ar.md` | Full Arabic draft (commands/links preserved) | Pending |
| `README.bn.md` | Complete canonical reference | Pending |
| `README.bs.md` | Complete canonical reference | Pending |
| `README.da.md` | Complete canonical reference | Pending |
| `README.de.md` | Full German draft (commands/links preserved) | Pending |
| `README.el.md` | Complete canonical reference | Pending |
| `README.es.md` | Full Spanish draft (commands/links/diagram preserved) | Pending |
| `README.fr.md` | Full French draft (commands/links preserved) | Pending |
| `README.it.md` | Full Italian draft (commands/links/diagram preserved) | Pending |
| `README.ja.md` | Full Japanese draft (commands/links/diagram preserved) | Pending |
| `README.ko.md` | Full Korean draft (commands/links/diagram preserved) | Pending |
| `README.no.md` | Full Norwegian draft (commands/links/diagram preserved) | Pending |
| `README.pl.md` | Full Polish draft (commands/links/diagram preserved) | Pending |
| `README.pt-BR.md` | Full Brazilian Portuguese draft (commands/links/diagram preserved) | Pending |
| `README.ru.md` | Complete canonical reference | Pending |
| `README.th.md` | Complete canonical reference | Pending |
| `README.tr.md` | Complete canonical reference | Pending |
| `README.uk.md` | Complete canonical reference | Pending |
| `README.vi.md` | Complete canonical reference | Pending |
| `README.zh-CN.md` | Complete canonical reference | Pending |
| `README.zh-TW.md` | Complete canonical reference | Pending |

The repository includes `scripts/translate-readme-locales.ps1`, which performs paragraph-level DeepL web translation in batches while protecting commands, URLs, HTML, and Markdown links. The public endpoint currently rate-limits automated requests; the script retries and leaves the existing file unchanged when a batch cannot be translated.

Localized README files intentionally omit the pre-badge translated-introduction, duplicate slogan, and quick-start block. These are maintained only in the canonical English README; localized files continue with the canonical product reference after the shared language selector.

DeepL's public web endpoint does not expose Bengali (`README.bn.md`), Bosnian (`README.bs.md`), or Thai (`README.th.md`) targets. These three files therefore remain canonical-reference files until a provider supporting those languages is explicitly selected. No language is marked `Reviewed` until its complete translation is verified in CI.

Human translation work must preserve executable commands, relative links, badges, Mermaid syntax, security language, and explicit deferrals. Each reviewed language should be marked `Reviewed` here and verified in CI.
