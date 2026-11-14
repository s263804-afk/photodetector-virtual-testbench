#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "protocol.h"
static unsigned checks;
#define CHECK(expr) do { ++checks; if (!(expr)) { \
    fprintf(stderr, "FAIL line %d: %s\n", __LINE__, #expr); exit(1); } } while (0)
typedef struct { Frame frames[8]; unsigned count; } Collected;
static void collect(const Frame *frame, void *context) {
    Collected *out = context;
    CHECK(out->count < 8);
    out->frames[out->count++] = *frame;
}
static void feed(ProtocolParser *parser, const char *data, Collected *out) {
    protocol_feed(parser, (const uint8_t *)data, strlen(data), collect, out);
}
int main(void) {
    const char *ping = "1|1|PING|-|CFA9\n";
    const char *hello = "1|0|HELLO|-|CB9D\n";
    CHECK(protocol_crc16((const uint8_t *)"123456789", 9) == 0x29B1u);
    char encoded[513]; size_t length = 0;
    CHECK(protocol_encode(1, "PING", "-", encoded, sizeof(encoded), &length));
    CHECK(length == strlen(ping)); CHECK(strcmp(encoded, ping) == 0);
    CHECK(protocol_encode(0, "HELLO", "-", encoded, sizeof(encoded), &length));
    CHECK(strcmp(encoded, hello) == 0);
    CHECK(protocol_encode(3, "FEED", "124,869,4095", encoded, sizeof(encoded), &length));
    CHECK(strcmp(encoded, "1|3|FEED|124,869,4095|643E\n") == 0);
    CHECK(!protocol_encode(1, "PING", "", encoded, sizeof(encoded), &length));
    CHECK(!protocol_encode(1, "PING", "x|y", encoded, sizeof(encoded), &length));
    CHECK(!protocol_encode(1, "PING", "-", encoded, 8, &length));
    for (size_t split=0; split<=strlen(ping); ++split) {
        ProtocolParser parser; Collected out = {0}; protocol_init(&parser);
        protocol_feed(&parser, (const uint8_t *)ping, split, collect, &out);
        protocol_feed(&parser, (const uint8_t *)ping+split, strlen(ping)-split, collect, &out);
        CHECK(out.count == 1); CHECK(out.frames[0].sequence == 1);
        CHECK(strcmp(out.frames[0].command, "PING") == 0);
    }
    ProtocolParser parser; Collected out = {0}; protocol_init(&parser);
    feed(&parser, "1|0|HELLO|-|CB9D\n1|1|PING|-|CFA9\n", &out);
    CHECK(out.count == 2);
    protocol_init(&parser); out.count=0;
    feed(&parser, "1|1|PING|-|0000\n", &out);
    CHECK(out.count == 0); CHECK(parser.crc_errors == 1);
    feed(&parser, "1|1|PING|-|CFAg\n", &out);
    feed(&parser, "1|1|FEED||0000\n", &out);
    CHECK(parser.structure_errors == 2);
    uint8_t bad[513]; memset(bad, 'X', 512); bad[512]='\n';
    protocol_feed(&parser, bad, sizeof(bad), collect, &out);
    CHECK(parser.overflow_errors == 1);
    const uint8_t non_ascii[] = {255, '\n'};
    protocol_feed(&parser, non_ascii, sizeof(non_ascii), collect, &out);
    CHECK(parser.structure_errors == 3);
    feed(&parser, ping, &out); CHECK(out.count == 1);
    printf("Protocol: %u assertions passed\n", checks);
    return 0;
}
