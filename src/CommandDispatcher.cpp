#include "../include/CommandDispatcher.h"
#include <sstream>

CommandDispatcher::CommandDispatcher(std::shared_ptr<InputController> inputController)
    : m_input(std::move(inputController)) {}

// =============================================================================
// Helpers
// =============================================================================

std::string CommandDispatcher::Trim(const std::string& str) {
    size_t first = str.find_first_not_of(" \t\r\n");
    if (first == std::string::npos) return "";
    size_t last = str.find_last_not_of(" \t\r\n");
    return str.substr(first, (last - first + 1));
}

std::vector<std::string> CommandDispatcher::Tokenize(const std::string& str) {
    std::vector<std::string> tokens;
    std::istringstream iss(str);
    std::string token;
    while (iss >> token) {
        tokens.push_back(token);
    }
    return tokens;
}

// Simple JSON string field extractor — no external library required.
// Finds  "key":"value"  or  "key": value  (for numbers/booleans).
static std::string JsonGetString(const std::string& json, const std::string& key) {
    std::string search = "\"" + key + "\"";
    size_t pos = json.find(search);
    if (pos == std::string::npos) return "";
    pos = json.find(':', pos + search.size());
    if (pos == std::string::npos) return "";
    // Skip whitespace
    pos = json.find_first_not_of(" \t", pos + 1);
    if (pos == std::string::npos) return "";
    if (json[pos] == '"') {
        // String value
        size_t start = pos + 1;
        size_t end   = json.find('"', start);
        if (end == std::string::npos) return "";
        return json.substr(start, end - start);
    } else {
        // Numeric / boolean value — stop at , } \n
        size_t end = json.find_first_of(",}\n", pos);
        if (end == std::string::npos) end = json.size();
        std::string val = json.substr(pos, end - pos);
        // Trim trailing whitespace
        size_t last = val.find_last_not_of(" \t\r");
        return (last != std::string::npos) ? val.substr(0, last + 1) : val;
    }
}

static double JsonGetDouble(const std::string& json, const std::string& key, double def = 0.0) {
    std::string val = JsonGetString(json, key);
    if (val.empty()) return def;
    try { return std::stod(val); } catch (...) { return def; }
}

static int JsonGetInt(const std::string& json, const std::string& key, int def = 0) {
    std::string val = JsonGetString(json, key);
    if (val.empty()) return def;
    try { return std::stoi(val); } catch (...) { return def; }
}

// =============================================================================
// Dispatch — handles BOTH Flutter JSON protocol AND legacy text commands
// =============================================================================

