# Verified platform subset

The reference candidate was Renode 1.17.0's STM32F4 Discovery model. Its shared generic
STM32F4 description has 2 MB flash/256 KB SRAM, so it must not be called an exact F407VG.
The project-owned simulation/platform.repl selects Cortex-M4, NVIC, USART2, GPIOA,
RCC/RTC and F407VG-sized 1 MB flash / 128 KB main SRAM. CCM is not used.

Checked register facts against ST's official stm32f407xx.h and STM32F407VG product page:
flash 0x08000000, main SRAM 0x20000000, GPIOA 0x40020000, RCC 0x40023800,
USART2 0x40004400; SR/DR/BRR/CR1 offsets 0/4/8/12, RXNE bit5, TXE bit7,
USART UE/TE/RE bits13/3/2 and RCC APB1 USART2 enable bit17.

Renode uses UART.STM32_UART with IRQ38. The firmware polls registers and uses no ADC,
DMA, peripheral interrupt timing or physical optical input. Synthetic samples will enter
the C acquisition boundary through the explicit FEED protocol.
The 16 MHz clock/115200 setting is nominal configuration, not measured UART timing evidence.

E1 own-binary evidence: e1-arm-build.log, e1-smoke-green.log, e1-arm-uart.txt,
e1-bind.json and e1-renode-smoke.log. The CPU booted at PC 0x08000041 with SP 0x20020000
and returned ARM_SMOKE_OK over the emulated UART. This is ARM execution, distinct from host C tests.

The upstream Contiki demo was separately loaded/run for 0.1 virtual seconds as an environment
check only; its PC reached 0x0800526c. Its unimplemented Ethernet/SYSCFG warnings are retained
in e1-third-party-demo.log and do not establish those peripherals as validated project features.

References:
- https://github.com/renode/renode/tree/v1.17.0/platforms
- https://github.com/STMicroelectronics/cmsis_device_f4/blob/master/Include/stm32f407xx.h
- https://www.st.com/en/microcontrollers-microprocessors/stm32f407vg.html
- https://renode.readthedocs.io/en/latest/host-integration/uart.html
