# Documentation policy

Documentation is part of the product contract. Any change to commands, schemas, MCP tools, dashboard endpoints, package versions, or user-visible behavior must update `docs/USER-GUIDE.md` and the closest reference document in the same commit.

Before merge or release:

1. Compare changed behavior with the user guide and CLI/MCP references.
2. Run the affected examples or record why an example cannot run.
3. Check version numbers, links, and explicit deferrals.
4. Include documentation parity in the release checklist.

Never document a planned or deferred capability as shipped.
