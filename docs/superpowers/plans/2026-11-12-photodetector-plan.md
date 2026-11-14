# Photodetector Test Bench Implementation Plan

> **For agentic workers:** Use `superpowers:executing-plans` to implement this plan task by task. Where unavailable, apply the same sequential build/test/evidence workflow. No subagent implementation is selected by this work order.

**Goal:** Execute STM32-target firmware in Renode and verify a concrete photodetector-monitoring workflow using C#, while teaching each engineering step English-languagely.

**Architecture:** A C# tool sends protocol commands and explicitly virtual sample data to ARM C firmware through an emulated UART. The firmware processes samples; independent C# references check results and produce traceable reports.

**Tech Stack:** C11, GNU Arm toolchain, CMake, Renode STM32F4 platform, supported C#/.NET 10 LTS patch, automated host and emulator tests.

**Spec:** `../specs/2026-11-12-photodetector-design.md`.

## Global Constraints

- Independent repository: `projects/photodetector-virtual-testbench/`; no physical board required.
- C on the ARM target; C# on the host.
- A real ARM ELF must run in Renode; virtual sample injection must be labelled.
- 12-bit codes 0–4095, 16-sample integer moving average, bounded 512-byte LF frames, CRC-16/CCITT-FALSE.
- See the design for exact optical constants, state transitions, protocol sequence/replay rules and scenario limits.
- Raw and filtered metrics, model time and wall time, host tests and ARM integration remain distinct.
- No hardware validation or high-speed optical claims; no automatic external publication.

## Review Focus

1. Serial byte streams may split frames or combine several frames in one read — E3/E5.
2. A lost response followed by a duplicate FEED must not process samples twice — E4/E6.
3. Invalid samples or frames must not partially mutate filter state — E2/E3/E4.
4. Reset with TCP retained, exhausted timeouts and post-send cancellation must invalidate the old session and become visible in reports — E4/E6/E7.
5. Clipping and integer quantisation can invalidate naive linearity/noise claims — E5/E7.

## E1 — Prove the simulator path

**Files:** Create `docs/environment.md`, `docs/platform-support.md`, `firmware/platform/stm32/`, build files, `simulation/platform.resc`, `scripts/build-firmware.ps1`, `scripts/run-simulator.ps1`, `reports/emulator-smoke.md`.

**Interfaces:** Produces an ARM ELF plus a reproducible load/run command and an identified UART endpoint. Do not treat a downloaded demo ELF as the completed project.

- [ ] Inspect Arm compiler, CMake, host C compiler, .NET, Renode and target-platform files; record exact paths/versions.
- [ ] Read the chosen Renode board description and official STM32 reference material; record actual MCU, memory map and UART.
- [ ] Run the official demo only as an environment check and label it as third-party.
- [ ] Build a minimal project-owned firmware smoke program with UART startup output, load the ARM ELF in Renode and capture its output.
- [ ] Configure the UART network terminal without negotiation bytes, verify local connectivity and record any clock/peripheral limitations.
- [ ] If blocked, investigate and record the exact unsupported register/model behaviour; preserve independent tasks but do not report ARM success.
- [ ] Teach compile, link, load, emulate, boot and inspect. Save lesson, evidence and a local commit.

## E2 — Portable processing core

**Files:** Create `firmware/core/filter.c,h`, `firmware/tests/test_filter.c`, host-C test target and `fixtures/filter-golden.json`.

**Interfaces:** `filter_init(Filter*)`; `filter_push(Filter*, uint16_t raw, uint16_t *filtered) -> bool`; default window 16; invalid input leaves state unchanged. `uint16_t` still permits values 4096–65535 which must be rejected.

- [ ] Write failing tests: [100,200,300] → [100,150,200]; sixteen 4095 values remain 4095; input 4096 rejected without changing the next valid output.
- [ ] Write window-rollover test: inputs 0–16 give last output 8; reset returns startup behaviour.
- [ ] Add the optical step vector: 64 values 124 followed by 64 values 869; first step output 170, 16th changed output 869.
- [ ] Implement the bounded ring buffer and `uint32_t` sum; no dynamic allocation.
- [ ] Run host-C tests, including available compiler warnings/sanitizers. Record compiler support; do not claim unavailable checks.
- [ ] Teach buffer, accumulate, overflow, smooth out and settle. Learner changes a vector and computes its expected first outputs. Commit.

