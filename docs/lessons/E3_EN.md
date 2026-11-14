# E3 — Parse a byte stream

Status: MATERIAL_PREPARED / NOT_TAUGHT. Personal mastery: PENDING_LEARNER.

Read the actual implementation: firmware/core/protocol.c and host/PhotoBench.Core/Protocol/FrameCodec.cs.
Walk from input through validation/state/calculation to output; predict a value
before running the corresponding test. Use scripts/tools.ps1 to select the pinned
NET10 executable; the word dotnet below means that executable.

Run: dotnet test host/PhotoBench.Tests --filter FullyQualifiedName~ProtocolTests.
Record the command, real output, changed lines and your explanation. An agent-run
PASS is technical evidence, not evidence that you personally understand it.

Your modification exercise: Corrupt one CRC nibble in a copied fixture; predict the counter and whether sequence advances.
English retelling task: Explain frame, fragment, checksum, discard and resynchronise using actual bytes.

Acceptance: run it yourself, explain the result without copying the answer,
complete the modification and retell in English. Then update LEARNING_PROGRESS.md
with observed evidence; otherwise leave PENDING_LEARNER visible.
