#include "uart.h"

int main(void) {
    uart_init();
    uart_write("PHOTO_ARM_BOOT\n", 15);
    for (;;) {
        int byte = uart_read_byte();
        if (byte == '?') {
            uart_write("ARM_SMOKE_OK\n", 13);
        }
    }
}