## E3 — Protocol and streaming parser

**Files:** Create `docs/protocol.md`, `firmware/core/protocol.c,h`, `firmware/tests/test_protocol.c`, `host/PhotoBench.Core/Protocol/`, `.NET codec tests`, shared golden byte fixtures.

**Interfaces:** C `protocol_feed(parser, bytes, length)` and C# `FrameCodec.Encode(Frame)` / `FrameParser.Push(ReadOnlySpan<byte>)`; agree on exact returned frame types before implementation. No state-changing command execution in the parser.

- [ ] Freeze the exact request/response grammar and error/drop rules from the specification.
- [ ] Write CRC test `123456789` → 0x29B1 independently in C and C#.
- [ ] Write tests for split frame at every byte boundary, two frames in one read, bad CRC, invalid hex, missing fields, non-ASCII, empty argument field and 513-byte overflow followed by a good frame.
- [ ] Test signed input, non-integer, code 4096, empty FEED and 17-sample FEED; reject before a batch can mutate state.
- [ ] Implement bounded streaming parsers and codecs; create at least three cross-language golden frames with independently checked expected bytes.
- [ ] Run host C/.NET tests, inspect emitted bytes and save evidence.
- [ ] Teach frame, checksum, fragment, parse, discard and resynchronise. Learner corrupts a fixture and predicts the observed failure. Commit.

## E4 — State, sequence and actual firmware integration

**Files:** Create `firmware/core/device.c,h`, core state tests; modify STM32 UART adapter and entry point; create `simulation/smoke.robot` or an equivalent documented headless test.

**Interfaces:** `device_handle(Device*, const Frame*, Response*)` consumes a fully validated frame and uses `filter_push`; `sample_source_push` applies an entirely validated 1–16 item batch. UART adapter connects the core to the verified platform.

- [ ] Write tests: FEED before START → `ERR_STATE`, START clears history/count, STOP rejects FEED, invalid SET_LIMITS preserves old limits.
- [ ] Test sequence consumption on semantic error, cached duplicate FEED response, changed duplicate payload rejection, unexpected sequence rejection and wrap 65535→1.
- [ ] Test HELLO(0) resets STOPPED state and replay cache, and next expected sequence is 1.
- [ ] Test boot/reset enters NEEDS_HELLO; CRC-valid non-HELLO returns ERR_SESSION without processing. START/SET_LIMITS while RUNNING return ERR_STATE; STOP in STOPPED is a successful no-op.
- [ ] Implement the device state machine, bounded response generation and UART integration.
- [ ] Cross-compile the actual project, load it in Renode and execute HELLO→START→FEED→STATUS→STOP; verify numerical output against E2 vectors.
- [ ] Capture ELF hash, compiler flags, platform version and UART log.
- [ ] Teach transition, initialise, reject, acknowledge and replay. Learner traces one command from bytes to response. Commit.

## E5 — Optical model and independent reference

**Files:** Create `host/PhotoBench.Core/Optics/`, `Analysis/`, model/reference tests, `fixtures/optical/`, `docs/model.md`.

**Interfaces:** `OpticalModel.Generate(ScenarioConfig) -> SampleVector`; `ReferenceFilter.Process(samples) -> expected samples`; `Analysis.Summarise(raw, filtered, config) -> MeasurementSummary`. Do not call firmware filter code to generate expected values.

- [ ] Write model tests: 0 uW→124, 10 uW→869, 60 uW→4095 with sigma=0; negative optical power is a configuration error.
- [ ] Test `n-1` standard deviation with known voltages [1,2,3] → mean 2, std 1; fewer than two values → explicit insufficient-data result.
- [ ] Test settled step results, rail flags and excluded clipped points; linear fit requires at least three valid distinct power levels.
- [ ] Implement the model, independent moving-average reference, voltage conversion and measurement summaries.
- [ ] Save exact deterministic/noisy vectors, metadata and hashes. Use reference fixture results checked separately from production analysis code.
- [ ] Run .NET tests and teach responsivity, gain, offset, clip, subtract and fit. Learner converts one microwatt input to voltage and code. Commit.

