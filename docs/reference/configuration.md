# Configuration Reference

`.arifce/config.json` is canonical UTF-8 JSON:

```json
{
  "schemaVersion": 1,
  "currentSoftTokenWarning": 4000,
  "currentHardTokenWarning": 8000
}
```

`schemaVersion` is required for compatibility. `currentSoftTokenWarning` and `currentHardTokenWarning` define the active-state budget in approximate tokens (the implementation uses four characters per token); `doctor` reports when `CURRENT.md` exceeds these bands, and handoffs apply the configured hard bound to their rendered snapshot. Unknown configuration behavior must not be assumed.

Credentials, vendor authentication, machine paths, and OAuth tokens are prohibited in portable configuration.
