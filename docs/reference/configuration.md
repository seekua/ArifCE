# Configuration Reference

`.arifce/config.json` is canonical UTF-8 JSON:

```json
{
  "schemaVersion": 1,
  "currentSoftTokenWarning": 4000,
  "currentHardTokenWarning": 8000
}
```

`schemaVersion` is required for compatibility. `currentSoftTokenWarning` and `currentHardTokenWarning` document the active-state budget; `doctor` reports when `CURRENT.md` exceeds the default safety bands, and handoffs apply the hard bound to their rendered snapshot. Custom threshold wiring remains a follow-up. Unknown configuration behavior must not be assumed.

Credentials, vendor authentication, machine paths, and OAuth tokens are prohibited in portable configuration.
