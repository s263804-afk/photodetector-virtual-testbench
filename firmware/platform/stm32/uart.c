#include "uart.h"
#define REG(address) (*(volatile uint32_t *)(address))
#define UART_BASE 0x40004400u

void uart_init(void) {
    REG(0x40023830u) |= 1u;          /* GPIOA clock */
    REG(0x40023840u) |= (1u << 17);  /* USART2 clock */
    REG(0x40020000u) = (REG(0x40020000u) & ~(15u << 4)) | (10u << 4);
    REG(0x40020020u) = (REG(0x40020020u) & ~(255u << 8)) | (0x77u << 8);
    REG(UART_BASE + 0x08u) = 139u;  /* Nominal HSI 16 MHz / 115200; not timing evidence */
    REG(UART_BASE + 0x0Cu) = (1u << 13) | (1u << 3) | (1u << 2);
}

void uart_write(const char *data, size_t length) {
    for (size_t i = 0; i < length; ++i) {
        while (!(REG(UART_BASE) & (1u << 7))) { }
        REG(UART_BASE + 4u) = (uint8_t)data[i];
    }
}

int uart_read_byte(void) {
    return (REG(UART_BASE) & (1u << 5)) ? (int)(REG(UART_BASE + 4u) & 255u) : -1;
}
