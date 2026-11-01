# Windows C++ Remote Control Server

A high-performance C++ Windows Console Application that acts as a remote control server for mobile devices. It leverages **Winsock2** for low-latency TCP communication and the modern **Windows API (`SendInput`, Per-Monitor V2 DPI awareness)** to control mouse cursor movement, clicks, scrolling, keyboard input, Unicode text typing, and media keys.

---

## 🌟 Key Features

- **Network Communication (Winsock2)**:
  - Multi-threaded TCP listener supporting concurrent mobile client connections.
  - Automatically identifies and prints local Wi-Fi / LAN IP addresses (`GetAdaptersAddresses`) so you immediately know where to connect from your phone.
  - Configured with `TCP_NODELAY` (disabling Nagle's algorithm) to guarantee instantaneous, low-latency mouse and trackpad responsiveness.
  - Clean stream buffering that reliably handles partial packets and multi-command streams.
  - Configurable listening port via command-line arguments (default: `8080`).

- **Hardware Simulation (Windows API `SendInput`)**:
  - **Per-Monitor V2 DPI Awareness**: Coordinates are pixel-perfect and match your display resolution accurately without DPI virtualization scaling issues.
  - **Mouse Controls**: Relative cursor movement (`MOVE`), absolute position (`SETPOS`), left/right/middle clicks, double click, press-and-hold (drag & drop), and smooth vertical/horizontal mouse wheel scrolling.
  - **Keyboard Controls**: Virtual key press/release (`KEY`, `KEYDOWN`, `KEYUP`) supporting arrow keys, modifiers, functional keys (`F1`-`F24`), `ENTER`, `ESC`, `BACKSPACE`, etc.
  - **Hotkey Combinations**: Multi-key combos like `HOTKEY CTRL+C`, `HOTKEY WIN+D`, `HOTKEY ALT+TAB`.
  - **Unicode Text Injection**: `TYPE <text>` injects UTF-8 / UTF-16 characters directly using Windows API `KEYEVENTF_UNICODE`, eliminating keyboard layout mismatch issues.
  - **Multimedia & Volume Controls**: `MEDIA PLAY_PAUSE`, `MEDIA VOL_UP`, `MEDIA VOL_DOWN`, `MEDIA MUTE`, `MEDIA NEXT`, `MEDIA PREV`.

- **Console Experience**:
  - ANSI-color formatted terminal output.
  - Real-time logging of connected client IP endpoints and executed remote actions.
  - Graceful `Ctrl+C` / close handler (`SetConsoleCtrlHandler`) ensuring sockets and Winsock resources are freed cleanly.

---

## 📂 Project Structure

```
d:\Project\PC\
├── bin\
│   └── RemoteControlServer.exe      # Compiled 64-bit console executable
├── include\
│   ├── Common.h                     # Windows & Winsock headers, ANSI logging
│   ├── NetworkUtils.h               # Local network adapter IP detection
│   ├── InputController.h            # Windows API SendInput abstraction
│   ├── CommandDispatcher.h          # Command parser & protocol dispatcher
│   └── NetworkServer.h              # Winsock TCP server & client thread handler
├── src\
│   ├── main.cpp                     # Entry point, console UI, signal handling
│   ├── NetworkUtils.cpp             # Adapter enumeration implementation
│   ├── InputController.cpp          # Windows API mouse & keyboard execution
│   ├── CommandDispatcher.cpp        # Protocol command execution
│   └── NetworkServer.cpp            # Socket listening, accept loop & client thread
├── test_client.py                   # Automated test suite and interactive CLI
├── CMakeLists.txt                   # CMake configuration (C++17)
├── build.bat                        # Automated build script using MSVC
└── README.md                        # Documentation and Mobile Integration Guide
```

---

## 🛠️ Building the Project

### Option 1: Using `build.bat` (Recommended)
Open Command Prompt / PowerShell and run:
```cmd
build.bat
```
This automatically detects Microsoft Visual Studio, sets up the x64 environment, compiles the source files with `/O2` optimization, and creates `bin\RemoteControlServer.exe`.

### Option 2: Using CMake
```cmd
mkdir build
cd build
cmake ..
cmake --build . --config Release
```

---

## 🚀 Running the Server

Run the executable directly:
```cmd
.\bin\RemoteControlServer.exe
```

Or specify a custom port:
```cmd
.\bin\RemoteControlServer.exe 9000
```

When started, the console displays:
- Active local network IP address(es) (e.g. `192.168.1.34`).
- Listening port (e.g. `8080`).
- Real-time timestamped action logs when commands arrive.

---

## 📡 Remote Control Protocol

Commands are sent as UTF-8 plain text terminated by a newline (`\n` or `\r\n`).

| Command | Arguments | Description | Example |
| :--- | :--- | :--- | :--- |
| `MOVE` | `<dx> <dy>` | Relative cursor move (pixels) | `MOVE 10 -5` |
| `SETPOS` | `<x> <y>` | Absolute cursor position | `SETPOS 500 300` |
| `GETPOS` | *None* | Get current mouse position | `GETPOS` $\rightarrow$ `OK 500 300` |
| `CLICK` | `[LEFT\|RIGHT\|MIDDLE]` | Mouse button click | `CLICK LEFT` |
| `DBLCLICK`| `[LEFT\|RIGHT\|MIDDLE]` | Mouse button double click | `DBLCLICK LEFT` |
| `DOWN` | `[LEFT\|RIGHT\|MIDDLE]` | Mouse button down (drag start) | `DOWN LEFT` |
| `UP` | `[LEFT\|RIGHT\|MIDDLE]` | Mouse button up (drag drop) | `UP LEFT` |
| `SCROLL` | `<delta>` | Vertical mouse wheel scroll | `SCROLL 120` *(up)* or `-120` *(down)* |
| `HSCROLL`| `<delta>` | Horizontal wheel scroll | `HSCROLL 120` |
| `KEY` | `<key_name>` | Press and release a key | `KEY ENTER`, `KEY ESC`, `KEY TAB` |
| `KEYDOWN`| `<key_name>` | Hold key down | `KEYDOWN SHIFT` |
| `KEYUP` | `<key_name>` | Release key | `KEYUP SHIFT` |
| `HOTKEY` | `<k1>+<k2>` | Press multi-key combo | `HOTKEY CTRL+C`, `HOTKEY WIN+D` |
| `TYPE` | `<text>` | Direct Unicode text typing | `TYPE Hello from Phone! 🎉` |
| `MEDIA` | `<action>` | Media controls | `MEDIA PLAY`, `MEDIA VOL_UP`, `MEDIA MUTE` |
| `RESOLUTION` | *None* | Get screen resolution | `RESOLUTION` $\rightarrow$ `OK 1920 1080` |
| `PING` | *None* | Connection heartbeat | `PING` $\rightarrow$ `PONG` |
| `HELP` | *None* | Returns supported command list| `HELP` |
| `QUIT` | *None* | Disconnect session | `QUIT` $\rightarrow$ `BYE` |

*Note: The server also accepts standard JSON commands like `{"cmd":"move","dx":10,"dy":5}`.*

---

## 📱 Mobile Client Integration Examples

### 1. Flutter (Dart)
```dart
import 'dart:io';
import 'dart:convert';

class RemoteClient {
  Socket? _socket;

  Future<void> connect(String host, int port) async {
    _socket = await Socket.connect(host, port);
    _socket!.listen((data) {
      print("Server: ${utf8.decode(data)}");
    });
  }

  void moveMouse(int dx, int dy) {
    _socket?.write("MOVE $dx $dy\n");
  }

  void click([String btn = "LEFT"]) {
    _socket?.write("CLICK $btn\n");
  }

  void typeText(String text) {
    _socket?.write("TYPE $text\n");
  }

  void sendKey(String key) {
    _socket?.write("KEY $key\n");
  }
}
```

### 2. React Native / Node.js
```javascript
import TcpSocket from 'react-native-tcp-socket';

const client = TcpSocket.createConnection({ port: 8080, host: '192.168.1.34' }, () => {
  console.log('Connected to Windows Remote Server');
});

function move(dx, dy) {
  client.write(`MOVE ${dx} ${dy}\n`);
}

function click(button = 'LEFT') {
  client.write(`CLICK ${button}\n`);
}

function type(text) {
  client.write(`TYPE ${text}\n`);
}
```

### 3. Swift (iOS Network.framework)
```swift
import Network

class RemoteClient {
    private var connection: NWConnection?

    func connect(host: String, port: UInt16) {
        let hostEndpoint = NWEndpoint.Host(host)
        let portEndpoint = NWEndpoint.Port(rawValue: port)!
        connection = NWConnection(host: hostEndpoint, port: portEndpoint, using: .tcp)
        connection?.start(queue: .main)
    }

    func send(command: String) {
        let data = (command + "\n").data(using: .utf8)!
        connection?.send(content: data, completion: .idempotent)
    }
}
```

### 4. Kotlin (Android Socket)
```kotlin
import java.io.PrintWriter
import java.net.Socket
import kotlin.concurrent.thread

class RemoteClient(val host: String, val port: Int) {
    private var socket: Socket? = null
    private var writer: PrintWriter? = null

    fun connect() {
        thread {
            socket = Socket(host, port)
            writer = PrintWriter(socket!!.getOutputStream(), true)
        }
    }

    fun move(dx: Int, dy: Int) {
        thread { writer?.println("MOVE $dx $dy") }
    }

    fun click(button: String = "LEFT") {
        thread { writer?.println("CLICK $button") }
    }
}
```

---

## 🧪 Testing with Python Test Client

You can simulate a mobile client directly on your PC:

**Automated Test Suite:**
```cmd
python test_client.py
```

**Interactive Remote Shell:**
```cmd
python test_client.py --interactive
```
In interactive mode, type any command (such as `MOVE 50 50`, `CLICK LEFT`, `TYPE Testing`, `HOTKEY WIN+D`) and see your Windows PC react in real time!
