# Project File Layout

At the repository root, `README.md` is the canonical English entry point. Translated entry points live under `docs/locales/` (for example, `docs/locales/README.tr.md`) so the root stays easy to scan.

```text
.arifce/
  README.md              store guide
  PROJECT.md             stable project facts
  CURRENT.md             compact active state
  PROTOCOL.md            agent behavior
  config.json            schema-versioned configuration
  memory/                architecture, conventions, domain, integrations, issues, glossary
  decisions/ tasks/ attempts/ checkpoints/
  claims/ evidence/ reviews/ findings/
  refactors/ handoffs/ runs/ threads/
  journal/events.jsonl   append-only canonical events
  raw/                   untrusted, never bulk-loaded
  cache/                 disposable
  index/arifce.db         disposable SQLite/FTS5 index
```

Canonical entity files are UTF-8 JSON with kebab-case filenames equal to the lower-cased entity ID, and contain `schemaVersion`. Human-maintained project and memory documents are Markdown. Events are one JSON object per line. Temporary writes use a sibling temporary file followed by atomic replacement where the platform permits.

`cache/` and `index/` are derived and should be ignored by Git. Canonical files may be committed. `raw/` is opt-in and must be reviewed for sensitive data before commit. Deleting derived directories must not lose intelligence.
