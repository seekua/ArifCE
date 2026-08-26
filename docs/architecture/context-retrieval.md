# Context Retrieval

V0.1 retrieval is deterministic and explainable. The CLI extracts up to twelve distinct alphanumeric task terms, forms an FTS5 OR query, orders candidates by FTS rank with stable path behavior, estimates tokens as `ceil(characters / 4)`, and includes candidates that fit the positive budget.

Output names every source, estimate, lexical inclusion reason, score, and total estimate. The total never intentionally exceeds the requested budget. Estimates are not tokenizer-exact measurements.

Raw transcripts are excluded. Retrieval is lexical rather than semantic-vector based and makes no token-reduction percentage claim. Benchmark work must compare task success and irrelevant context, not only byte counts.
