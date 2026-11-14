#include "device.h"
#include "bounded.h"
#include <string.h>

void device_init(Device *device, DeviceSend send, void *context) {
    memset(device,0,sizeof(*device));
    device->send=send; device->send_context=context;
    device->state=NEEDS_HELLO; device->expected_sequence=1;
    device->low=16; device->high=4079;
}

static void reply(Device *device, const Frame *frame, const char *command,
                  const char *arguments, Response *response, bool cache) {
    if (strncmp(command,"ERR_",4)==0) ++device->semantic_errors;
    if (!protocol_encode(frame->sequence,command,arguments,response->bytes,
                         sizeof(response->bytes),&response->length)) return;
    if (cache) {
        device->has_replay=true;
        device->last_sequence=frame->sequence;
        strcpy(device->last_request,frame->raw);
        device->last_response=*response;
    }
}

static bool parse_samples(const char *arguments, uint16_t *values, size_t *count) {
    char copy[PHOTO_ARGS_MAX+1]; strcpy(copy,arguments);
    char *cursor=copy; *count=0;
    for (;;) {
        if (*count>=16u) return false;
        char *comma=strchr(cursor,',');
        if (comma != NULL) *comma='\0';
        uint16_t value;
        if (!parse_u16(cursor,&value) || value>4095u) return false;
        values[(*count)++]=value;
        if (comma==NULL) return true;
        cursor=comma+1;
    }
}

bool sample_source_push(Device *device, const uint16_t *samples, size_t count,
                        SampleResult *results) {
    if (device==NULL || samples==NULL || results==NULL ||
        device->state!=RUNNING || count==0 || count>16) return false;
    for (size_t i=0;i<count;++i) if (samples[i]>4095u) return false;
    Filter next=device->filter;
    for (size_t i=0;i<count;++i) {
        uint16_t filtered;
        if (!filter_push(&next,samples[i],&filtered)) return false;
        results[i].raw=samples[i]; results[i].filtered=filtered;
        results[i].flags=(uint8_t)((samples[i]<=device->low ? 1u : 0u) |
                                  (samples[i]>=device->high ? 2u : 0u));
    }
    device->filter=next;
    device->processed+=(uint32_t)count;
    return true;
}

void device_handle(Device *device, const Frame *frame, Response *response) {
    if (response==NULL) return;
    response->length=0; response->bytes[0]='\0';
    if (device==NULL || frame==NULL) return;
    if (strcmp(frame->command,"HELLO")==0) {
        if (frame->sequence!=0) {
            reply(device,frame,"ERR_SEQUENCE","-",response,false);
        } else if (strcmp(frame->arguments,"-")!=0) {
            reply(device,frame,"ERR_ARGUMENT","-",response,false);
        } else {
            device->state=STOPPED; device->processed=0;
            device->expected_sequence=1; device->has_replay=false;
            filter_init(&device->filter);
            reply(device,frame,"HELLO","fw=1.0.0;protocol=1;mode=VIRTUAL_FEED",response,false);
        }
        return;
    }
    if (device->state==NEEDS_HELLO) {
        reply(device,frame,"ERR_SESSION","-",response,false); return;
    }
    if (device->has_replay && frame->sequence==device->last_sequence) {
        if (strcmp(frame->raw,device->last_request)==0) *response=device->last_response;
        else reply(device,frame,"ERR_SEQUENCE","-",response,false);
        return;
    }
    if (frame->sequence!=device->expected_sequence) {
        reply(device,frame,"ERR_SEQUENCE","-",response,false); return;
    }
    device->expected_sequence=(uint16_t)(frame->sequence==65535u ? 1u : frame->sequence+1u);
    const char *command="OK"; const char *arguments="-";
    char output[PHOTO_ARGS_MAX+1];
    if (strcmp(frame->command,"START")==0) {
        if (device->state!=STOPPED) command="ERR_STATE";
        else if (strcmp(frame->arguments,"-")!=0) command="ERR_ARGUMENT";
        else { device->state=RUNNING; device->processed=0; filter_init(&device->filter); }
    } else if (strcmp(frame->command,"STOP")==0) {
        if (strcmp(frame->arguments,"-")!=0) command="ERR_ARGUMENT";
        else device->state=STOPPED;
    } else if (strcmp(frame->command,"SET_LIMITS")==0) {
        if (device->state!=STOPPED) command="ERR_STATE";
        else {
            char copy[PHOTO_ARGS_MAX+1]; strcpy(copy,frame->arguments);
            char *comma=strchr(copy,',');
            uint16_t low=0,high=0;
            bool valid=false;
            if (comma!=NULL) {
                *comma='\0';
                valid=parse_u16(copy,&low) && parse_u16(comma+1,&high) &&
                      low<high && high<=4095u;
            }
            if (valid) { device->low=low; device->high=high; }
            else command="ERR_ARGUMENT";
        }
    } else if (strcmp(frame->command,"PING")==0) {
        command=strcmp(frame->arguments,"-")==0 ? "PONG" : "ERR_ARGUMENT";
    } else if (strcmp(frame->command,"STATUS")==0) {
        if (strcmp(frame->arguments,"-")!=0) command="ERR_ARGUMENT";
        else {
            command="STATUS";
            TextWriter writer; writer_init(&writer,output,sizeof(output));
            writer_text(&writer,"state=");
            writer_text(&writer,device->state==RUNNING ? "RUNNING" : "STOPPED");
            writer_text(&writer,";processed="); writer_uint(&writer,device->processed);
            writer_text(&writer,";crc_errors="); writer_uint(&writer,device->parser.crc_errors);
            writer_text(&writer,";overflow_errors="); writer_uint(&writer,device->parser.overflow_errors);
            writer_text(&writer,";errors=");
            writer_uint(&writer,device->semantic_errors+device->parser.structure_errors);
            arguments=output;
        }
    } else if (strcmp(frame->command,"FEED")==0) {
        if (device->state!=RUNNING) command="ERR_STATE";
        else {
            uint16_t values[16]; size_t count=0;
            if (!parse_samples(frame->arguments,values,&count)) command="ERR_ARGUMENT";
            else {
                SampleResult results[16]; uint32_t start=device->processed;
                if (!sample_source_push(device,values,count,results)) command="ERR_ARGUMENT";
                else {
                    command="DATA";
                    TextWriter writer; writer_init(&writer,output,sizeof(output));
                    writer_uint(&writer,start);
                    for (size_t i=0;i<count;++i) {
                        writer_char(&writer,';'); writer_uint(&writer,results[i].raw);
                        writer_char(&writer,':'); writer_uint(&writer,results[i].filtered);
                        writer_char(&writer,':'); writer_uint(&writer,results[i].flags);
                    }
                    arguments=output;
                }
            }
        }
    } else command="ERR_COMMAND";
    reply(device,frame,command,arguments,response,true);
}

static void on_frame(const Frame *frame, void *context) {
    Device *device=context; Response response;
    device_handle(device,frame,&response);
    if (response.length && device->send!=NULL)
        device->send(response.bytes,response.length,device->send_context);
}

void device_feed(Device *device, const uint8_t *bytes, size_t length) {
    if (device!=NULL) protocol_feed(&device->parser,bytes,length,on_frame,device);
}
