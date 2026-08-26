# Configuration Reference

`.arifce/config.json` is canonical UTF-8 JSON:

```json
{
  "schemaVersion": 1,
  "currentSoftTokenWarning": 4000,
  "currentHardTokenWarning": 8000
}
```

`schemaVersion` is required for compatibility. The token-warning fields document intended `CURRENT.md` limits; V0.1 does not yet enforce those warnings in the CLI. Unknown configuration behavior must not be assumed.

Credentials, vendor authentication, machine paths, and OAuth tokens are prohibited in portable configuration.
