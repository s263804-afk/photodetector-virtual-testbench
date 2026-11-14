"""E1 environment proof against our project-owned ARM smoke binary."""

import socket

with socket.create_connection(("127.0.0.1", 12345), timeout=5) as connection:
    connection.settimeout(5)
    connection.sendall(b"?")
    received = b""
    while b"ARM_SMOKE_OK\n" not in received:
        chunk = connection.recv(1024)
        if not chunk:
            raise RuntimeError("Emulated UART disconnected")
        received += chunk
    print(received.decode("ascii"))
    assert b"ARM_SMOKE_OK\n" in received