## E6 — C# transport and fault recovery

**Files:** Create `host/PhotoBench.Core/Transport/`, `Session/`, session tests, `.NET emulator integration tests`, fault-injection proxy helper and protocol trace output.

**Interfaces:** `IByteTransport.ConnectAsync`, `ReadAsync`, `WriteAsync`, `DisposeAsync`; `DeviceSession.ExecuteAsync(Command, CancellationToken)`; implement TCP transport for the emulated UART. Mock transport is for unit tests only.

- [ ] Write async tests for 2000 ms configurable timeout, at most three attempts using the exact same frame bytes, cancellation and unrelated response-sequence rejection. Use injected time where practical.
- [ ] Drop all three responses after a command may have executed: assert INTERRUPTED_INDETERMINATE, closed/invalid host session and no further command until reconnect+HELLO.
- [ ] Test cancellation before write preserves the session; cancellation during/after write invalidates/closes it and prohibits automatic cross-session replay.
- [ ] Add transport fragmentation/coalescing, dropped response after successful FEED, bad-CRC frame and connection interruption tests.
- [ ] Implement transport/session handling; document every retry and reset in machine-readable traces.
- [ ] Run real Renode integration: lost FEED response then retry must return cached data and increase processed count only once.
- [ ] Reset firmware during a test while retaining the UART TCP connection: next valid non-HELLO receives ERR_SESSION; host marks interruption, closes/invalidates the session, reconnects with HELLO and confirms STOPPED/expected sequence. Also verify the disconnect path.
- [ ] Send overflow garbage followed by valid PING and prove recovery.
- [ ] Teach time out, retry, reconnect, recover from and rule out. Learner explains transport success versus application success. Commit.

## E7 — Automated characterisation and report

**Files:** Create `host/PhotoBench.Cli/`, `Reporting/`, scenario runner tests, `scripts/demo.ps1`, `reports/example/`, `docs/test-plan.md`.

**Interfaces:** `photo-bench run --scenario all --transport tcp --host 127.0.0.1 --port 12345 --output ...`; produces JSON, CSV and HTML with explicit session/scenario/model metadata.

- [ ] Write report-schema tests requiring firmware version/hash, host version, config, fixture hashes, session IDs, actual metrics, limits, verdict and simulation disclaimer.
- [ ] Test any interrupted/missing/unverified scenario cannot produce overall PASS; skipped optional scenarios remain labelled.
- [ ] Implement six specified scenario groups: dark baseline, light step, linearity, noise, saturation and communication recovery.
- [ ] Run all optical scenarios against the ARM firmware in Renode, not just the C# reference.
- [ ] Compare actual versus expected results using the design's exact limits; save raw records and reports.
- [ ] Record an English bug report about one actual failure; if a teaching fault is injected, label it deliberate.
- [ ] Teach characterise, calibrate against, compare with, flag and trace back. Learner gives a two-minute explanation of one report and its limitations. Commit.

## E8 — Reproducibility, portfolio and learning gate

**Files:** Create README, architecture/hardware-gap/AI-assistance/progress/learning/resume documents, CI workflow, `reports/verification.md`, demo script and conditional CV bullets.

**Interfaces:** Produces an independent reproducible virtual test bench and a truthful portfolio handoff.

- [ ] Run host C tests, `.NET` tests, ARM build and headless Renode integration with exact documented commands.
- [ ] Set up CI for these layers where supported; pin runner/tool versions and record remotely unexecuted jobs as configured-only.
- [ ] Demonstrate from a fresh environment; save actual setup issues and fixes.
- [ ] Document real-hardware gaps: ADC/DMA/timer/peripheral implementation, electrical calibration, physical serial link, analog design and instrument validation.
- [ ] Produce a three-minute English demo and answer “What did you actually simulate?” precisely.
- [ ] Learner runs a scenario, changes an alarm limit, diagnoses one damaged frame, and explains raw versus filtered noise and simulation limitations.
- [ ] Self-review against design/checklist; leave learning-pending items visible. Commit locally and hand over paths.

## Teaching link

Use E1–E8 lessons in `LEARNING_EN_ZH.md`. Protocol, metrics and optical vocabulary must be practised with the actual implementation rather than memorised as unsupported claims.
