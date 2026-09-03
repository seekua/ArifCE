# Benchmark host-process timing

## Scope and decision

The existing `durationMs` measures preparation through repository evaluation, including queue/approval delay. It is not an agent speed measurement. Phase 67 adds a separate host-process stopwatch and deliberately leaves active model work unknown (ADR-0014, TASK-0017).

The runner launches a user-selected executable with an argument array, streams stdout and stderr concurrently, sends the prompt on stdin, bounds runtime, and preserves nonzero exit codes. It stores stopwatch ticks/frequency and hashes of session, prompt, stdout, and stderr. Completion derives elapsed milliseconds and verifies the capture again; collection preserves null totals for incomplete timing coverage. Trial schema advances to 3 and suite schema to 4, without changing canonical entity schemas.

## Local tests

`test-engineering-benchmark-timing.ps1` exercises real local PowerShell fixture processes: stdin delivery, checkout working directory, literal argument boundaries, concurrent output exceeding pipe buffers, a known delay, nonzero exit, timeout termination, overwrite rejection, changed hashes/counters, invented active-work values, absent legacy timing, and complete/partial aggregates. `test-engineering-benchmark-telemetry.ps1` remains green. All 83 behavior tests pass.

The elapsed assertion checks a known delay against an outer stopwatch, not a tight machine-dependent performance threshold. These are fixture-process measurements, not a live model benchmark or a speedup result.

`test-engineering-benchmark-completion.ps1` also passes locally: a captured fixture process is bound to a real isolated Git/.NET trial, altered elapsed time is rejected, and the existing token/evaluator provenance checks remain green. `test-engineering-benchmark-suite.ps1` passes matched preparation and incomplete-suite rejection. Remote CI proof is pending.

## Boundaries

- Host elapsed time includes startup, internal model/network queueing, and any internal approval waits; it is not CPU time or active model time.
- The existing preparation-based duration remains readable and is not retroactively reinterpreted.
- A persistent reservation prevents repeated/concurrent capture. Interrupted captures fail closed and retain raw artifacts; a separately identified retry and explicit failure accounting are required.
- The executable runs with the caller's permissions. This wrapper is not a sandbox, model selector, or token-budget enforcer.
- Logs may contain private data. No raw fixture/model transcripts are published here. Hashes enforce consistency, not authenticity against an operator controlling every artifact.
- Equal permission setup, active-wait segmentation, interrupted-run scoring automation, and new real A/B results are not claimed complete.
