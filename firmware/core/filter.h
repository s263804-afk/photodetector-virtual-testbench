#ifndef PHOTO_FILTER_H
#define PHOTO_FILTER_H
#include <stdbool.h>
#include <stdint.h>
typedef struct {
    uint16_t values[16];
    uint32_t sum;
    uint8_t count;
    uint8_t index;
} Filter;
void filter_init(Filter *filter);
bool filter_push(Filter *filter, uint16_t raw, uint16_t *filtered);
#endif
