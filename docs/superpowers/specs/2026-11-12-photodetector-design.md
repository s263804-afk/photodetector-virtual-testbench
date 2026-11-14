# Photodetector Monitoring & Automated Test Bench — Design / 光电监测设计

Prepared 12 November 2026. Concrete scenario selected by the learner: photodetector monitoring and automated testing, using computer simulation only.
本人已选定光电探测器监测与自动化测试，当前仅使用电脑仿真。

## Workplace purpose / 实际用途

A photonics engineer changes the firmware or processing settings of a low-bandwidth photodetector monitor. Before relying on the new version, the engineer wants repeatable checks of baseline measurement, averaging, signal response, clipping indicators and communication recovery, with a saved report identifying firmware and test configuration.
光子实验室工程师修改低带宽探测器监测固件后，需要重复检查基线、平均处理、响应、饱和提示与通信恢复，并保存固件及测试配置。

The application is a detector readout/monitoring and validation workflow. It does not implement a high-speed optical receiver, BER tester, real-time optical link, photonic-chip controller or physical analog circuit.
定位是探测器读出监测与验证流程；高速接收、误码测试、真实光链路、光芯片控制与实物模拟电路不在核心范围。

## Approaches / 路线比较

- Selected: two independent portfolio projects; this repository handles the optical monitoring/test workflow locally. / 已选独立项目路线。
- Alternative: a shared backend for device test results; useful later but creates an unnecessary dependency between the two demos. / 可后续接后台，当前不绑定。
- Simulation choice: run an actual ARM firmware binary in Renode, with a transparent virtual sample-input adapter. Host-only C tests supplement that evidence. / ARM 固件执行与电脑单元测试分别留证。
- Optional later hardware path: replace the sample adapter with verified ADC/timer/DMA input and add a physical serial transport. These are explicitly deferred. / 实物输入与串口适配留后续。

## Architecture / 结构

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
样本从软件采集边界注入；核心不宣称经过 ADC 外设模型，必须在 README 明示。

## Required technology / 技术要求

- C11 portable core plus STM32F4/ARM Cortex-M4 firmware target. / C11 核心与 ARM 固件。
- GNU Arm toolchain, CMake and a verified Renode release; pin exact tested versions and platform description at E1. / E1 锁定工具与模型版本。
- C#/.NET 10 LTS, using a currently supported patch verified on this environment. Do not fall back to an unsupported runtime; record and resolve a compatibility blocker explicitly. / 使用仍受支持且经验证的 .NET 10 LTS 补丁版本；兼容性阻塞需明确处理。
- A headless C# console runner is mandatory. A small WPF interface is optional after the workflow is verified. / 命令行测试器必做，界面选做。
- First target candidate: STM32F4 Discovery platform supplied by Renode; verify the actual MCU, memory map, UART instance and peripheral models before naming a more precise target. / 先核对型号、内存、UART 与模型。
- Bind the virtual UART server to localhost where supported or otherwise document and constrain access. Disable terminal negotiation bytes for raw protocol traffic. / 本机连接，关闭终端协商字节。

## Explicit optical model / 透明光电模型

These are educational test assumptions, not device specifications or measured performance.
以下为教学模型假设，不是器件规格和实测结果。

| Parameter / 参数 | Proposed value / 建议值 |
|---|---:|
| Photodiode responsivity / 响应度 | 0.6 A/W, constant model assumption |
| Effective transimpedance gain / 等效跨阻增益 | 100,000 ohm |
| Dark/background output offset / 暗态等效输出偏置 | 0.100 V |
| ADC-equivalent reference / 等效参考电压 | 3.300 V |
| Code range / 数字码范围 | 0–4095, 12-bit unsigned |
| Nominal sample spacing / 标称样本间距 | 1 ms of model time, not measured hardware timing |
| Default random seed / 默认种子 | 20261112 |
| Default additive voltage-noise sigma / 默认电压噪声标准差 | 0.002 V |

Model equations / 模型公式:
```text
I_photo[A] = 0.6 * P[W]
V_ideal[V] = 0.100 + 100000 * I_photo
V_noisy = V_ideal + n, with n ~ Normal(0, sigma^2)
V_clipped = min(3.300, max(0, V_noisy))
code = floor(4095 * V_clipped / 3.300 + 0.5)
```

For input power expressed in microwatts, the ideal relation is `V = 0.100 + 0.060 * P_uW`. The positive sign is an assumed effective readout polarity; a physical inverting TIA may use a different polarity and biasing arrangement. Do not copy this simplified model as a validated analog design.
以微瓦输入时为 `V = 0.100 + 0.060 × P_uW`。正号是等效读出极性假设；实际反相跨阻放大器的极性与偏置需另行设计。

Noise-free check values / 无噪声检查值:
- `P=0 uW` → 0.100 V → code 124.
- `P=10 uW` → 0.700 V → code 869.
- `P=60 uW` → clipped 3.300 V → code 4095.

The deterministic fixture defines its own sample values; seeded random generation must be recorded and its exact fixture saved so a runtime upgrade cannot silently alter the evidence.
随机种子之外，还要保存具体向量；运行库升级不能悄悄改变证据。

