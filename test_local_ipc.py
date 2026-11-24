"""
Test script to verify the local WebSocket IPC channel (ws://127.0.0.1:28888)
Adheres to Phase 4 Plan: Verify the local WebSocket/Pipe server is responsive before UI rendering.
"""

import asyncio
import json
import sys
import websockets

async def test_ipc():
    uri = "ws://127.0.0.1:28888/"
    print(f"[TEST] Connecting to Local IPC Server at {uri}...")
    try:
        async with websockets.connect(uri) as ws:
            print("[TEST] Connected successfully!")
            
            # 1. First message should be the instant state snapshot
            initial_snapshot = await asyncio.wait_for(ws.recv(), timeout=2.0)
            print(f"[TEST] Received Instant Snapshot: {initial_snapshot[:120]}...")
            data = json.loads(initial_snapshot)
            assert data["type"] == "state_update", f"Unexpected message type: {data}"
            state = data["data"]
            print(f"       Service Running: {state['serviceRunning']}")
            print(f"       Device Name:     {state['deviceName']}")
            print(f"       Is Connected:    {state['isConnected']}")
            print(f"       Battery Level:   {state['batteryPercentage']}%")
            print(f"       Master Volume:   {state['masterVolume']}%")

            # 2. Test ping-pong
            print("\n[TEST] Sending ping...")
            await ws.send(json.dumps({"type": "ping"}))
            pong_resp = await asyncio.wait_for(ws.recv(), timeout=2.0)
            print(f"[TEST] Ping Response: {pong_resp}")
            assert "pong" in pong_resp

            # 3. Test simulating a connected device
            print("\n[TEST] Simulating device connection: 'Samsung Galaxy S24 Ultra' (88% battery, charging)...")
            await ws.send(json.dumps({
                "type": "simulate_device",
                "deviceName": "Samsung Galaxy S24 Ultra",
                "batteryPercentage": 88,
                "isCharging": True,
                "isConnected": True
            }))
            
            # Wait for broadcast state_update
            update_msg = await asyncio.wait_for(ws.recv(), timeout=2.0)
            print(f"[TEST] Broadcasted update received: {update_msg[:120]}...")
            update_data = json.loads(update_msg)
            if update_data.get("type") == "state_update":
                new_state = update_data["data"]
                print(f"       Updated Device:  {new_state['deviceName']}")
                print(f"       Updated Battery: {new_state['batteryPercentage']}% (Charging: {new_state['isCharging']})")
                print(f"       Updated Status:  {'Connected' if new_state['isConnected'] else 'Disconnected'}")
                assert new_state["deviceName"] == "Samsung Galaxy S24 Ultra"
                assert new_state["batteryPercentage"] == 88

            print("\n[SUCCESS] Local IPC server verification passed completely!")
            return True
    except Exception as e:
        print(f"[ERROR] IPC Test failed: {e}")
        return False

if __name__ == "__main__":
    success = asyncio.run(test_ipc())
    sys.exit(0 if success else 1)

