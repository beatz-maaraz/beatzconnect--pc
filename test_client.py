"""
Mobile Remote Control Test Client (Python)
Simulates a mobile app connecting via TCP socket to the C++ Remote Control Server.
Supports automated test suite and interactive CLI mode.
"""

import socket
import sys
import time
import argparse

def run_automated_tests(host: str, port: int):
    print(f"\n==========================================")
    print(f"Connecting to Remote Control Server at {host}:{port}...")
    print(f"==========================================")

    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(5.0)
        s.connect((host, port))
        
        # Read welcome banner
        banner = s.recv(1024).decode('utf-8', errors='ignore')
        print(f"[RECV BANNER] {banner.strip()}")
        assert "REMOTE CONTROL SERVER READY" in banner, "Invalid server banner"

        commands = [
            ("PING", "PONG"),
            ("RESOLUTION", "OK"),
            ("GETPOS", "OK"),
            ("MOVE 15 15", "OK"),
            ("MOVE -15 -15", "OK"),
            ("CLICK LEFT", "OK"),
            ("SCROLL 120", "OK"),
            ("SCROLL -120", "OK"),
            ("TYPE [RemoteControlServer OK]", "OK"),
            ("KEY BACKSPACE", "OK"),
            ('{"cmd":"move","dx":5,"dy":5}', "OK"),
            ("HELP", "OK Supported Commands:"),
            ("QUIT", "BYE")
        ]

        print(f"\n[RUNNING TEST CASES]")
        for cmd, expected in commands:
            send_data = cmd + "\n"
            s.sendall(send_data.encode('utf-8'))
            response = s.recv(2048).decode('utf-8', errors='ignore').strip()
            print(f"  SEND: {cmd:<35} -> RECV: {response[:45]}")
            if expected not in response:
                print(f"  [FAIL] Expected '{expected}' in response, got '{response}'")
                s.close()
                return False
            time.sleep(0.05)

        s.close()
        print(f"\n==========================================")
        print(f"[SUCCESS] All remote control commands executed successfully!")
        print(f"==========================================\n")
        return True

    except Exception as e:
        print(f"\n[ERROR] Connection failed: {e}")
        return False

def run_interactive(host: str, port: int):
    print(f"\nConnecting to {host}:{port} in interactive mode...")
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.connect((host, port))
        banner = s.recv(1024).decode('utf-8', errors='ignore')
        print(banner.strip())
        print("\nEnter command (or 'exit' to quit):")
        while True:
            cmd = input("remote> ").strip()
            if not cmd:
                continue
            if cmd.lower() in ("exit", "quit"):
                s.sendall(b"QUIT\n")
                print(s.recv(1024).decode('utf-8', errors='ignore').strip())
                break
            s.sendall((cmd + "\n").encode('utf-8'))
            resp = s.recv(2048).decode('utf-8', errors='ignore').strip()
            print(resp)
        s.close()
    except Exception as e:
        print(f"Error: {e}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Remote Control Client Simulator")
    parser.add_argument("--host", default="127.0.0.1", help="Server host IP (default: 127.0.0.1)")
    parser.add_argument("--port", type=int, default=8080, help="Server port (default: 8080)")
    parser.add_argument("--interactive", action="store_true", help="Launch interactive prompt")

    args = parser.parse_args()

    if args.interactive:
        run_interactive(args.host, args.port)
    else:
        success = run_automated_tests(args.host, args.port)
        sys.exit(0 if success else 1)
