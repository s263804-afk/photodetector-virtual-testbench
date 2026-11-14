# Third-party components

Renode1.17.0 (Antmicro and contributors): https://github.com/renode/renode,
MIT licensed. Platform peripheral names/addresses and reset pattern were checked
against its shipped STM32F4 definitions. Tool binaries are installed separately,
not distributed here. The project-owned LoopbackTerminal uses the Renode API.

GNU Arm Embedded14.3.Rel1, Zig0.17.1, NET SDK10.0.404, CMake4.4.4 and Ninja1.13.2
retain their respective upstream licenses; the setup downloads official archives.
NuGet package versions and content hashes are recorded in packages.lock.json.
STM32 reference/product documentation belongs to STMicroelectronics; no ST HAL
or vendor startup source is redistributed. The startup/UART adapter is project-owned.

Synthetic optical fixtures were generated for this project. Protocol known-vector
CRC16/CCITT-FALSE check is123456789→29B1. No measured third-party detector data is used.
