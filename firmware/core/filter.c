#include "filter.h"
#include <string.h>

void filter_init(Filter *filter) {
    if (filter != NULL) memset(filter, 0, sizeof(*filter));
}

bool filter_push(Filter *filter, uint16_t raw, uint16_t *filtered) {
    if (filter == NULL || filtered == NULL || raw > 4095u) return false;
    if (filter->count == 16u) {
        filter->sum -= filter->values[filter->index];
    } else {
        ++filter->count;
    }
    filter->values[filter->index] = raw;
    filter->sum += raw;
    filter->index = (uint8_t)((filter->index + 1u) % 16u);
    *filtered = (uint16_t)(filter->sum / filter->count);
    return true;
}
