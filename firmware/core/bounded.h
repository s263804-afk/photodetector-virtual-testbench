#ifndef PHOTO_BOUNDED_H
#define PHOTO_BOUNDED_H
#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

typedef struct {
    char *data;
    size_t length;
    size_t capacity;
    bool valid;
} TextWriter;

static inline void writer_init(TextWriter *writer, char *data, size_t capacity) {
    writer->data=data; writer->length=0; writer->capacity=capacity;
    writer->valid=data != NULL && capacity > 0;
    if (writer->valid) data[0]='\0';
}
static inline void writer_char(TextWriter *writer, char value) {
    if (!writer->valid) return;
    if (writer->length+1 >= writer->capacity) { writer->valid=false; return; }
    writer->data[writer->length++]=value;
    writer->data[writer->length]='\0';
}
static inline void writer_text(TextWriter *writer, const char *value) {
    while (*value) writer_char(writer, *value++);
}
static inline void writer_uint(TextWriter *writer, uint32_t value) {
    char digits[10]; size_t count=0;
    do { digits[count++]=(char)('0'+value%10u); value/=10u; } while (value);
    while (count) writer_char(writer, digits[--count]);
}
static inline bool parse_u16(const char *text, uint16_t *result) {
    if (text == NULL || *text == '\0') return false;
    uint32_t value=0;
    while (*text) {
        if (*text<'0' || *text>'9') return false;
        value=value*10u+(uint32_t)(*text++-'0');
        if (value>65535u) return false;
    }
    *result=(uint16_t)value;
    return true;
}
#endif