## Firmware behaviour / 固件行为

Acquisition adapter:
```text
sample_source_push(const uint16_t *samples, size_t count)
```
Accept 1–16 samples per batch, only while running. It is an explicit virtual-feed mode announced by `HELLO`.
每批 1–16 个样本，仅运行状态接收；握手明确报告虚拟注入模式。

Processing requirements / 处理要求:
- Moving average window: 16. During startup, divide by the available sample count. Use a `uint32_t` accumulator and floor integer division. / 16 点滑动平均，起始按已有点数平均，向下取整。
- Reject codes outside 0–4095 before modifying any filter/sample state. / 非法码不能改变状态。
- Each accepted sample returns raw code, filtered code and status flags. / 返回原始、滤波及状态。
- Default low-rail flag: raw <= 16; high-rail flag: raw >= 4079. Configurable limits must satisfy `0 <= low < high <= 4095`. / 默认近轨阈值，可配置且校验。
- A near-rail flag describes observed code range, not a proven cause such as broken fibre or disconnected sensor. / 提示近轨不等于诊断物理故障原因。
- `START` clears filter history and resets sample index to 0. `STOP` stops acceptance. / 开始清空历史与编号，停止后拒绝注入。
- On boot/reset, the device starts in `NEEDS_HELLO`. Every structurally valid, CRC-valid non-HELLO request returns `ERR_SESSION` without processing or consuming a sequence. HELLO creates the protocol session and enters STOPPED. / 启动与复位后必须重新握手；其他有效请求返回会话错误，不处理样本或推进序号。
- `HELLO` starts a fresh logical session in STOPPED state and clears the replay cache. Host explicitly records the session boundary. / 握手开启新会话，不能假装断线后数据连续。
- `START` is allowed only from STOPPED; `SET_LIMITS` only from STOPPED; otherwise return `ERR_STATE`. `STOP` in STOPPED is an OK no-op. STATUS and PING require a valid session. / 开始与改阈值只允许停止态；重复停止成功但无动作；查询也需要握手。
- Use bounded buffers; no unbounded string parsing and no dynamic allocation in the core protocol/filter path. / 有界缓存，无无限解析与核心动态内存。

## Protocol v1 / 协议第一版

Encoding: ASCII, LF-terminated, maximum 512 bytes including LF. CR is not used. Four fields precede the checksum: `version|sequence|command|arguments`.
ASCII 编码，以 LF 结束，含换行最多 512 字节；校验前四个字段如上。

```text
version|sequence|command|arguments|CRC4\n
```

`CRC4` is four uppercase hex digits from CRC-16/CCITT-FALSE over the exact bytes before the final `|CRC4`. Parameters: poly 0x1021, init 0xFFFF, refin false, refout false, xorout 0. Known vector: ASCII `123456789` → 0x29B1.
校验只覆盖最后分隔符之前的实际字节，不包含 CRC 字段和 LF。

Commands and response payloads / 命令与返回:

| Command / 命令 | Arguments / 参数 | Response / 返回 |
|---|---|---|
| HELLO, sequence 0 | `-` | `HELLO` with firmware version, protocol 1, mode VIRTUAL_FEED; reset to STOPPED |
| PING | `-` | `PONG` |
| SET_LIMITS | `low,high` | `OK` or `ERR_ARGUMENT` |
| START | `-` | `OK` and zeroed processing state |
| FEED | 1–16 comma-separated integer codes | `DATA` with start index followed by raw:filtered:flags tuples |
| STATUS | `-` | state, processed count, CRC/overflow/error counters |
| STOP | `-` | `OK` |

Specify the exact response field grammar in `docs/protocol.md` before E3 implementation; keep it identical across C and C#. Preserve field meanings above. For `DATA`, use `start_index;raw:filtered:flags;...` as the arguments field, with flags as a decimal bitmask.
E3 前写明响应语法，C 和 C# 一致；DATA 参数采用上述固定形式。

Sequence/retry rules / 序号与重试:
- One outstanding request at a time. HELLO uses 0; other requests increment 1–65535 and then wrap to 1. / 单个在途请求，0 只用于握手。
- After HELLO the expected sequence is 1. A valid new expected request consumes the sequence even if its semantic result is an error. / 有效预期序号即使业务报错也推进序号。
- The immediately previous request with identical payload returns its cached response without processing samples twice. Same sequence with changed payload yields `ERR_SEQUENCE`. Any other unexpected sequence also yields `ERR_SEQUENCE` without consuming the expected sequence. / 重试不重复处理，变更或乱序拒绝。
- Bad CRC: discard, increment counter, no state change and no reply. / 坏 CRC 丢弃不响应。
- Overlong frame: discard through the next LF, count overflow, recover on the next valid frame. / 超长帧丢到换行后恢复。
- Invalid encoding/structure: reject without executing a command; counters and documented error/drop behaviour must be consistent. / 非法结构不能执行命令。
- C# response timeout: configurable 2000 ms wall time, at most three total attempts with identical frame bytes. / 主机墙钟超时与三次总尝试。
- A transport disconnect aborts the current test. Reconnect uses HELLO and a new logical test session; do not automatically replay an unknown old-session operation. / 断线终止当前测试，新连接开新会话。
- Exhausting all three response attempts leaves the command outcome unknown. Mark the test `INTERRUPTED_INDETERMINATE`, invalidate and close the host session, and require reconnect plus HELLO before further commands. Never carry the uncertain operation into the new session. / 三次都未收到回复时结果不确定；中断测试并关闭会话，重新连接握手，不跨会话重放。
- Cancellation before handing bytes to the transport can preserve the session. Cancellation during/after a write is indeterminate and follows the same invalidation/close rule. / 写入前取消可保留会话；开始写入后取消按结果不确定处理。
- A firmware reset may leave the Renode TCP terminal connected. The next valid non-HELLO request must receive `ERR_SESSION`; the host then marks interruption, invalidates/closes the old session and performs a new HELLO. Do not assume reset always disconnects the socket. / 固件复位不一定断开 TCP；收到会话错误后重建，测试必须覆盖连接保留情况。

