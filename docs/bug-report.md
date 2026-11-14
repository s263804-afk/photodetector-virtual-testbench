# Actual integration bug — reset lost the vector table

Expected: after machine Reset, the Cortex-M4 boots the same firmware, enters
NEEDS_HELLO, and returns ERR_SESSION to a non-HELLO command over retained TCP.
Observed: Renode cleared VectorTableOffset; PC/SP became0 and UART input was
rejected because the receiver was disabled. Host retries exhausted.

Reproduction: boot platform.resc without a reset macro, establish HELLO/START,
issue machine Reset through stdin, send PING without reconnecting.
Root cause: the platform mapped flash at0x08000000 but reset CPU configuration
had no macro restoring the load/vector location. The default vector address0
was unmapped. This was a real configuration bug, not deliberate fault injection.

Fix: reset macro reloads the exact project ELF and restores VectorTableOffset
0x08000000, following the shipped STM32F4 example. No host state was substituted.
Regression: ActualArmFirmwarePassesTcpFaultAndRetainedConnectionResetChecks
failed before the change and passed afterwards. Raw failure logs:
reports/evidence/e6-reset-vector-failure.log and e6-reset-integration-failure.log.

中文：复位后入口地址丢失是真实发现的问题；修复启动配置后，由原集成测试验证恢复。
