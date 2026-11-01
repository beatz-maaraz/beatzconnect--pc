"""
WebSocket client verification for C# PCControl Access Point.
Connects over standard RFC 6455 WebSocket ws://127.0.0.1:8080/
"""

import asyncio
import json
import websockets
import sys

async def test_websocket():
    uri = "ws://127.0.0.1:8080/"
    print(f"Connecting to WebSocket at {uri}...")
    try:
        async with websockets.connect(uri) as ws:
            print("Connected to C# WebSocket server!")

            # Ping
            await ws.send(json.dumps({"type": "ping"}))
            resp = await ws.recv()
            print(f"  RECV PING: {resp}")
            assert "pong" in resp

            # Volume query
            await ws.send(json.dumps({"type": "volume_get"}))
            resp = await ws.recv()
            print(f"  RECV VOLUME: {resp}")

            # Mouse move
            await ws.send(json.dumps({"type": "mouse_move", "dx": 20, "dy": 15}))
            resp = await ws.recv()
            print(f"  RECV MOVE: {resp}")

            print("\n[SUCCESS] RFC 6455 WebSocket test passed!")
            return True
    except Exception as e:
        print(f"[ERROR] WebSocket test failed: {e}")
        return False

if __name__ == "__main__":
    success = asyncio.run(test_websocket())
    sys.exit(0 if success else 1)
