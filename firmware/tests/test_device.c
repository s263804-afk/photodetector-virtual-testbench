#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "device.h"
static unsigned checks;
#define CHECK(expr) do { ++checks; if (!(expr)) { \
    fprintf(stderr,"FAIL line %d: %s\n",__LINE__,#expr); exit(1); } } while (0)
typedef struct { char bytes[513]; unsigned count; } Sent;
typedef struct { Frame frame; unsigned count; } Decoded;
static void send_bytes(const char *bytes, size_t length, void *context) {
    Sent *sent=context; CHECK(length<sizeof(sent->bytes));
    memcpy(sent->bytes,bytes,length); sent->bytes[length]='\0'; ++sent->count;
}
static void decode_frame(const Frame *frame, void *context) {
    Decoded *decoded=context; decoded->frame=*frame; ++decoded->count;
}
static Frame call(Device *device, Sent *sent, uint16_t sequence,
                  const char *command, const char *arguments) {
    char raw[513]; size_t length;
    CHECK(protocol_encode(sequence,command,arguments,raw,sizeof(raw),&length));
    sent->count=0;
    device_feed(device,(const uint8_t *)raw,length);
    CHECK(sent->count==1);
    ProtocolParser parser; protocol_init(&parser);
    Decoded decoded={0};
    protocol_feed(&parser,(const uint8_t *)sent->bytes,strlen(sent->bytes),decode_frame,&decoded);
    CHECK(decoded.count==1);
    CHECK(decoded.frame.sequence==sequence);
    return decoded.frame;
}
static void expect(Frame frame,const char *command) { CHECK(strcmp(frame.command,command)==0); }
int main(void) {
    Device device; Sent sent={0};
    device_init(&device,send_bytes,&sent);
    expect(call(&device,&sent,1,"PING","-"),"ERR_SESSION");
    CHECK(device.processed==0); CHECK(device.state==NEEDS_HELLO);
    expect(call(&device,&sent,0,"HELLO","-"),"HELLO");
    CHECK(device.state==STOPPED); CHECK(device.expected_sequence==1);
    expect(call(&device,&sent,1,"FEED","100"),"ERR_STATE");
    CHECK(device.expected_sequence==2); CHECK(device.processed==0);
    expect(call(&device,&sent,2,"SET_LIMITS","100,50"),"ERR_ARGUMENT");
    CHECK(device.low==16 && device.high==4079);
    expect(call(&device,&sent,0,"HELLO","-"),"HELLO");
    expect(call(&device,&sent,1,"START","-"),"OK");
    expect(call(&device,&sent,2,"PING","-"),"PONG");
    Frame data=call(&device,&sent,3,"FEED","124,869,4095");
    expect(data,"DATA");
    CHECK(strcmp(data.arguments,"0;124:124:0;869:496:0;4095:1696:2")==0);
    CHECK(strcmp(sent.bytes,"1|3|DATA|0;124:124:0;869:496:0;4095:1696:2|609D\n")==0);
    char cached[513]; strcpy(cached,sent.bytes);
    expect(call(&device,&sent,3,"FEED","124,869,4095"),"DATA");
    CHECK(strcmp(cached,sent.bytes)==0); CHECK(device.processed==3);
    expect(call(&device,&sent,3,"FEED","124,870,4095"),"ERR_SEQUENCE");
    CHECK(device.processed==3); CHECK(device.expected_sequence==4);
    Frame status=call(&device,&sent,4,"STATUS","-");
    expect(status,"STATUS"); CHECK(strstr(status.arguments,"processed=3;")!=NULL);
    expect(call(&device,&sent,5,"START","-"),"ERR_STATE");
    expect(call(&device,&sent,6,"SET_LIMITS","0,100"),"ERR_STATE");
    expect(call(&device,&sent,7,"STOP","-"),"OK");
    expect(call(&device,&sent,8,"STOP","-"),"OK");
    expect(call(&device,&sent,9,"FEED","100"),"ERR_STATE");
    expect(call(&device,&sent,10,"START","-"),"OK");
    CHECK(device.processed==0);
    const char *invalid[]={"-1","1.5","4096","1,,2","-",
                          "1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17"};
    uint16_t seq=11;
    for (size_t i=0;i<sizeof(invalid)/sizeof(invalid[0]);++i) {
        expect(call(&device,&sent,seq++,"FEED",invalid[i]),"ERR_ARGUMENT");
        CHECK(device.processed==0); CHECK(device.filter.count==0);
    }
    data=call(&device,&sent,seq++,"FEED","100,200");
    CHECK(strcmp(data.arguments,"0;100:100:0;200:150:0")==0);
    expect(call(&device,&sent,0,"HELLO","-"),"HELLO");
    CHECK(device.state==STOPPED && device.processed==0 && !device.has_replay);
    expect(call(&device,&sent,1,"SET_LIMITS","100,1000"),"OK");
    expect(call(&device,&sent,2,"START","-"),"OK");
    data=call(&device,&sent,3,"FEED","100,1000");
    CHECK(strcmp(data.arguments,"0;100:100:1;1000:550:2")==0);
    device_init(&device,send_bytes,&sent);
    expect(call(&device,&sent,4,"STATUS","-"),"ERR_SESSION");
    expect(call(&device,&sent,0,"HELLO","-"),"HELLO");
    device.expected_sequence=65535;
    expect(call(&device,&sent,65535,"PING","-"),"PONG");
    CHECK(device.expected_sequence==1);
    expect(call(&device,&sent,1,"PING","-"),"PONG");
    printf("Device: %u assertions passed\n",checks);
    return 0;
}
