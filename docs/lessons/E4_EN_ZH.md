# E4 — Follow one FEED command

Status: MATERIAL_PREPARED / NOT_TAUGHT. Personal mastery: PENDING_LEARNER.

Read the actual implementation: firmware/core/device.c → sample_source_push → filter_push.
Walk from input through validation/state/calculation to output; predict a value
before running the corresponding test. Use scripts/tools.ps1 to select the pinned
NET10 executable; the word dotnet below means that executable.

Run: dotnet test host/PhotoBench.Tests --filter Category=ARM.
Record the command, real output, changed lines and your explanation. An agent-run
PASS is technical evidence, not evidence that you personally understand it.

Your modification exercise: Add a two-sample vector and calculate raw:filtered:flags; resend the identical sequence.
English retelling task: Explain why duplicate FEED is replayed and a changed duplicate is rejected.
中文对照：沿代码走读状态、序号、缓存和滤波调用。

Acceptance: run it yourself, explain the result without copying the answer,
complete the modification and retell in English. Then update LEARNING_PROGRESS.md
with observed evidence; otherwise leave PENDING_LEARNER visible.
