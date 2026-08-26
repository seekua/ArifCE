# Indexing

ArifCE uses SQLite and FTS5 for derived entity lookup and lexical retrieval. `.arifce/index/arifce.db` contains an entity table and an FTS virtual table. Raw, cache, and index content are never indexed.

`arifce rebuild` deletes the existing derived database and recreates it from canonical Markdown and JSON files. SQLite connection pooling is disabled for this operation so Windows can replace the database deterministically.

Rebuild failure does not mutate canonical intelligence. `doctor` reports a missing index and recovery tells the user to run `rebuild`. V0.1 does not require a vector database.
