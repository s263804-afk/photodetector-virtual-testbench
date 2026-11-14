#include "protocol.h"
#include "bounded.h"
#include <string.h>

static bool valid_command(const char *value) {
    if (value == NULL) return false;
    size_t length=0;
    while (*value) {
        if (++length>16u || !((*value>='A' && *value<='Z') || *value=='_')) return false;
        ++value;
    }
    return length>0;
}

static bool valid_arguments(const char *value) {
    if (value == NULL) return false;
    size_t length=0;
    while (*value) {
        unsigned char c=(unsigned char)*value++;
        if (++length>PHOTO_ARGS_MAX || c<32 || c>126 || c=='|') return false;
    }
    return length>0;
}

uint16_t protocol_crc16(const uint8_t *data, size_t length) {
    uint16_t crc=0xFFFFu;
    for (size_t i=0; i<length; ++i) {
        crc^=(uint16_t)((uint16_t)data[i]<<8);
        for (unsigned bit=0; bit<8; ++bit) {
            crc=(uint16_t)((crc&0x8000u) ? ((uint32_t)crc<<1)^0x1021u : (uint32_t)crc<<1);
        }
    }
    return crc;
}

bool protocol_encode(uint16_t sequence, const char *command, const char *arguments,
                     char *output, size_t capacity, size_t *length) {
    if (length == NULL) return false;
    *length=0;
    if (!valid_command(command) || !valid_arguments(arguments)) return false;
    TextWriter writer; writer_init(&writer,output,capacity);
    writer_text(&writer,"1|"); writer_uint(&writer,sequence); writer_char(&writer,'|');
    writer_text(&writer,command); writer_char(&writer,'|'); writer_text(&writer,arguments);
    if (!writer.valid) return false;
    uint16_t crc=protocol_crc16((const uint8_t *)output,writer.length);
    writer_char(&writer,'|');
    const char *hex="0123456789ABCDEF";
    for (int shift=12; shift>=0; shift-=4) writer_char(&writer,hex[(crc>>shift)&15u]);
    writer_char(&writer,'\n');
    if (!writer.valid || writer.length>PHOTO_FRAME_MAX) return false;
    *length=writer.length;
    return true;
}

void protocol_init(ProtocolParser *parser) { memset(parser, 0, sizeof(*parser)); }

/* Return 0 on success, 1 for CRC mismatch and 2 for invalid structure. */
static int parse_frame(const uint8_t *raw, size_t length, Frame *frame) {
    char work[PHOTO_FRAME_MAX];
    memcpy(work,raw,length); work[length]='\0';
    char *fields[5]; size_t count=1;
    fields[0]=work;
    for (size_t i=0; i<length; ++i) {
        if (work[i]=='|') {
            if (count==5u) return 2;
            work[i]='\0'; fields[count++]=work+i+1;
        }
    }
    if (count!=5u || strcmp(fields[0],"1")!=0 ||
        !valid_command(fields[2]) || !valid_arguments(fields[3]) ||
        !parse_u16(fields[1],&frame->sequence) || strlen(fields[4])!=4u) return 2;
    uint16_t expected=0;
    for (unsigned i=0; i<4; ++i) {
        char c=fields[4][i];
        if (!((c>='0' && c<='9') || (c>='A' && c<='F'))) return 2;
        expected=(uint16_t)((expected<<4)|(uint16_t)(c<='9' ? c-'0' : c-'A'+10));
    }
    size_t crc_length=(size_t)(fields[4]-work-1);
    if (protocol_crc16(raw,crc_length)!=expected) return 1;
    strcpy(frame->command,fields[2]); strcpy(frame->arguments,fields[3]);
    memcpy(frame->raw,raw,length); frame->raw[length]='\0';
    return 0;
}

void protocol_feed(ProtocolParser *parser, const uint8_t *data, size_t length,
                   FrameCallback callback, void *context) {
    if (parser == NULL || (data == NULL && length)) return;
    for (size_t i=0; i<length; ++i) {
        uint8_t value=data[i];
        if (value=='\n') {
            if (!parser->discarding) {
                Frame frame;
                int result=parse_frame(parser->buffer,parser->length,&frame);
                if (result==1) ++parser->crc_errors;
                else if (result==2) ++parser->structure_errors;
                else if (callback != NULL) callback(&frame,context);
            }
            parser->discarding=false; parser->length=0;
        } else if (!parser->discarding) {
            if (value<32u || value>126u) {
                ++parser->structure_errors; parser->discarding=true; parser->length=0;
            } else if (parser->length>=PHOTO_FRAME_MAX-1u) {
                ++parser->overflow_errors; parser->discarding=true; parser->length=0;
            } else {
                parser->buffer[parser->length++]=value;
            }
        }
    }
}
