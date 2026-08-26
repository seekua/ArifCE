# First Verification

Create a precise claim after implementation:

```bash
arifce claim create "The parser test suite passes"
arifce verify CLAIM-0001 --command "dotnet test tests/Parser.Tests/Parser.Tests.csproj"
arifce claim status CLAIM-0001
```

ArifCE records the command, exit code, bounded output summary, Git snapshot, timestamp, and structured test counts when the .NET summary is recognized. Failed commands contradict the claim. A passing command supports only what that command actually checks.

Review findings can be linked without turning model agreement into truth:

```bash
arifce finding create "Missing malformed-input test" --description "No regression case covers truncated frames" --severity HIGH --task TASK-0001 --path tests/Parser.Tests
arifce review record CLAIM-0001 --reviewer claude --verdict INCONCLUSIVE --summary "Core tests pass, but malformed input is uncovered" --finding FINDING-0001
```

Positive review agreement never promotes a claim to `VERIFIED`. `DISAGREE` can move an eligible claim to `DISPUTED`. High-risk policy requires independent review; critical policy also requires human approval.
