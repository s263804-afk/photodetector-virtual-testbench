#ifndef PHOTO_UART_H
#define PHOTO_UART_H
#include <stddef.h>
#include <stdint.h>
void uart_init(void);
void uart_write(const char *data, size_t length);
int uart_read_byte(void);
#endif
