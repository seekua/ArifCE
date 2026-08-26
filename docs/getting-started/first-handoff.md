# First Handoff

Create a checkpoint before handing work to another session or agent:

```bash
arifce checkpoint --summary "Parser implemented; integration test still failing"
arifce handoff
```

The handoff includes current state, latest task, decision, failed attempt, checkpoint, claim, evidence, finding, review, and Git state. It does not load or dump `.arifce/raw/`.

The canonical handoff is saved under `.arifce/handoffs/` and also printed. A successor should read `.arifce/PROTOCOL.md` and `.arifce/CURRENT.md`, then retrieve a targeted projection:

```bash
arifce context "finish parser integration test" --budget 6000
```

Handoffs summarize current engineering state. They are not proof that a claim is correct; inspect linked evidence and freshness.