std::string CommandDispatcher::Dispatch(const std::string& rawLine, const std::string& clientIdentifier) {
    std::string line = Trim(rawLine);
    if (line.empty()) return "";

    // -------------------------------------------------------------------------
    // Flutter / WebSocket JSON protocol
    // Messages from the mobile app arrive as JSON objects like:
    //   {"type":"MOUSE_MOVE","dx":10.5,"dy":-3.2}
    // -------------------------------------------------------------------------
    if (line.front() == '{') {
        std::string type = JsonGetString(line, "type");

        // Uppercase for case-insensitive matching
        std::transform(type.begin(), type.end(), type.begin(),
                       [](unsigned char c) { return static_cast<char>(std::toupper(c)); });

        // -- Heartbeat --
        if (type == "PING") {
            return R"({"type":"PONG"})";
        }

        // -- Mouse Move --
        // {"type":"MOUSE_MOVE","dx":10.5,"dy":-3.2}
        if (type == "MOUSE_MOVE") {
            int dx = static_cast<int>(JsonGetDouble(line, "dx"));
            int dy = static_cast<int>(JsonGetDouble(line, "dy"));
            m_input->MoveMouseRelative(dx, dy);
            return ""; // fire-and-forget, no reply needed for performance
        }

        // -- Mouse Click --
        // {"type":"MOUSE_CLICK","button":"left","state":"tap"}
        // state: "tap" = single click, "double" = double click,
        //        "down" = press, "up" = release
        if (type == "MOUSE_CLICK") {
            std::string btnStr = JsonGetString(line, "button");
            std::string state  = JsonGetString(line, "state");
            if (btnStr.empty()) btnStr = "left";

            // Normalise button name
            std::transform(btnStr.begin(), btnStr.end(), btnStr.begin(),
                           [](unsigned char c) { return static_cast<char>(std::toupper(c)); });
            std::transform(state.begin(),  state.end(),  state.begin(),
                           [](unsigned char c) { return static_cast<char>(std::toupper(c)); });

            bool ok = false;
            MouseButton btn = InputController::ResolveMouseButton(btnStr, ok);
            if (!ok) btn = MouseButton::Left;

            if (state == "DOUBLE") {
                m_input->MouseDoubleClick(btn);
            } else if (state == "DOWN") {
                m_input->MouseButtonDown(btn);
            } else if (state == "UP") {
                m_input->MouseButtonUp(btn);
            } else {
                // Default: single tap / click
                m_input->MouseClick(btn);
            }
            Logger::Log(Logger::Level::Command,
                "[" + clientIdentifier + "] Mouse " + state + " " + btnStr);
            return "";
        }

        // -- Mouse Scroll --
        // {"type":"MOUSE_SCROLL","deltaY":120}   (positive = scroll down in Flutter coords)
        if (type == "MOUSE_SCROLL") {
            // Flutter deltaY is pixels; convert to Windows WHEEL_DELTA units.
            // Positive deltaY from Flutter means the user scrolled UP on the touchpad.
            double rawDelta = JsonGetDouble(line, "deltaY");
            // Negate: Flutter positive deltaY → upward scroll → Windows positive wheel
            int delta = static_cast<int>(-rawDelta * 1.5);
            m_input->MouseScrollVertical(delta);
            return "";
        }

        // -- Volume --
        // {"type":"VOLUME_SET","level":75,"mute":false}
        if (type == "VOLUME_SET") {
            bool mute = (JsonGetString(line, "mute") == "true");
            if (mute) {
                m_input->TriggerMediaAction(MediaAction::VolumeMute);
                Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] Volume mute");
            } else {
                int level = JsonGetInt(line, "level", -1);
                // We don't have absolute volume set on Windows without Core Audio,
                // so we map levels to incremental presses of VK_VOLUME_UP/DOWN.
                // "level" is the desired absolute level (0–100). We use the direction
                // as an indication since we can't read current volume here.
                // A simple heuristic: if level > 50 press VolumeUp once, else VolumeDown.
                // For a better UX we send the event directly; the Flutter UI handles throttling.
                if (level > 50) {
                    m_input->TriggerMediaAction(MediaAction::VolumeUp);
                } else {
                    m_input->TriggerMediaAction(MediaAction::VolumeDown);
                }
                Logger::Log(Logger::Level::Command,
                    "[" + clientIdentifier + "] Volume target: " + std::to_string(level));
            }
            return "";
        }

        // -- Media Control --
        // {"type":"MEDIA_CONTROL","action":"play_pause"}
        if (type == "MEDIA_CONTROL") {
            std::string action = JsonGetString(line, "action");
            std::transform(action.begin(), action.end(), action.begin(),
                           [](unsigned char c) { return static_cast<char>(std::toupper(c)); });

            bool ok = false;
            MediaAction act = InputController::ResolveMediaAction(action, ok);
            if (ok) {
                m_input->TriggerMediaAction(act);
                Logger::Log(Logger::Level::Command,
                    "[" + clientIdentifier + "] Media: " + action);
            }
            return "";
        }

        // -- Keyboard Input (text typing) --
        // {"type":"KEYBOARD_INPUT","text":"Hello World"}
        if (type == "KEYBOARD_INPUT") {
            // Text can contain spaces, so we need the raw value between quotes
            std::string text = JsonGetString(line, "text");
            if (!text.empty()) {
                m_input->TypeText(text);
                Logger::Log(Logger::Level::Command,
                    "[" + clientIdentifier + "] Typed " + std::to_string(text.size()) + " chars");
            }
            return "";
        }

        // -- System Power --
        // {"type":"SYSTEM_POWER","action":"shutdown"|"restart"|"sleep"|"lock"}
        if (type == "SYSTEM_POWER") {
            std::string action = JsonGetString(line, "action");
            std::transform(action.begin(), action.end(), action.begin(),
                           [](unsigned char c) { return static_cast<char>(std::toupper(c)); });

            if (action == "SHUTDOWN") {
                Logger::Log(Logger::Level::Warning, "[" + clientIdentifier + "] Power: SHUTDOWN");
                system("shutdown /s /t 10");
            } else if (action == "RESTART") {
                Logger::Log(Logger::Level::Warning, "[" + clientIdentifier + "] Power: RESTART");
                system("shutdown /r /t 10");
            } else if (action == "SLEEP") {
                Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] Power: SLEEP");
                SetSuspendState(FALSE, FALSE, FALSE);
            } else if (action == "LOCK") {
                Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] Power: LOCK");
                LockWorkStation();
            } else if (action == "CANCEL_SHUTDOWN") {
                system("shutdown /a");
            }
            return "";
        }

        // -- Unlock (biometric token from mobile) --
        // {"type":"SYSTEM_UNLOCK","token":"..."}
        if (type == "SYSTEM_UNLOCK") {
            // For now, acknowledge the unlock — the PC-side lock/unlock
            // integration would require the Windows credential provider API.
            Logger::Log(Logger::Level::Info, "[" + clientIdentifier + "] Unlock request received.");
            return R"({"pcState":"UNLOCKED"})";
        }

        // -- App List Request --
        // {"type":"APP_LIST"}
        if (type == "APP_LIST") {
            // Return empty app list — full process enumeration can be added later
            Logger::Log(Logger::Level::Info, "[" + clientIdentifier + "] App list requested.");
            return R"({"type":"APP_LIST","apps":[]})";
        }

        // -- App Config (settings from mobile) --
        // {"type":"APP_CONFIG","sensitivity":1.5,"autoStart":false,"brightness":80}
        if (type == "APP_CONFIG") {
            Logger::Log(Logger::Level::Info, "[" + clientIdentifier + "] Config received.");
            // Config acknowledged — individual settings are applied on mobile side
            return "";
        }

        // Unknown JSON type — log and ignore (don't flood console with mouse moves)
        if (type != "MOUSE_MOVE") {
            Logger::Log(Logger::Level::Warning,
                "[" + clientIdentifier + "] Unknown JSON type: " + type);
        }
        return "";
    }

    // -------------------------------------------------------------------------
    // Legacy text protocol (for backward compatibility / testing via telnet)
    // e.g.: MOVE 10 5, CLICK LEFT, MEDIA VOLUP, PING, HELP, QUIT
    // -------------------------------------------------------------------------

    auto tokens = Tokenize(line);
    if (tokens.empty()) return "OK\n";

    std::string cmd = tokens[0];
    std::transform(cmd.begin(), cmd.end(), cmd.begin(), [](unsigned char c) {
        return static_cast<char>(std::toupper(c));
    });

    try {
        if (cmd == "MOVE") {
            if (tokens.size() < 3) return "ERR Usage: MOVE <dx> <dy>\n";
            m_input->MoveMouseRelative(std::stoi(tokens[1]), std::stoi(tokens[2]));
            return "OK\n";
        }

        if (cmd == "SETPOS") {
            if (tokens.size() < 3) return "ERR Usage: SETPOS <x> <y>\n";
            m_input->SetMousePosition(std::stoi(tokens[1]), std::stoi(tokens[2]));
            Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] Set cursor: (" + tokens[1] + "," + tokens[2] + ")");
            return "OK\n";
        }

        if (cmd == "GETPOS") {
            int x = 0, y = 0;
            m_input->GetMousePosition(x, y);
            return "OK " + std::to_string(x) + " " + std::to_string(y) + "\n";
        }

        if (cmd == "CLICK") {
            std::string btnStr = (tokens.size() >= 2) ? tokens[1] : "LEFT";
            bool ok = false;
            MouseButton btn = InputController::ResolveMouseButton(btnStr, ok);
            if (!ok) return "ERR Unknown button: " + btnStr + "\n";
            m_input->MouseClick(btn);
            Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] Click: " + btnStr);
            return "OK\n";
        }

        if (cmd == "DBLCLICK") {
            std::string btnStr = (tokens.size() >= 2) ? tokens[1] : "LEFT";
            bool ok = false;
            MouseButton btn = InputController::ResolveMouseButton(btnStr, ok);
            if (!ok) return "ERR Unknown button: " + btnStr + "\n";
            m_input->MouseDoubleClick(btn);
            Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] DblClick: " + btnStr);
            return "OK\n";
        }

        if (cmd == "DOWN") {
            std::string btnStr = (tokens.size() >= 2) ? tokens[1] : "LEFT";
            bool ok = false;
            MouseButton btn = InputController::ResolveMouseButton(btnStr, ok);
            if (!ok) return "ERR Unknown button\n";
            m_input->MouseButtonDown(btn);
            return "OK\n";
        }

        if (cmd == "UP") {
            std::string btnStr = (tokens.size() >= 2) ? tokens[1] : "LEFT";
            bool ok = false;
            MouseButton btn = InputController::ResolveMouseButton(btnStr, ok);
            if (!ok) return "ERR Unknown button\n";
            m_input->MouseButtonUp(btn);
            return "OK\n";
        }

        if (cmd == "SCROLL" || cmd == "VSCROLL") {
            if (tokens.size() < 2) return "ERR Usage: SCROLL <delta>\n";
            m_input->MouseScrollVertical(std::stoi(tokens[1]));
            return "OK\n";
        }

        if (cmd == "HSCROLL") {
            if (tokens.size() < 2) return "ERR Usage: HSCROLL <delta>\n";
            m_input->MouseScrollHorizontal(std::stoi(tokens[1]));
            return "OK\n";
        }

        if (cmd == "KEY") {
            if (tokens.size() < 2) return "ERR Usage: KEY <key_name>\n";
            WORD vk = InputController::ResolveVirtualKey(tokens[1]);
            if (vk == 0) return "ERR Unknown key: " + tokens[1] + "\n";
            m_input->KeyPress(vk);
            Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] Key: " + tokens[1]);
            return "OK\n";
        }

        if (cmd == "KEYDOWN") {
            if (tokens.size() < 2) return "ERR Usage: KEYDOWN <key_name>\n";
            WORD vk = InputController::ResolveVirtualKey(tokens[1]);
            if (vk == 0) return "ERR Unknown key: " + tokens[1] + "\n";
            m_input->KeyDown(vk);
            return "OK\n";
        }

        if (cmd == "KEYUP") {
            if (tokens.size() < 2) return "ERR Usage: KEYUP <key_name>\n";
            WORD vk = InputController::ResolveVirtualKey(tokens[1]);
            if (vk == 0) return "ERR Unknown key: " + tokens[1] + "\n";
            m_input->KeyUp(vk);
            return "OK\n";
        }

        if (cmd == "HOTKEY") {
            if (tokens.size() < 2) return "ERR Usage: HOTKEY <k1>+<k2>\n";
            std::string combo = tokens[1];
            std::vector<WORD> keys;
            std::stringstream ss(combo);
            std::string part;
            bool valid = true;
            while (std::getline(ss, part, '+')) {
                part = Trim(part);
                if (part.empty()) continue;
                WORD vk = InputController::ResolveVirtualKey(part);
                if (vk == 0) { valid = false; break; }
                keys.push_back(vk);
            }
            if (!valid || keys.empty()) return "ERR Invalid hotkey: " + combo + "\n";
            m_input->SendHotkey(keys);
            Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] Hotkey: " + combo);
            return "OK\n";
        }

        if (cmd == "TYPE") {
            size_t typePos = line.find_first_of(" \t");
            if (typePos == std::string::npos || typePos + 1 >= line.size())
                return "ERR Usage: TYPE <text>\n";
            std::string textToType = line.substr(typePos + 1);
            m_input->TypeText(textToType);
            Logger::Log(Logger::Level::Command,
                "[" + clientIdentifier + "] Typed " + std::to_string(textToType.length()) + " chars");
            return "OK\n";
        }

        if (cmd == "MEDIA") {
            if (tokens.size() < 2) return "ERR Usage: MEDIA <action>\n";
            bool ok = false;
            MediaAction act = InputController::ResolveMediaAction(tokens[1], ok);
            if (!ok) return "ERR Unknown media action: " + tokens[1] + "\n";
            m_input->TriggerMediaAction(act);
            Logger::Log(Logger::Level::Command, "[" + clientIdentifier + "] Media: " + tokens[1]);
            return "OK\n";
        }

        if (cmd == "RESOLUTION") {
            int w = 0, h = 0;
            m_input->GetScreenResolution(w, h);
            return "OK " + std::to_string(w) + " " + std::to_string(h) + "\n";
        }

        if (cmd == "PING") {
            return "PONG\n";
        }

        if (cmd == "HELP") {
            return
                "OK Supported Commands:\n"
                "  MOVE <dx> <dy>                     - Relative mouse movement\n"
                "  SETPOS <x> <y>                     - Absolute mouse position\n"
                "  GETPOS                             - Returns current cursor position\n"
                "  CLICK [LEFT|RIGHT|MIDDLE]          - Click mouse button\n"
                "  DBLCLICK [LEFT|RIGHT|MIDDLE]       - Double click\n"
                "  DOWN / UP [LEFT|RIGHT|MIDDLE]      - Mouse button press/release\n"
                "  SCROLL <delta>                     - Vertical scroll (120 / -120)\n"
                "  HSCROLL <delta>                    - Horizontal scroll\n"
                "  KEY <key_name>                     - Key press\n"
                "  KEYDOWN / KEYUP <key_name>         - Individual key state\n"
                "  HOTKEY <k1>+<k2>                   - Combo (e.g. CTRL+C, WIN+D)\n"
                "  TYPE <text>                        - Unicode text typing\n"
                "  MEDIA <PLAY|NEXT|PREV|VOLUP|VOLDOWN|MUTE>\n"
                "  RESOLUTION                         - Screen dimensions\n"
                "  PING                               - Heartbeat\n"
                "  QUIT                               - Disconnect\n";
        }

        if (cmd == "QUIT" || cmd == "EXIT" || cmd == "DISCONNECT") {
            return "BYE\n";
        }

    } catch (const std::exception& e) {
        return "ERR " + std::string(e.what()) + "\n";
    }

    return "ERR Unknown command: " + cmd + ". Send HELP for list.\n";
}
