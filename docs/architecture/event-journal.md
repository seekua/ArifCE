# Event Journal

`.arifce/journal/events.jsonl` is append-only during normal operation. Each event occupies exactly one line and contains schema version, event ID, type, UTC time, entity ID, and event data.

Writers append and flush canonical events after entity mutation. Readers tolerate an interrupted partial final line for continuity, but `doctor` reports every corrupt or partial line. Corrupt complete middle lines make normal reads fail with a line number.

`arifce doctor --repair` is explicit mutation. It copies the original journal to `.arifce/backups/journal/`, retains valid JSON events, atomically replaces the journal, rebuilds the derived index, and reports kept/removed counts plus the backup path. Plain `doctor` never repairs.

The journal is a timeline, not the only canonical source. Rebuild scans canonical entity and Markdown files because users may legitimately edit human-readable documents outside an ArifCE command.
