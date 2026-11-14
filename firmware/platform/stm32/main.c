#include "device.h"
#include "uart.h"

static Device device;
static void send_response(const char *bytes, size_t length, void *context) {
    (void)context;
    uart_write(bytes,length);
}

int main(void) {
    uart_init();
    device_init(&device,send_response,NULL);
    const char banner[]="PHOTO_ARM_VIRTUAL_FEED_BOOT\n";
    uart_write(banner,sizeof(banner)-1);
    for (;;) {
        int received=uart_read_byte();
        if (received>=0) {
            uint8_t byte=(uint8_t)received;
            device_feed(&device,&byte,1);
        }
    }
}