Simulation model time is distinct from host wall-clock time. Core results do not claim electrical baud-rate accuracy, ADC throughput, interrupt latency or hard real-time guarantees.
模型时间与主机墙钟分开；核心不宣称电气波特率、ADC 吞吐、IRQ 延迟或硬实时指标。

## C# host responsibilities / C# 主机职责

- `IByteTransport`: async connect/read/write/dispose; use `TcpClient` for Renode. / 异步传输接口。
- Codec: CRC, bounded frames and streaming parser; cope with split/coalesced network reads. / 支持分片与粘连。
- Session: sequence, retries, response matching and cancellation. / 会话与取消。
- Stimulus model: generate/save labelled synthetic scenarios. / 生成并保存明确标记的模型样本。
- Reference calculation: independently compute expected filter results; it must not call firmware code. / 独立参考答案。
- Analysis: raw mean, sample standard deviation (`n-1`), baseline subtraction, linearity and clipping rate; keep raw and filtered statistics separate. / 原始与滤波指标分开。
- Reporting: JSON/CSV plus a small HTML summary; include config, firmware/tool versions, source hashes, session IDs, actual results, thresholds and verdict. / 报告可追溯。
- CLI: `photo-bench run --scenario all --transport tcp --host 127.0.0.1 --port 12345 --output reports/run-id`. / 目标命令，不代表已经实现。

## Optical scenarios and acceptance / 光电场景与验收

All thresholds below are project test limits under the stated model, not physical detector specifications.
全部阈值均为本模型的项目测试条件。

1. **Dark baseline / 暗态基线:** 1024 noise-free samples at 0 uW. Raw and settled filtered codes are 124; no rail flag. The conversion 124*3.3/4095 approximates 0.100 V within one LSB. / 明确已知结果。
2. **Light step / 光强阶跃:** 64 noise-free samples at code 124, then 64 at code 869. First filtered value after the step is floor((15*124+869)/16)=170; the 16th changed sample settles at 869. / 验证动态响应及延迟。
3. **Linearity / 线性度:** model powers 0,5,10,20,30,40 uW; 256 noise-free samples per level. Fit settled mean voltage against power: expected slope 0.060 V/uW within 0.0005 and intercept 0.100 V within 0.002. / 验证模型与处理链，不证明实物线性度。
4. **Noise measurement / 噪声测量:** saved 1024-point noisy fixture at 20 uW. C# statistics match a separately checked reference within 1e-6 V for mean/std. Report raw/filtered results; do not assert the moving average's ideal sqrt(16) gain for correlated/finite data. / 噪声指标用独立参考验证。
5. **Saturation / 饱和:** noise-free 60 uW produces 4095 and high-rail flags on every sample. Saturated points are excluded from linearity fitting and shown as excluded. / 饱和点不参与线性拟合。
6. **Communication recovery / 通信恢复:** corrupt CRC, duplicate FEED, overlong frame, fragmented response, lost reply and emulator reset; verify the protocol contract and saved failure evidence. / 通信故障测试。

## Repository structure / 仓库结构

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

## Completion and limits / 完成与边界

Core completion requires the compiled ARM ELF to run in Renode, actual C# ↔ virtual UART communication, all optical scenarios and protocol fault cases, saved reports and reproducible setup. Unit tests or mock transports alone are insufficient.
核心完成必须包含 ARM 固件真实仿真执行、C# 通信、光电场景与协议故障测试、报告及复现说明。

If an emulator/peripheral issue blocks that path, preserve host-C/.NET work and report embedded integration as blocked. Do not relabel it complete by showing only a desktop graph.
如果模型或外设阻塞，保留独立任务并标明集成未验证。

Future physical work: analog schematic and sensor selection, safe low-voltage build, real ADC/timer/DMA integration, instrument measurements, calibration against a known reference and validation under real noise/timing conditions. None is claimed now.
后续实物工作包括器件电路、真实 ADC/定时/DMA、仪器测量、参考源校准及真实噪声时序验证；当前不作已完成声明。
