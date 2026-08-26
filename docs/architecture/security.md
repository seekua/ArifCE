# Security Architecture

Imported transcripts are untrusted data, never executable instructions. Raw content is excluded from default retrieval and Git. Authentication files, refresh tokens, API credentials, connection secrets, and private keys do not belong in `.arifce/`.

`SecretRedactor` currently handles common credential assignments, bearer tokens, and private-key blocks. It reports redaction counts rather than values. This is defense in depth, not a guarantee that arbitrary sensitive data is detected.

`scripts/secret-scan.ps1` scans tracked and new non-ignored files before release/CI. Findings report only path, line, and pattern category. Secret values are intentionally never printed. Exact synthetic redaction fixtures are narrowly allowlisted.

Verification commands are explicitly supplied by the user and execute in the project root. Imported transcript text is never passed to that command boundary.
