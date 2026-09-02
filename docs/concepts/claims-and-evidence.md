# Claims and Evidence

An agent statement such as “tests pass” is a claim, not a project fact. Evidence records what was actually observed: command, exit code, structured counts when available, output summary, time, environment, Git snapshot, and optional file/directory dependency scope.

Evidence supports only the statement its check can establish. A passing unit-test command does not prove security, API compatibility, or unrelated acceptance criteria. Generic and architecture scopes hash file content; API and SQLite adapters hash their normalized public surface or schema so implementation bytes and database rows do not create false stale states. Legacy and unscoped evidence conservatively becomes stale when the repository snapshot changes.

Claims retain historical evidence IDs, including observations that later became stale. Freshness and acceptance use current evidence rather than requiring every historical observation to remain current. Re-verification can therefore restore support without deleting the old evidence trail; an acceptance records only the evidence that was current when it was approved.
