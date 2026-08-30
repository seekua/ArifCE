# Semantic embeddings plan

The current `DeterministicHashEmbeddingProvider` is a deterministic test stub. It must not be used as semantic similarity and is not presented as one.

## Acceptance criteria

- A local provider maps related phrases closer than unrelated phrases on a checked-in fixture set.
- The provider works without a hosted service or API key.
- Model license, size, download, cache, and upgrade behavior are documented.
- Vector dimensions and similarity calculations are deterministic for a pinned model version.
- Lexical FTS remains the explainable fallback when the model is unavailable.
- Benchmarks report retrieval precision/recall and latency separately; no unsupported quality percentage is claimed.

## Implementation boundary

The first implementation should be an opt-in local provider behind the existing selector. It must not change canonical records or make cloud synchronization a prerequisite. A model-backed provider will be enabled only after fixture-based tests and cross-platform packaging evidence pass.
