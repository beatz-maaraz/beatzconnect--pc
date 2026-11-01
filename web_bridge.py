"""
Web Controller Bridge
Serves the mobile touch-pad web app on HTTP port 8000 and bridges WebSocket traffic (port 8081)
to the C++ Remote Control Server TCP port 8080.
"""

import asyncio
import http.server
import socket
import socketserver
import threading
import websockets

HTTP_PORT = 8000
WS_PORT = 8081
TCP_SERVER_PORT = 8080
TCP_SERVER_HOST = "127.0.0.1"

def start_http_server():
    class CustomHandler(http.server.SimpleHTTPRequestHandler):
        def do_GET(self):
            if self.path in ("/", "/index.html"):
                self.path = "/web_remote_controller.html"
            return super().do_GET()

    with socketserver.TCPServer(("", HTTP_PORT), CustomHandler) as httpd:
        print(f"[HTTP] Mobile controller web page served at http://0.0.0.0:{HTTP_PORT}")
        httpd.serve_forever()

async def ws_handler(websocket):
    print(f"[WS] Mobile client connected to WebSocket bridge: {websocket.remote_address}")
    try:
        # Connect to C++ TCP server
        reader, writer = await asyncio.open_connection(TCP_SERVER_HOST, TCP_SERVER_PORT)
        # Read welcome banner from C++ server
        banner = await reader.readline()
        print(f"[C++ TCP] {banner.decode().strip()}")

        async def pump_ws_to_tcp():
            async for message in websocket:
                cmd = message.strip() + "\n"
                writer.write(cmd.encode('utf-8'))
                await writer.drain()

        async def pump_tcp_to_ws():
            while True:
                data = await reader.readline()
                if not data:
                    break
                await websocket.send(data.decode('utf-8'))

        await asyncio.gather(pump_ws_to_tcp(), pump_tcp_to_ws())

    except Exception as e:
        print(f"[WS Bridge] Session ended: {e}")
    finally:
        print("[WS] Client disconnected")

async def main():
    # Start HTTP server in background thread
    http_thread = threading.Thread(target=start_http_server, daemon=True)
    http_thread.start()

    print(f"[WS] WebSocket bridge listening on ws://0.0.0.0:{WS_PORT}")
    async with websockets.serve(ws_handler, "0.0.0.0", WS_PORT):
        await asyncio.Future() # run forever

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        print("\n[INFO] Bridge stopped.")
