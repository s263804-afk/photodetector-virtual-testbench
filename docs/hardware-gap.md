# Physical hardware gap

The actual executable is compiled for Cortex-M4/Thumb and runs in Renode. UART
register access, parsing, state transitions, integer filtering and response bytes
are exercised. The platform is a minimal STM32F4 subset with F407VG flash/main-SRAM
sizes, not a complete Discovery-board model.

Still required for a physical product: photodiode/analog front-end selection and
schematic; power/bias/protection; real ADC sampling and reference accuracy; timer,
DMA and interrupts; physical UART/USB transport; electrical signal integrity;
instrument-backed calibration, temperature/noise tests and timing measurements.
The positive0.060V/uW model slope is an assumed effective polarity, not a validated
TIA circuit. No measured optical sensitivity, bandwidth, BER or throughput claim.

中文：当前验证软件处理与虚拟测试流程；电路、ADC/DMA、真实串口、仪器校准与硬件时序均待实物验证。
