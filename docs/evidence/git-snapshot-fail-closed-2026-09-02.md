# Fail-closed Git snapshot evidence

**Status:** implemented and verified locally and remotely.

`GitInspector` now throws when `git status --porcelain=v1 -b` fails. It no longer converts an unreadable repository state into an empty, apparently clean snapshot. This prevents evidence freshness, handoffs, and acceptance-related callers from using an untrusted repository snapshot.

The regression suite proves a non-repository cannot produce a snapshot. LLM and context tests now initialize real Git repositories instead of constructing an empty `.git` directory, so their fixtures exercise the same contract on Windows, Ubuntu, and macOS.

[GitHub Actions run 33629366199](https://github.com/seekua/ArifCE/actions/runs/33629366199) passed the three-OS build/test/package matrix and five self-contained CLI targets for commits `76b0209` and `298b185`.
