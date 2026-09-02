# Task-risk parsing integrity evidence

**Status:** implemented and locally verified; remote CI evidence is added after the commit is observed in the three-OS matrix.

## Problem

`task create` previously accepted the remaining command-line arguments as title text. A user who wrote `task create "Harden parser" --risk HIGH` could create a task whose canonical title included `--risk HIGH`, while its risk silently remained `MEDIUM`.

## Implemented contract

```text
arifce task create <title> [--risk <LOW|MEDIUM|HIGH|CRITICAL>]
```

- Risk defaults to `MEDIUM` when the option is absent.
- `--risk` is accepted only after the complete title and only with one supported value.
- Unknown, misplaced, or incomplete options fail before `CreateTaskAsync` is called.
- The packaged smoke fixture creates a `HIGH`-risk task, verifies its canonical title and risk after completion, and proves an unsupported option exits with an error.

## Local proof

- `dotnet build ArifCE.slnx -c Release --no-restore -m:1` completed with 0 warnings and 0 errors.
- `dotnet test ArifCE.slnx -c Release --no-build --disable-build-servers -m:1` passed.
- `pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/package-smoke.ps1` passed, including the valid-risk and rejected-option checks against the packed global tool.

This is command-line parsing integrity, not a new task-domain concept. The canonical record remains the source of truth.
