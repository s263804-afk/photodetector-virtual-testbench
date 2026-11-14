# Photodetector Monitoring & Automated Test Bench — Design

Prepared 12 November 2026. Concrete scenario selected by the learner: photodetector monitoring and automated testing, using computer simulation only.

## Workplace purpose

A photonics engineer changes the firmware or processing settings of a low-bandwidth photodetector monitor. Before relying on the new version, the engineer wants repeatable checks of baseline measurement, averaging, signal response, clipping indicators and communication recovery, with a saved report identifying firmware and test configuration.

The application is a detector readout/monitoring and validation workflow. It does not implement a high-speed optical receiver, BER tester, real-time optical link, photonic-chip controller or physical analog circuit.

## Approaches

- Selected: two independent portfolio projects; this repository handles the optical monitoring/test workflow locally.
- Alternative: a shared backend for device test results; useful later but creates an unnecessary dependency between the two demos.
- Simulation choice: run an actual ARM firmware binary in Renode, with a transparent virtual sample-input adapter. Host-only C tests supplement that evidence.
- Optional later hardware path: replace the sample adapter with verified ADC/timer/DMA input and add a physical serial transport. These are explicitly deferred.

## Architecture

```text
C# optical stimulus model + test scenarios
       ↓ commands and explicit virtual ADC-code feed
TCP connection to Renode UART terminal
       ↓
STM32F4-target C firmware running as ARM instructions
  bounded protocol parser → state machine → 16-sample filter → flags
       ↓ raw/filtered results and status
C# test runner → calculations → JSON/CSV/HTML report
```

The sample feed is injected into a software acquisition boundary. It does not pass through a simulated ADC peripheral unless a later verified extension adds that path. The README must say this prominently.

## Required technology

- C11 portable core plus STM32F4/ARM Cortex-M4 firmware target.
- GNU Arm toolchain, CMake and a verified Renode release; pin exact tested versions and platform description at E1.
- C#/.NET 10 LTS, using a currently supported patch verified on this environment. Do not fall back to an unsupported runtime; record and resolve a compatibility blocker explicitly.
- A headless C# console runner is mandatory. A small WPF interface is optional after the workflow is verified.
- First target candidate: STM32F4 Discovery platform supplied by Renode; verify the actual MCU, memory map, UART instance and peripheral models before naming a more precise target.
- Bind the virtual UART server to localhost where supported or otherwise document and constrain access. Disable terminal negotiation bytes for raw protocol traffic.

## Explicit optical model

These are educational test assumptions, not device specifications or measured performance.

| Parameter | Proposed value |
| --- | ---: |
| Photodiode responsivity | 0.6 A/W, constant model assumption |
| Effective transimpedance gain | 100,000 ohm |
| Dark/background output offset | 0.100 V |
| ADC-equivalent reference | 3.300 V |
| Code range | 0–4095, 12-bit unsigned |
| Nominal sample spacing | 1 ms of model time, not measured hardware timing |
| Default random seed | 20261112 |
| Default additive voltage-noise sigma | 0.002 V |

Model equations
```text
I_photo[A] = 0.6 * P[W]
V_ideal[V] = 0.100 + 100000 * I_photo
V_noisy = V_ideal + n, with n ~ Normal(0, sigma^2)
V_clipped = min(3.300, max(0, V_noisy))
code = floor(4095 * V_clipped / 3.300 + 0.5)
```

For input power expressed in microwatts, the ideal relation is `V = 0.100 + 0.060 * P_uW`. The positive sign is an assumed effective readout polarity; a physical inverting TIA may use a different polarity and biasing arrangement. Do not copy this simplified model as a validated analog design.

Noise-free check values
- `P=0 uW` → 0.100 V → code 124.
- `P=10 uW` → 0.700 V → code 869.
- `P=60 uW` → clipped 3.300 V → code 4095.

The deterministic fixture defines its own sample values; seeded random generation must be recorded and its exact fixture saved so a runtime upgrade cannot silently alter the evidence.

## Firmware behaviour

Acquisition adapter:
```text
sample_source_push(const uint16_t *samples, size_t count)
```
Accept 1–16 samples per batch, only while running. It is an explicit virtual-feed mode announced by `HELLO`.

Processing requirements
- Moving average window: 16. During startup, divide by the available sample count. Use a `uint32_t` accumulator and floor integer division.
- Reject codes outside 0–4095 before modifying any filter/sample state.
- Each accepted sample returns raw code, filtered code and status flags.
- Default low-rail flag: raw <= 16; high-rail flag: raw >= 4079. Configurable limits must satisfy `0 <= low < high <= 4095`.
- A near-rail flag describes observed code range, not a proven cause such as broken fibre or disconnected sensor.
- `START` clears filter history and resets sample index to 0. `STOP` stops acceptance.
- On boot/reset, the device starts in `NEEDS_HELLO`. Every structurally valid, CRC-valid non-HELLO request returns `ERR_SESSION` without processing or consuming a sequence. HELLO creates the protocol session and enters STOPPED.
- `HELLO` starts a fresh logical session in STOPPED state and clears the replay cache. Host explicitly records the session boundary.
- `START` is allowed only from STOPPED; `SET_LIMITS` only from STOPPED; otherwise return `ERR_STATE`. `STOP` in STOPPED is an OK no-op. STATUS and PING require a valid session.
- Use bounded buffers; no unbounded string parsing and no dynamic allocation in the core protocol/filter path.

