#ifndef PHOTO_DEVICE_H
#define PHOTO_DEVICE_H
#include "filter.h"
#include "protocol.h"
typedef enum { NEEDS_HELLO, STOPPED, RUNNING } DeviceState;
typedef struct { uint16_t raw, filtered; uint8_t flags; } SampleResult;
typedef struct { char bytes[PHOTO_FRAME_MAX+1]; size_t length; } Response;
typedef void (*DeviceSend)(const char *bytes, size_t length, void *context);
typedef struct {
    ProtocolParser parser;
    Filter filter;
    DeviceState state;
    uint16_t expected_sequence, last_sequence, low, high;
    uint32_t processed, semantic_errors;
    bool has_replay;
    char last_request[PHOTO_FRAME_MAX];
    Response last_response;
    DeviceSend send;
    void *send_context;
} Device;
void device_init(Device *device, DeviceSend send, void *context);
void device_handle(Device *device, const Frame *frame, Response *response);
void device_feed(Device *device, const uint8_t *bytes, size_t length);
bool sample_source_push(Device *device, const uint16_t *samples, size_t count,
                        SampleResult *results);
#endif
