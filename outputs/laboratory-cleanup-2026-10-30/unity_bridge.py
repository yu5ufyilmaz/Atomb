"""Call the project's existing loopback Unity MCP bridge (8-byte framed JSON)."""
import argparse
import json
from pathlib import Path
import socket
import struct

parser = argparse.ArgumentParser()
parser.add_argument('command')
parser.add_argument('params', nargs='?', default='{}')
parser.add_argument('--port', type=int, default=6421)
parser.add_argument('--timeout', type=float, default=30)
parser.add_argument('--out')
args = parser.parse_args()

def read_exact(connection, count):
    data = bytearray()
    while len(data) < count:
        chunk = connection.recv(count - len(data))
        if not chunk:
            raise ConnectionError('Unity closed the connection')
        data.extend(chunk)
    return bytes(data)

with socket.create_connection(('127.0.0.1', args.port), args.timeout) as connection:
    connection.settimeout(args.timeout)
    welcome = bytearray()
    while not welcome.endswith(b'\n'):
        welcome.extend(read_exact(connection, 1))
    if not welcome.startswith(b'WELCOME UNITY-MCP 1 FRAMING=1'):
        raise RuntimeError('Unexpected Unity bridge handshake')
    request = json.dumps({'type': args.command, 'params': json.loads(args.params)}, ensure_ascii=False).encode('utf-8')
    connection.sendall(struct.pack('>Q', len(request)) + request)
    count = struct.unpack('>Q', read_exact(connection, 8))[0]
    if count > 64 * 1024 * 1024:
        raise RuntimeError('Unity response is too large')
    result = json.loads(read_exact(connection, count))
    if args.out:
        Path(args.out).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(result, ensure_ascii=True))