## Protocol v1

Encoding: ASCII, LF-terminated, maximum 512 bytes including LF. CR is not used. Four fields precede the checksum: `version|sequence|command|arguments`.

```text
version|sequence|command|arguments|CRC4\n
```

`CRC4` is four uppercase hex digits from CRC-16/CCITT-FALSE over the exact bytes before the final `|CRC4`. Parameters: poly 0x1021, init 0xFFFF, refin false, refout false, xorout 0. Known vector: ASCII `123456789` → 0x29B1.

Commands and response payloads

| Command | Arguments | Response |
| --- | --- | --- |
| HELLO, sequence 0 | `-` | `HELLO` with firmware version, protocol 1, mode VIRTUAL_FEED; reset to STOPPED |
| PING | `-` | `PONG` |
| SET_LIMITS | `low,high` | `OK` or `ERR_ARGUMENT` |
| START | `-` | `OK` and zeroed processing state |
| FEED | 1–16 comma-separated integer codes | `DATA` with start index followed by raw:filtered:flags tuples |
| STATUS | `-` | state, processed count, CRC/overflow/error counters |
| STOP | `-` | `OK` |

Specify the exact response field grammar in `docs/protocol.md` before E3 implementation; keep it identical across C and C#. Preserve field meanings above. For `DATA`, use `start_index;raw:filtered:flags;...` as the arguments field, with flags as a decimal bitmask.

Sequence/retry rules
- One outstanding request at a time. HELLO uses 0; other requests increment 1–65535 and then wrap to 1.
- After HELLO the expected sequence is 1. A valid new expected request consumes the sequence even if its semantic result is an error.
- The immediately previous request with identical payload returns its cached response without processing samples twice. Same sequence with changed payload yields `ERR_SEQUENCE`. Any other unexpected sequence also yields `ERR_SEQUENCE` without consuming the expected sequence.
- Bad CRC: discard, increment counter, no state change and no reply.
- Overlong frame: discard through the next LF, count overflow, recover on the next valid frame.
- Invalid encoding/structure: reject without executing a command; counters and documented error/drop behaviour must be consistent.
- C# response timeout: configurable 2000 ms wall time, at most three total attempts with identical frame bytes.
- A transport disconnect aborts the current test. Reconnect uses HELLO and a new logical test session; do not automatically replay an unknown old-session operation.
- Exhausting all three response attempts leaves the command outcome unknown. Mark the test `INTERRUPTED_INDETERMINATE`, invalidate and close the host session, and require reconnect plus HELLO before further commands. Never carry the uncertain operation into the new session.
- Cancellation before handing bytes to the transport can preserve the session. Cancellation during/after a write is indeterminate and follows the same invalidation/close rule.
- A firmware reset may leave the Renode TCP terminal connected. The next valid non-HELLO request must receive `ERR_SESSION`; the host then marks interruption, invalidates/closes the old session and performs a new HELLO. Do not assume reset always disconnects the socket.

Simulation model time is distinct from host wall-clock time. Core results do not claim electrical baud-rate accuracy, ADC throughput, interrupt latency or hard real-time guarantees.

## C# host responsibilities

- `IByteTransport`: async connect/read/write/dispose; use `TcpClient` for Renode.
- Codec: CRC, bounded frames and streaming parser; cope with split/coalesced network reads.
- Session: sequence, retries, response matching and cancellation.
- Stimulus model: generate/save labelled synthetic scenarios.
- Reference calculation: independently compute expected filter results; it must not call firmware code.
- Analysis: raw mean, sample standard deviation (`n-1`), baseline subtraction, linearity and clipping rate; keep raw and filtered statistics separate.
- Reporting: JSON/CSV plus a small HTML summary; include config, firmware/tool versions, source hashes, session IDs, actual results, thresholds and verdict.
- CLI: `photo-bench run --scenario all --transport tcp --host 127.0.0.1 --port 12345 --output reports/run-id`.

## Optical scenarios and acceptance

All thresholds below are project test limits under the stated model, not physical detector specifications.

1. **Dark baseline
2. **Light step
3. **Linearity
4. **Noise measurement
5. **Saturation
6. **Communication recovery

## Repository structure

```text
firmware/
  core/filter.c,h, protocol.c,h, device.c,h
  platform/stm32/             startup, linker and UART adapter
  tests/                     portable host-C tests
  CMakeLists.txt
host/
  PhotoBench.Core/            transport, codec, session, optical model, analysis
  PhotoBench.Cli/             CLI and report orchestration
  PhotoBench.Tests/           .NET unit and integration categories
simulation/platform.resc, platform metadata
fixtures/                    versioned synthetic vectors and expected results
docs/protocol.md, model.md, architecture.md, hardware-gap.md
reports/, scripts/, .github/workflows/ci.yml
```

## Completion and limits

Core completion requires the compiled ARM ELF to run in Renode, actual C# ↔ virtual UART communication, all optical scenarios and protocol fault cases, saved reports and reproducible setup. Unit tests or mock transports alone are insufficient.

If an emulator/peripheral issue blocks that path, preserve host-C/.NET work and report embedded integration as blocked. Do not relabel it complete by showing only a desktop graph.

Future physical work: analog schematic and sensor selection, safe low-voltage build, real ADC/timer/DMA integration, instrument measurements, calibration against a known reference and validation under real noise/timing conditions. None is claimed now.
