# Agent session hooks plan

Automatic capture can reduce the manual ceremony identified in the technical review, but hooks must remain opt-in and local.

## Allowed observations

- Session start/end metadata
- Git changed-file list and commit snapshot
- Explicitly reported command/test exit status
- Agent-supplied task handoff summary

## Safety boundaries

- Never capture prompts, transcripts, environment variables, or credentials by default.
- Never execute arbitrary commands supplied by a hook payload.
- Write through the existing canonical services and journal; do not create a parallel memory store.
- Show what will be installed and provide an uninstall command.
- A hook failure must not block a developer commit unless the user explicitly enables a gate.

## Acceptance criteria

Adapters for Claude Code, Codex, and OpenCode must have fixture payloads, redaction tests, uninstall coverage, and a documented opt-in installation flow before release.
