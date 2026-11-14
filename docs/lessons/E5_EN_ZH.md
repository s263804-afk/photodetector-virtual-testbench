# E5 — Convert optical power and analyse noise

Status: MATERIAL_PREPARED / NOT_TAUGHT. Personal mastery: PENDING_LEARNER.

Read the actual implementation: host/PhotoBench.Core/Optics/OpticalModel.cs and Analysis/MeasurementAnalysis.cs.
Walk from input through validation/state/calculation to output; predict a value
before running the corresponding test. Use scripts/tools.ps1 to select the pinned
NET10 executable; the word dotnet below means that executable.

Run: dotnet test host/PhotoBench.Tests --filter FullyQualifiedName~OpticsTests.
Record the command, real output, changed lines and your explanation. An agent-run
PASS is technical evidence, not evidence that you personally understand it.

Your modification exercise: Calculate the voltage and code at5microwatts; then generate it and compare.
English retelling task: Explain responsivity, transimpedance, offset, clipping and n-minus-one standard deviation.
中文对照：单位先换成瓦；说明量化和原始/滤波噪声。

Acceptance: run it yourself, explain the result without copying the answer,
complete the modification and retell in English. Then update LEARNING_PROGRESS.md
with observed evidence; otherwise leave PENDING_LEARNER visible.
