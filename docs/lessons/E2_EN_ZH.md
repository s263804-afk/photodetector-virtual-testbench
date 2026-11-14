# E2 — Trace an integer moving average

Status: MATERIAL_PREPARED / NOT_TAUGHT. Personal mastery: PENDING_LEARNER.

Read the actual implementation: firmware/core/filter.c and firmware/tests/test_filter.c.
Walk from input through validation/state/calculation to output; predict a value
before running the corresponding test. Use scripts/tools.ps1 to select the pinned
NET10 executable; the word dotnet below means that executable.

Run: scripts/build-host-c.ps1.
Record the command, real output, changed lines and your explanation. An agent-run
PASS is technical evidence, not evidence that you personally understand it.

Your modification exercise: Replace the first three samples with100,300,500; calculate startup outputs before running.
English retelling task: Describe the ring buffer, running sum and floor division. Why can uint32 hold16 full-scale samples?
中文对照：先手算，再运行；说明累加器和起始窗口。

Acceptance: run it yourself, explain the result without copying the answer,
complete the modification and retell in English. Then update LEARNING_PROGRESS.md
with observed evidence; otherwise leave PENDING_LEARNER visible.
