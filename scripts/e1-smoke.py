"""Launch Renode on a no-space staging path; control it via private stdin, not a TCP monitor."""

import argparse
import binascii
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]
argument_parser = argparse.ArgumentParser()
argument_parser.add_argument("--protocol", action="store_true")
arguments = argument_parser.parse_args()
prefix = "e4" if arguments.protocol else "e1"
resc = "simulation/platform.resc" if arguments.protocol else "simulation/smoke.resc"
elf = "build/arm/photo-firmware.elf" if arguments.protocol else "build/arm/photo-smoke.elf"
TOOLS = Path(os.environ["LOCALAPPDATA"]) / "PortfolioTools"
RENODE = TOOLS / "renode-1.17.0/renode_1.17.0-portable/renode.exe"
STAGE = TOOLS / ("photobench-runs/" + prefix + "-smoke")
STAGE.mkdir(parents=True, exist_ok=True)
(STAGE / ".local").mkdir(exist_ok=True)
config = STAGE / ".local/renode-config"
config.write_text("[general]\nhistory-path = " + str(STAGE / ".local/monitor-history") +
                  "\ncompiler-cache-enabled = False\n", encoding="utf-8")
for relative in [
    resc, "simulation/platform.repl", "simulation/LoopbackTerminal.cs", elf,
]:
    dest = STAGE / relative
    dest.parent.mkdir(parents=True, exist_ok=True)
    if relative.endswith(".resc"):
        content = (ROOT / relative).read_text(encoding="utf-8")
        for path_prefix in ["simulation", "build", ".local"]:
            content = content.replace(
                "@" + path_prefix + "/", "@" + (STAGE / path_prefix).as_posix() + "/"
            )
        dest.write_text(content, encoding="utf-8")
    else:
        shutil.copy2(ROOT / relative, dest)
output = ROOT / ("reports/evidence/" + prefix + "-renode-smoke.log")
with output.open("wb") as log:
    process = subprocess.Popen(
        [str(RENODE), "--disable-xwt", "--hide-analyzers", "--plain", "--console",
         "--config", str(config), resc],
        cwd=STAGE, stdin=subprocess.PIPE, stdout=log, stderr=subprocess.STDOUT,
        creationflags=subprocess.CREATE_NO_WINDOW,
    )
    try:
        connection = None
        for _ in range(100):
            if process.poll() is not None:
                raise RuntimeError("Renode exited; inspect the smoke log")
            try:
                connection = socket.create_connection(("127.0.0.1", 12345), timeout=0.2)
                break
            except OSError:
                time.sleep(0.2)
        if connection is None:
            raise RuntimeError("UART did not start; inspect the smoke log")
        with connection:
            bind = subprocess.check_output([
                "powershell.exe", "-NoProfile", "-Command",
                "Get-NetTCPConnection -State Listen -LocalPort 12345 | "
                "Select-Object LocalAddress,LocalPort,OwningProcess | ConvertTo-Json -Compress",
            ])
            (ROOT / ("reports/evidence/" + prefix + "-bind.json")).write_bytes(bind)
            assert b"127.0.0.1" in bind
            connection.settimeout(5)
            if arguments.protocol:
                transcript = []
                with connection.makefile("rb") as reader:
                    def exchange(sequence, command, args, expected_command):
                        body = f"1|{sequence}|{command}|{args}".encode("ascii")
                        wire = body + b"|" + format(binascii.crc_hqx(body,0xFFFF),"04X").encode() + b"\n"
                        connection.sendall(wire)
                        while True:
                            raw = reader.readline(513)
                            if not raw:
                                raise RuntimeError("ARM UART disconnected")
                            if raw.startswith(b"1|"):
                                break
                        assert len(raw) <= 512 and raw.endswith(b"\n")
                        fields = raw.decode("ascii").rstrip("\n").split("|")
                        assert len(fields) == 5 and int(fields[1]) == sequence
                        assert int(fields[4],16) == binascii.crc_hqx(raw[:raw.rfind(b"|")],0xFFFF)
                        assert fields[2] == expected_command
                        transcript.append({"request":wire.decode(),"response":raw.decode()})
                        return fields[3]
                    hello = exchange(0,"HELLO","-","HELLO")
                    assert "mode=VIRTUAL_FEED" in hello
                    exchange(1,"START","-","OK")
                    exchange(2,"PING","-","PONG")
                    data = exchange(3,"FEED","124,869,4095","DATA")
                    assert data == "0;124:124:0;869:496:0;4095:1696:2"
                    assert exchange(3,"FEED","124,869,4095","DATA") == data
                    assert "processed=3;" in exchange(4,"STATUS","-","STATUS")
                    exchange(5,"STOP","-","OK")
                (ROOT / "reports/evidence/e4-arm-protocol.json").write_text(
                    json.dumps({"transcript":transcript,"verdict":"PASS"},indent=2)+"\n",
                    encoding="utf-8")
                print("ARM HELLO/START/FEED/replay/STATUS/STOP: PASS")
            else:
                connection.sendall(b"?")
                received = b""
                while b"ARM_SMOKE_OK\n" not in received:
                    data = connection.recv(1024)
                    if not data:
                        raise RuntimeError("UART disconnected")
                    received += data
                print(received.decode("ascii"))
                (ROOT / "reports/evidence/e1-arm-uart.txt").write_bytes(received)
        print("ARM ELF SHA256:", hashlib.sha256((STAGE / elf).read_bytes()).hexdigest())
    finally:
        if process.poll() is None:
            process.stdin.write(b"quit\n")
            process.stdin.flush()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
