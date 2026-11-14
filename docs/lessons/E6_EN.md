# E6 — Recover without inventing outcomes

Status: MATERIAL_PREPARED / NOT_TAUGHT. Personal mastery: PENDING_LEARNER.

Read the actual implementation: host/PhotoBench.Core/Session/DeviceSession.cs and Diagnostics/CommunicationChecks.cs.
Walk from input through validation/state/calculation to output; predict a value
before running the corresponding test. Use scripts/tools.ps1 to select the pinned
NET10 executable; the word dotnet below means that executable.

Run: dotnet test host/PhotoBench.Tests --filter FullyQualifiedName~SessionTests.
Record the command, real output, changed lines and your explanation. An agent-run
PASS is technical evidence, not evidence that you personally understand it.

Your modification exercise: Change the unit-test timeout and inspect identical retry bytes in the trace; restore it afterwards.
English retelling task: Explain why a connected socket does not prove a live application session. What is indeterminate?

Acceptance: run it yourself, explain the result without copying the answer,
complete the modification and retell in English. Then update LEARNING_PROGRESS.md
with observed evidence; otherwise leave PENDING_LEARNER visible.
