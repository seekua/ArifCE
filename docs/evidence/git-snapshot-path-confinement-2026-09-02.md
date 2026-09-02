# Git snapshot path confinement evidence

Git snapshot hashing now rejects any status path that resolves outside the repository root. A focused regression test proves `../outside.txt` fails before file content is read. [CI run 33636565099](https://github.com/seekua/ArifCE/actions/runs/33636565099) passed Windows, Ubuntu, macOS, and five self-contained targets for commit `29e0e0a`.
