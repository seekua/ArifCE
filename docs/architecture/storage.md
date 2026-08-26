# Storage

Canonical project intelligence is stored under `.arifce/` as Markdown, JSON, and JSONL. Entity files use schema version `1`, UTF-8, lower-case filenames derived from their uppercase IDs, and safe temporary-write replacement.

`CanonicalStore` owns entity reads, writes, and monotonic local ID allocation. It writes the temporary sibling first, then replaces the target. Existing initialization documents are never overwritten by `init` or `adopt`.

SQLite under `.arifce/index/` is derived. Cache, index, and raw data are excluded from Git by default. Deleting derived data must not delete decisions, tasks, attempts, claims, evidence, reviews, findings, refactors, checkpoints, or handoffs.

Current canonical entity payloads are JSON rather than YAML to keep V0.1 on `System.Text.Json` and avoid an additional parser dependency. Human-maintained project state remains Markdown.
