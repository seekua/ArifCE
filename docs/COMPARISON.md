# ArifCE in context

ArifCE is a repository-local continuity and evidence layer for AI-assisted engineering. It does not replace an agent's native instruction file or require a hosted memory service.

| Approach | Strength | ArifCE difference |
| --- | --- | --- |
| `CLAUDE.md` / `AGENTS.md` | Simple, familiar instructions | Adds canonical decisions, attempts, claims, evidence, freshness, and acceptance lifecycle. |
| Hosted agent memory | Automatic capture and broad retrieval | ArifCE keeps canonical state in the repository, works offline, and makes Git history the ownership boundary. |
| Generic notes/wiki | Human-readable project knowledge | ArifCE records provenance, repository snapshots, deterministic checks, and stale evidence explicitly. |
| LLM gateway | Provider routing and model execution | ArifCE's local provider layer is optional; the core product remains useful without a vendor account. |

## What ArifCE is strongest at

The differentiator is the claim lifecycle: a completion statement can remain `UNVERIFIED`, gain deterministic evidence, be reviewed, accepted by an explicit actor, and become `STALE` when the repository changes. This is deliberately stricter than storing an agent transcript or a free-form memory blob.

## What other tools may do better

Specialized tools may provide deeper semantic embeddings, automatic session capture, AST-level code graphs, or turnkey hosted collaboration. Those capabilities are outside ArifCE's local-first core and can be evaluated as optional integrations rather than implied guarantees.

## Positioning

ArifCE does not compete with `CLAUDE.md` or `AGENTS.md`; it sits underneath them as an inspectable, repository-owned evidence layer. It also does not promise that an agent is correct. It records what was claimed, what was checked, who accepted it, and when that evidence stopped matching the code.
