# MCP boundary evaluator calibration

## Verdict and scope

TASK-0026 addresses the ninth evaluator objective in FINDING-0005. The prior evaluator asserted selected JSON-RPC error phrases, but did not prove malformed requests left canonical records and the journal unchanged, did not test duplicate writes, and did not prove a valid MCP write follows the normal domain path.

Three replacement tests run the real stdio `ArifCE.Mcp.dll` against a temporary Git repository. They prove malformed enum, path-like ID, non-string required field, unknown argument and missing linked task calls return errors without canonical or journal byte changes; an oversized line is rejected before JSON parsing; duplicate decisions do not create a second record; and valid task/attempt calls persist linked domain records.

## Calibration controls

Source is pinned at `a7b4d828205ad8ebaf06efc66a11ed18e0ec9158`. Run `./scripts/test-engineering-benchmark-mcp-calibration.ps1 -SourceCommit <commit>`.

| Control | Required result |
| --- | --- |
| Unmodified implementation | PASSED |
| Fall back from an invalid risk enum | FAILED |
| Accept a path-like ID | FAILED |
| Accept unknown arguments | FAILED |
| Accept a non-string required field | FAILED |
| Remove the request-size limit | FAILED |

All local controls passed: 111 product tests, registry rejection checks, independent completion provenance smoke and good/five-mutant calibration. Remote CI evidence remains pending publication authorization.

## Limits

This establishes representative local stdio boundary behavior, not multi-user authorization, every schema, process-crash atomicity, concurrent MCP-server behavior, hostile environment ownership or every JSON resource-exhaustion pattern. No product-effectiveness claim follows.
