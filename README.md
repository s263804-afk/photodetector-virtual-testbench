# Photodetector Virtual Test Bench

Independent personal portfolio project by **Weitong Liu**, developed with **AI assistance**.
C11 firmware runs as ARM Cortex-M4 instructions in Renode. A C#/.NET 10 host sends
synthetic 12-bit samples through the emulated USART2 and checks returned processing
results against an independently implemented reference.

**Simulation boundary:** samples enter an explicitly named VIRTUAL_FEED software
boundary. This project verifies firmware processing and a local automated test
workflow. It does not validate an ADC peripheral, analog circuit, real photodiode,
physical serial interface or hardware timing.

## Results

- 16-sample integer moving average, bounded 512-byte ASCII/CRC16 protocol,
  state/sequence validation and exact duplicate-request replay.
- Six ARM-backed scenario groups: dark baseline, light step, linearity, noise,
  saturation and communication recovery;3968 returned sample tuples.
- CTest3/3 portable C executables (245+89+221 assertions); NET49/49 unit and actual
  Renode integration tests at the implementation checkpoint.
- The saved 1024-point noise vector is checked against separate Python statistics;
  raw and settled filtered results are reported independently.
- Faults include dropped responses, fragmented bytes, bad CRC, overflow, all3
  retries exhausted, cancellation after transmission, disconnect and reset while
  TCP remains connected. Uncertain operations invalidate the session.

Open [the example HTML report](reports/example/report.html),
[JSON evidence](reports/example/report.json) or [raw/filtered CSV](reports/example/samples.csv).
A report passes only when all six required groups pass. Missing/interrupted groups
cannot produce overallPASS. See [verification](reports/verification.md).

## Run on Windows x64

Install Python3.12.7, clone/download the complete repository, and open PowerShell
at this repository root. setup installs the pinned tools into the current user's
LOCALAPPDATA/PortfolioTools, verifies archive hashes and restores locked packages.
It does not add system services or change the global PATH. Downloads total about1GB.
Supply an explicit Python path if the Windows Store alias handles python.

    .\scripts\setup.ps1 -Python 'C:\Path\To\Python312\python.exe'
    .\scripts\verify.ps1

With an already configured environment:

    .\scripts\demo.ps1

The demo starts its own headless simulator, runs all scenarios, saves reports and
stops the simulator. Optional output directory: demo.ps1 -Output reports/my-run.
The equivalent C# CLI is:

    . .\scripts\tools.ps1
    & $taskDotnet run --project host/PhotoBench.Cli -- run --scenario all --transport tcp --host 127.0.0.1 --port 12345 --output reports/my-run --manage-simulator

Without --manage-simulator, the endpoint must already exist; retained-connection
reset remains UNVERIFIED because that external process is not controlled. Such
an incomplete communication check cannot yield overallPASS. --scenario accepts
an individual group or comma-separated groups; partial runs remainINCOMPLETE.
Ctrl+C saves partial scenario results when execution has begun. An uncertain
command is never automatically carried into a new logical session.

## Architecture

C# optical model / saved fixture → TCP loopback bridge → emulated USART2 →
ARM C bounded parser → device state machine → integer filter and rail flags →
C# independent comparison and analysis → JSON/CSV/HTML.

- [Protocol](docs/protocol.md): exact wire bytes, error handling and replay.
- [Model](docs/model.md): units, quantisation, noise and fit exclusions.
- [Platform](docs/platform-support.md): actual memory/peripheral subset.
- [Hardware gaps](docs/hardware-gap.md): what physical validation would require.
- [Environment](docs/environment.md): versions, official sources and setup issues.
- [Test plan](docs/test-plan.md), [actual bug report](docs/bug-report.md).
- [English demo](docs/DEMO_EN.md), [CV entry](docs/CV_ENTRY_EN.md).
- [AI contribution](docs/AI_ASSISTANCE.md) and [personal learning](docs/LEARNING_PROGRESS.md).

Renode1.17.0's portable bundle includes an oldNET8.0.10 runtime. Our C# host uses
NET10.0.15; the simulator vendor runtime is a separate documented dependency gap.
No substitute runtime has been silently applied. Both simulator control and UART
remain local. Tool binaries and caches are not redistributed in this repository.
