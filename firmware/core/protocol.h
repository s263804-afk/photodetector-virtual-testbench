#ifndef PHOTO_PROTOCOL_H
#define PHOTO_PROTOCOL_H
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#define PHOTO_FRAME_MAX 512u
#define PHOTO_ARGS_MAX 400u
typedef struct {
    uint16_t sequence;
    char command[17];
    char arguments[PHOTO_ARGS_MAX + 1];
    char raw[PHOTO_FRAME_MAX];
} Frame;
typedef struct {
    uint8_t buffer[PHOTO_FRAME_MAX];
    size_t length;
    bool discarding;
    uint32_t crc_errors;
    uint32_t overflow_errors;
    uint32_t structure_errors;
} ProtocolParser;
typedef void (*FrameCallback)(const Frame *frame, void *context);
uint16_t protocol_crc16(const uint8_t *data, size_t length);
bool protocol_encode(uint16_t sequence, const char *command, const char *arguments,
                     char *output, size_t capacity, size_t *length);
void protocol_init(ProtocolParser *parser);
void protocol_feed(ProtocolParser *parser, const uint8_t *data, size_t length,
                   FrameCallback callback, void *context);
#endif
