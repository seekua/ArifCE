# Security Policy

ArifCE treats imported transcripts as untrusted data. Never place credentials, authentication files, OAuth refresh tokens, SSH private keys, customer secrets, or machine-specific authentication under `.arifce/`. Raw history is excluded from retrieval and Git by default.

Report suspected vulnerabilities privately to the repository owner until a public security contact is configured. Include affected version, reproduction, impact, and suggested mitigation without real secrets. V0.1 secret redaction is defense in depth and does not make unsafe input safe to publish.
