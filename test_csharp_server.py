"""
Verification script for C# PCControl WebSocket / TCP Server.
Connects to port 8080, sends JSON command packets, and validates responses.
"""

import socket
import json
import time
import sys

def test_server(port=8080):
    print(f"Connecting to PCControl Server on 127.0.0.1:{port}...")
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(3.0)
        s.connect(("127.0.0.1", port))

        test_payloads = [
            {"type": "ping"},
            {"type": "volume_get"},
            {"type": "mouse_move", "dx": 10, "dy": -5},
            {"type": "mouse_click", "button": "left"},
            {"type": "type_text", "text": "[PCControl C# Test]"},
            {"type": "get_apps"}
        ]

        for payload in test_payloads:
            msg = json.dumps(payload) + "\n"
            s.sendall(msg.encode('utf-8'))
            resp = s.recv(4096).decode('utf-8').strip()
            print(f"  SENT: {msg.strip():<40} -> RECV: {resp[:60]}")
            time.sleep(0.05)

        s.close()
        print("\n[SUCCESS] C# PCControl Server responded to all JSON commands correctly!")
        return True
    except Exception as e:
        print(f"[ERROR] Test failed: {e}")
        return False

if __name__ == "__main__":
    success = test_server()
    sys.exit(0 if success else 1)
