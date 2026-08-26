# Claims and Evidence

An agent statement such as “tests pass” is a claim, not a project fact. Evidence records what was actually observed: command, exit code, structured counts when available, output summary, time, environment, and Git snapshot.

Evidence supports only the statement its check can establish. A passing unit-test command does not prove security, API compatibility, or unrelated acceptance criteria. Repository changes can make earlier evidence stale.
