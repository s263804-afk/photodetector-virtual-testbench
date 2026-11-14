# Architecture and session boundaries

C# Model/saved fixture → TCP NetworkStream → loopback UART bridge → STM32F4
USART2 registers → C parser → Device state/replay → Filter → UART DATA → C#
independent direct-window comparison → statistics and reports.

Firmware core owns bounded state and never allocates dynamically. The platform
adapter only polls UART registers and feeds bytes; virtual samples arrive via
FEED. C# owns scenario orchestration, unit conversion and report persistence.
The fault proxy changes real byte delivery and contains no detector/filter logic.

Device boot/reset: NEEDS_HELLO. HELLO0 → STOPPED/expected1. START → RUNNING and
clears filter/count; STOP → STOPPED. Semantic errors consume expected sequence;
framing/CRC errors do not. An exact duplicate of the previous request replays
cached output. A changed duplicate is rejected. Sequence65535 wraps to1.

One outstanding host operation,2-second default timeout, maximum3 identical
transmissions. A missing reply does not prove the command was not executed.
Exhausted retries, disconnect, post-write cancellation or ERR_SESSION invalidate
and close the logical session. A new DeviceSession object plus HELLO is required;
old uncertain operations are never automatically replayed across the boundary.

Run metadata identifies tool archive hashes, loaded ELF, fixture hashes, model
constants, test limits, session GUIDs, observed values and simulation boundaries.
All required groups must pass; partial/interrupted work is preserved withoutPASS.
