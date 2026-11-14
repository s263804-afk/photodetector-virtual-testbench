# E1 — Compile and execute firmware

Status: MATERIAL_PREPARED / NOT_TAUGHT. Personal mastery: PENDING_LEARNER.

Read the actual implementation: firmware/platform/stm32/startup.S → uart.c → smoke.c.
Walk from input through validation/state/calculation to output; predict a value
before running the corresponding test. Use scripts/tools.ps1 to select the pinned
NET10 executable; the word dotnet below means that executable.

Run: scripts/build-firmware.ps1; .venv/Scripts/python.exe scripts/e1-smoke.py.
Record the command, real output, changed lines and your explanation. An agent-run
PASS is technical evidence, not evidence that you personally understand it.

Your modification exercise: Change the smoke reply text, rebuild, and find the changed bytes in the UART log.
English retelling task: Explain compile, link, load and emulate. Which evidence proves ARM instructions ran?

Acceptance: run it yourself, explain the result without copying the answer,
complete the modification and retell in English. Then update LEARNING_PROGRESS.md
with observed evidence; otherwise leave PENDING_LEARNER visible.
