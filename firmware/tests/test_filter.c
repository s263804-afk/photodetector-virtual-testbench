#include <stdio.h>
#include <stdlib.h>
#include "filter.h"

static unsigned checks;
#define CHECK(expr) do { ++checks; if (!(expr)) { \
    fprintf(stderr, "FAIL line %d: %s\n", __LINE__, #expr); exit(1); } } while (0)

int main(void) {
    Filter filter;
    uint16_t output;
    filter_init(&filter);
    CHECK(filter_push(&filter, 100, &output)); CHECK(output == 100);
    CHECK(filter_push(&filter, 200, &output)); CHECK(output == 150);
    CHECK(filter_push(&filter, 300, &output)); CHECK(output == 200);
    filter_init(&filter);
    for (unsigned i=0; i<16; ++i) {
        CHECK(filter_push(&filter, 4095, &output)); CHECK(output == 4095);
    }
    CHECK(filter.sum == 65520u);
    filter_init(&filter);
    CHECK(filter_push(&filter, 100, &output));
    CHECK(!filter_push(&filter, 4096, &output));
    CHECK(!filter_push(&filter, 65535, &output));
    CHECK(output == 100);
    CHECK(filter_push(&filter, 200, &output)); CHECK(output == 150);
    filter_init(&filter);
    for (uint16_t i=0; i<=16; ++i) CHECK(filter_push(&filter, i, &output));
    CHECK(output == 8);
    filter_init(&filter);
    CHECK(filter_push(&filter, 400, &output)); CHECK(output == 400);
    filter_init(&filter);
    for (unsigned i=0; i<64; ++i) CHECK(filter_push(&filter, 124, &output));
    for (unsigned i=0; i<64; ++i) {
        CHECK(filter_push(&filter, 869, &output));
        if (i==0) CHECK(output == 170);
        if (i>=15) CHECK(output == 869);
    }
    CHECK(!filter_push(NULL, 1, &output));
    CHECK(!filter_push(&filter, 1, NULL));
    printf("Filter: %u assertions passed\n", checks);
    return 0;
}
