# Protocol v1 — frozen before E3 implementation

ASCII frames: version|sequence|command|arguments|CRC4 followed by LF.
Maximum 512 bytes including LF; CR/non-ASCII/control bytes are invalid.
Version is exactly 1. Sequence is unsigned decimal 0..65535. Command is 1..16 uppercase
letters/underscores. Arguments are 1..400 printable ASCII characters, excluding pipe.
CRC4 is exactly four uppercase hexadecimal digits, CRC-16/CCITT-FALSE over bytes before
the final pipe. Polynomial 1021, initial FFFF, no reflection/XOR. 123456789 → 29B1.

HELLO uses sequence0 and arguments "-" and returns:
1|0|HELLO|fw=1.0.0;protocol=1;mode=VIRTUAL_FEED|CRC
It starts a new logical session in STOPPED with expected sequence1 and clears replay state.
The initial/reset state is NEEDS_HELLO. Any valid non-HELLO in that state returns ERR_SESSION
without execution or sequence consumption.

Other requests use expected sequence1..65535, wrapping to1. Structurally/CRC-valid expected
requests consume sequence even on semantic errors. An identical immediately previous
request returns the exact cached response; changed payload at the same sequence or any
other unexpected sequence returns ERR_SEQUENCE and does not consume the expected sequence.

| Command | Arguments | Successful response command/arguments |
| --- | --- | --- |
| PING | - | PONG / - |
| SET_LIMITS | low,high | OK / - |
| START | - | OK / - |
| FEED | 1..16 codes, comma-separated | DATA / start_index;raw:filtered:flags;... |
| STATUS | - | STATUS / state=...;processed=...;crc_errors=...;overflow_errors=...;errors=... |
| STOP | - | OK / - |

Response sequence equals request sequence; response frames use the same CRC and byte limit.
HELLO/STATUS are the only tagged payloads. Status state names: NEEDS_HELLO, STOPPED, RUNNING.
Default low/high limits16/4079; valid0<=low<high<=4095. START and SET_LIMITS require STOPPED.
START clears filter and sample index. STOP in STOPPED is an OK no-op; FEED requires RUNNING.
FEED codes must be decimal digits only, 0..4095; all tokens validate before any batch mutation.
Filter startup uses available count, then a 16-sample integer mean. Flags: low rail bit0
(raw<=low), high rail bit1 (raw>=high). These flags do not diagnose a physical fault.

Semantic errors: ERR_ARGUMENT, ERR_STATE, ERR_COMMAND. Structurally valid unexpected
sequences use ERR_SEQUENCE; no session uses ERR_SESSION. Error arguments are "-".
State checks precede argument semantics for START/SET_LIMITS/FEED; an error has no sample mutation.
Successful START resets the uint32 sample index; counters/index are unsigned 32-bit values.

Parser policy: malformed structure/encoding/hex is dropped and increments structure errors;
otherwise bad CRC is dropped and increments CRC errors. Neither receives a response or consumes
a sequence. An overlong line increments overflow once and discards through LF. The next good
frame is parsed normally. STATUS errors includes structure plus semantic errors.

Host: one outstanding operation; default response timeout2000 ms, at most three total attempts
with identical request bytes. Lost reply retries must not repeat FEED processing. Exhausted
retries and during/after-write cancellation close/invalidate the session and report
INTERRUPTED_INDETERMINATE. Before-write cancellation preserves an established session.
Disconnect aborts the test. Reconnect plus HELLO creates a labelled new session; old uncertain
operations are never replayed across it. A reset may retain TCP; ERR_SESSION must still
invalidate the host session and require a fresh HELLO.

UART TCP bytes are raw (no telnet negotiation), bound to127.0.0.1 by the project backend.
The Renode monitor is controlled through private stdin, not an exposed TCP port.
Synthetic input enters a software FEED boundary, not an ADC peripheral.
