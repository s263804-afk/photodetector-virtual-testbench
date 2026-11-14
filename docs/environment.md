# Embedded environment

All tools are user-level and version-pinned. No board, Windows service, OS feature,
global PATH setting or existing Python environment is required or modified.

- ARM: Arm GNU Toolchain 14.3.Rel1, GCC 14.3.1, downloaded from Arm's official Windows ZIP.
- Simulator: Renode 1.17.0, official windows-portable ZIP; project UART bridge uses loopback.
- Host application: .NET SDK 10.0.404 / supported .NET 10.0.15 LTS runtime.
- Host C: Zig 0.17.1 cc (Clang 22.1.8). CMake 4.4.4 and Ninja 1.13.2.
- Build/test tools: isolated .venv based on the existing Python 3.12.7.

The exact archive hashes and paths are in reports/tool-versions.json.
Microsoft's published SHA-512 and Zig's published SHA-256 were matched before execution.
The Arm/Renode hashes record the exact downloads; no publisher checksum comparison is claimed.

Important simulator dependency boundary: Renode's vendor portable bundle runs on its
bundled .NET 8.0.10. This is separate from our C# application, which runs on supported
.NET 10.0.15. A direct NET10 launcher attempt failed due mixed-framework dependencies;
no unverified runtime substitution is used. The older vendor-bundled simulator runtime
is a known tool dependency limitation pending an upstream update. The simulator is used
only locally, with a loopback UART and stdin control (no network monitor exposed).

The workspace path contains spaces and a comma. GCC linker-map arguments use -Xlinker
to preserve the comma. Renode execution artifacts are copied byte-for-byte into a
project-owned no-space staging directory under LOCALAPPDATA/PortfolioTools/photobench-runs.
The launcher records the loaded ELF hash. Source files remain in the user's workspace.

Setup: scripts/setup.ps1 -Python <Python3.12.7 executable>; scripts/verify.ps1. Individual commands:
scripts/build-firmware.ps1; scripts/build-host-c.ps1; Python scripts/e1-smoke.py.

Each C#/Python launcher now supplies --config pointing to a per-launch private config
and monitor history, avoiding cross-process access to the global history file.
