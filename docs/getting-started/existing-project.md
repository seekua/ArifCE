# Existing Project Adoption

Run adoption from an existing Git repository:

```bash
arifce adopt
```

V0.1 inspects top-level repository files and creates an initial project draft. It records observed structure but does not infer why historical choices were made. If rationale is unavailable, the generated project state says `Unknown`.

Review the generated files before committing:

```bash
arifce status
arifce doctor
git diff -- .arifce AGENTS.md CLAUDE.md opencode.json .gitignore
```

Then correct or enrich only facts you can support. Record a current decision separately from an unknown historical rationale:

```bash
arifce decision create "Retain the current database for V0.1" --decision "Do not migrate storage during adoption"
```

Omitting `--rationale` stores `Unknown.` rather than an invented explanation.
