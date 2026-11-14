# Execution ledger — plan: docs/superpowers/plans/2026-11-12-photodetector-plan.md

Executor: inline; read-only review at whole-project completion.
Software S1–S7 completed technically before E1 began. See the independent software repository.

Pre-flight: filter output feeds device state; parser emits validated frames without executing
commands; C/C# share a frozen wire grammar and golden vectors; host statistics use an
independent reference; scenario reports must include interruption/session boundaries.

E1 VERIFIED: exact tool versions resolved, supported NET10 host compiled, official upstream
demo labelled third-party, project-owned ARM ELF compiled and executed in Renode,
raw local UART responded ARM_SMOKE_OK. Socket binding is 127.0.0.1.

Ruling: use a minimal project-owned STM32F4 UART subset with F407VG memory bounds,
not an inaccurate claim of exact full-board support. Cost: unmodelled peripherals remain gaps.
Ruling: use Renode's vendor runtime, separate from supported NET10 host; a NET10 direct
launcher failed with mixed dependencies. Cost: old bundled simulator runtime needs upstream update.
Ruling: stage execution artifacts on a no-space path; preserve source workspace and ELF hash.
Cost: a staging-copy step is needed; no code or samples bypass the ARM firmware.

Actual E1 incidents: linker -Wl map path split on workspace comma; fixed with -Xlinker.
Renode path tokenisation rejected the workspace path; fixed by staging. Relative file backend
output needed an explicit staged absolute path. All corresponding failed logs are retained.
The final smoke ran successfully; its exact ELF hash and startup/UART logs are evidence.

E2 IN_PROGRESS: filter tests first failed against the stub; implementing bounded ring buffer.

E2 VERIFIED: filter RED→GREEN; one CTest executable, 245 assertions. CMake object library avoids unavailable standalone AR when using zig cc. NET10 solution scaffold build: zero warnings/errors; not counted as functional tests.

E3 VERIFIED: C protocol 89 assertions and NET protocol 17 tests, RED→GREEN;
independent CRC golden bytes and streaming/drop/recovery cases exercised.
E4 VERIFIED: device C 221 assertions; full CTest 3/3. Actual project ARM ELF executed
HELLO/START/PING/FEED/replay/STATUS/STOP with exact numerical output and count3.
ELF SHA256 138d93321b3968a59178e2f0d839ccfe5100cee4558fe41aeae14f7e27ad5241.
Evidence e4-arm-integration.log, e4-arm-protocol.json, e4-renode-smoke.log.
Ruling: sample_source_push takes a Device instance to keep independently testable state;
same explicit virtual acquisition boundary, no ADC substitution claim.
E5 IN_PROGRESS: independent Python noisy vector saved; model/analysis tests first.

E5 VERIFIED: NET26 tests after model/statistics RED→GREEN. Saved noise/reference has
1024 raw and1009 settled samples. Exact fixture hash is versioned beside the vector.
E6 VERIFIED: unit retry/cancellation/session tests plus actual C# TCP↔ARM checks.
Real lost-reply, identical FEED replay, corrupt CRC, overflow, fragmented delivery,
all3 lost replies, post-write cancellation, socket disconnect, retained-TCP reset,
and new HELLO were observed. NET37/37 at E6.
Actual E6 bug: machine Reset cleared VectorTableOffset and started PC0/SP0.
RED actual integration evidence preserved; reset macro now reloads project ELF and
restores vector address, matching Renode's supported reset pattern. Same test GREEN.
Reference: https://renode.readthedocs.io/en/latest/basic/machines.html and shipped
scripts/single-node/stm32f4_discovery.resc. No host-side sample/state simulation added.
E7 VERIFIED: six scenario groups,3968 returned ARM sample tuples, JSON/CSV/HTML,
actual metadata and configured limits, missing/interrupted scenario guard. NET46/46.
Ruling: saturation runs before linearity so the excluded60uW point is observed,
not invented. Cost: report order differs from the design's explanatory numbering.
Ruling: an external simulator without reset control cannot give overallPASS;
--manage-simulator is required for the fully verified automatic run.
E8 IN_PROGRESS: pinned setup, full/fresh reproduction, portfolio/lessons, final review.

E8 REPRODUCTION VERIFIED: setup hash checks/locked restore; full46 NET tests, CTest3/3, ARM build, release build0warnings/errors, all6 CLI groups3968tuples. Fresh source/venv/build reproduction passed the same checks; archives/OS/basePython/NuGet cache reused, explicitly scoped. Host UBSan device221 assertions passed. Final whole-branch review pending.

Final review: independent fresh reviewer; no Critical, two Important, one Minor.
Final: fixed connection refusal losing the whole report — RefusedEndpointProducesFailureReportBeforeAnyTransmission and RefusalAfterCompletedScenarioPreservesItsSamplesAndTrace RED→GREEN.
Final: fixed interrupted communication losing completed checks/session/trace — CommunicationCancellationPreservesCompletedChecksAndInterruptedSession RED→GREEN.
Final verification: full49/49, fresh49/49, CTest3/3 and six-group CLI PASS; no skips.
Actual verification incident: concurrent Renode processes contended on global history;
full suite failed before private --config/history isolation and passed after it.
Final: minor (deferred): strict exact/unique HELLO protocol/mode field parsing; current
project firmware advertises exact values, other firmware compatibility remains limited.
Final: Ruling: physical ADC/analog/timing, remote CI execution, clean OS and personal
mastery are unvalidated evidence boundaries, not silently completed tasks.
E8 technically complete; publication remains a separate user-authorized external step.
