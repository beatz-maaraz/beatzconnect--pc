#include "../include/InputController.h"
#include <unordered_map>

InputController::InputController() {
    // Ensure Per-Monitor V2 DPI awareness for pixel-perfect coordinates
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
}

void InputController::MoveMouseRelative(int dx, int dy) {
    POINT pt;
    if (GetCursorPos(&pt)) {
        int screenWidth = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int screenHeight = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        int screenX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int screenY = GetSystemMetrics(SM_YVIRTUALSCREEN);

        int newX = pt.x + dx;
        int newY = pt.y + dy;

        // Clamp to virtual screen boundaries
        if (newX < screenX) newX = screenX;
        if (newX >= screenX + screenWidth) newX = screenX + screenWidth - 1;
        if (newY < screenY) newY = screenY;
        if (newY >= screenY + screenHeight) newY = screenY + screenHeight - 1;

        SetCursorPos(newX, newY);
    } else {
        // Fallback to SendInput relative move
        INPUT input = {0};
        input.type = INPUT_MOUSE;
        input.mi.dx = dx;
        input.mi.dy = dy;
        input.mi.dwFlags = MOUSEEVENTF_MOVE;
        SendInput(1, &input, sizeof(INPUT));
    }
}

void InputController::SetMousePosition(int x, int y) {
    SetCursorPos(x, y);
}

void InputController::GetMousePosition(int& x, int& y) const {
    POINT pt = {0, 0};
    GetCursorPos(&pt);
    x = pt.x;
    y = pt.y;
}

void InputController::GetScreenResolution(int& width, int& height) const {
    width = GetSystemMetrics(SM_CXSCREEN);
    height = GetSystemMetrics(SM_CYSCREEN);
}

void InputController::MouseButtonDown(MouseButton button) {
    INPUT input = {0};
    input.type = INPUT_MOUSE;
    switch (button) {
        case MouseButton::Left:
            input.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
            break;
        case MouseButton::Right:
            input.mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
            break;
        case MouseButton::Middle:
            input.mi.dwFlags = MOUSEEVENTF_MIDDLEDOWN;
            break;
    }
    SendInput(1, &input, sizeof(INPUT));
}

void InputController::MouseButtonUp(MouseButton button) {
    INPUT input = {0};
    input.type = INPUT_MOUSE;
    switch (button) {
        case MouseButton::Left:
            input.mi.dwFlags = MOUSEEVENTF_LEFTUP;
            break;
        case MouseButton::Right:
            input.mi.dwFlags = MOUSEEVENTF_RIGHTUP;
            break;
        case MouseButton::Middle:
            input.mi.dwFlags = MOUSEEVENTF_MIDDLEUP;
            break;
    }
    SendInput(1, &input, sizeof(INPUT));
}

void InputController::MouseClick(MouseButton button) {
    MouseButtonDown(button);
    Sleep(15);
    MouseButtonUp(button);
}

void InputController::MouseDoubleClick(MouseButton button) {
    MouseClick(button);
    Sleep(60);
    MouseClick(button);
}

void InputController::MouseScrollVertical(int delta) {
    INPUT input = {0};
    input.type = INPUT_MOUSE;
    input.mi.dwFlags = MOUSEEVENTF_WHEEL;
    input.mi.mouseData = static_cast<DWORD>(delta);
    SendInput(1, &input, sizeof(INPUT));
}

void InputController::MouseScrollHorizontal(int delta) {
    INPUT input = {0};
    input.type = INPUT_MOUSE;
    input.mi.dwFlags = MOUSEEVENTF_HWHEEL;
    input.mi.mouseData = static_cast<DWORD>(delta);
    SendInput(1, &input, sizeof(INPUT));
}

void InputController::KeyDown(WORD vkCode) {
    INPUT input = {0};
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = vkCode;
    input.ki.wScan = static_cast<WORD>(MapVirtualKey(vkCode, MAPVK_VK_TO_VSC));
    input.ki.dwFlags = 0;
    
    // Check if it's an extended key
    if (vkCode == VK_RMENU || vkCode == VK_RCONTROL || 
        vkCode == VK_INSERT || vkCode == VK_DELETE || 
        vkCode == VK_HOME || vkCode == VK_END || 
        vkCode == VK_PRIOR || vkCode == VK_NEXT || 
        vkCode == VK_UP || vkCode == VK_DOWN || 
        vkCode == VK_LEFT || vkCode == VK_RIGHT || 
        vkCode == VK_NUMLOCK) {
        input.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
    }

    SendInput(1, &input, sizeof(INPUT));
}

void InputController::KeyUp(WORD vkCode) {
    INPUT input = {0};
    input.type = INPUT_KEYBOARD;
    input.ki.wVk = vkCode;
    input.ki.wScan = static_cast<WORD>(MapVirtualKey(vkCode, MAPVK_VK_TO_VSC));
    input.ki.dwFlags = KEYEVENTF_KEYUP;

    if (vkCode == VK_RMENU || vkCode == VK_RCONTROL || 
        vkCode == VK_INSERT || vkCode == VK_DELETE || 
        vkCode == VK_HOME || vkCode == VK_END || 
        vkCode == VK_PRIOR || vkCode == VK_NEXT || 
        vkCode == VK_UP || vkCode == VK_DOWN || 
        vkCode == VK_LEFT || vkCode == VK_RIGHT || 
        vkCode == VK_NUMLOCK) {
        input.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
    }

    SendInput(1, &input, sizeof(INPUT));
}

void InputController::KeyPress(WORD vkCode) {
    KeyDown(vkCode);
    Sleep(15);
    KeyUp(vkCode);
}

void InputController::SendHotkey(const std::vector<WORD>& keys) {
    if (keys.empty()) return;

    // Press all keys down in forward order
    for (WORD vk : keys) {
        KeyDown(vk);
        Sleep(5);
    }
    
    Sleep(20);

    // Release all keys in reverse order
    for (auto it = keys.rbegin(); it != keys.rend(); ++it) {
        KeyUp(*it);
        Sleep(5);
    }
}

void InputController::TypeText(const std::string& utf8Text) {
    if (utf8Text.empty()) return;

    int wideLen = MultiByteToWideChar(CP_UTF8, 0, utf8Text.c_str(), -1, nullptr, 0);
    if (wideLen <= 1) return;

    std::wstring wideStr(wideLen - 1, 0);
    MultiByteToWideChar(CP_UTF8, 0, utf8Text.c_str(), -1, &wideStr[0], wideLen);

    std::vector<INPUT> inputs;
    inputs.reserve(wideStr.size() * 2);

    for (wchar_t ch : wideStr) {
        if (ch == L'\r') {
            continue; // Skip \r in \r\n sequences
        }
        if (ch == L'\n') {
            INPUT down = {0};
            down.type = INPUT_KEYBOARD;
            down.ki.wVk = VK_RETURN;
            inputs.push_back(down);

            INPUT up = {0};
            up.type = INPUT_KEYBOARD;
            up.ki.wVk = VK_RETURN;
            up.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs.push_back(up);
            continue;
        }
        if (ch == L'\t') {
            INPUT down = {0};
            down.type = INPUT_KEYBOARD;
            down.ki.wVk = VK_TAB;
            inputs.push_back(down);

            INPUT up = {0};
            up.type = INPUT_KEYBOARD;
            up.ki.wVk = VK_TAB;
            up.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs.push_back(up);
            continue;
        }

        // Unicode character event
        INPUT down = {0};
        down.type = INPUT_KEYBOARD;
        down.ki.wScan = ch;
        down.ki.dwFlags = KEYEVENTF_UNICODE;
        inputs.push_back(down);

        INPUT up = {0};
        up.type = INPUT_KEYBOARD;
        up.ki.wScan = ch;
        up.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
        inputs.push_back(up);
    }

    if (!inputs.empty()) {
        SendInput(static_cast<UINT>(inputs.size()), inputs.data(), sizeof(INPUT));
    }
}

void InputController::TriggerMediaAction(MediaAction action) {
    WORD vk = 0;
    switch (action) {
        case MediaAction::PlayPause:
            vk = VK_MEDIA_PLAY_PAUSE;
            break;
        case MediaAction::NextTrack:
            vk = VK_MEDIA_NEXT_TRACK;
            break;
        case MediaAction::PreviousTrack:
            vk = VK_MEDIA_PREV_TRACK;
            break;
        case MediaAction::Stop:
            vk = VK_MEDIA_STOP;
            break;
        case MediaAction::VolumeUp:
            vk = VK_VOLUME_UP;
            break;
        case MediaAction::VolumeDown:
            vk = VK_VOLUME_DOWN;
            break;
        case MediaAction::VolumeMute:
            vk = VK_VOLUME_MUTE;
            break;
    }
    if (vk != 0) {
        KeyPress(vk);
    }
}

WORD InputController::ResolveVirtualKey(const std::string& keyName) {
    std::string upperKey = keyName;
    std::transform(upperKey.begin(), upperKey.end(), upperKey.begin(), [](unsigned char c) {
        return static_cast<char>(std::toupper(c));
    });

    static const std::unordered_map<std::string, WORD> keyMap = {
        // Special Keys
        {"ENTER", VK_RETURN},
        {"RETURN", VK_RETURN},
        {"ESC", VK_ESCAPE},
        {"ESCAPE", VK_ESCAPE},
        {"BACKSPACE", VK_BACK},
        {"BKSP", VK_BACK},
        {"TAB", VK_TAB},
        {"SPACE", VK_SPACE},
        {"DELETE", VK_DELETE},
        {"DEL", VK_DELETE},
        {"INSERT", VK_INSERT},
        {"HOME", VK_HOME},
        {"END", VK_END},
        {"PAGEUP", VK_PRIOR},
        {"PGUP", VK_PRIOR},
        {"PAGEDOWN", VK_NEXT},
        {"PGDN", VK_NEXT},
        {"UP", VK_UP},
        {"DOWN", VK_DOWN},
        {"LEFT", VK_LEFT},
        {"RIGHT", VK_RIGHT},
        
        // Modifiers
        {"CTRL", VK_CONTROL},
        {"LCTRL", VK_LCONTROL},
        {"RCTRL", VK_RCONTROL},
        {"CONTROL", VK_CONTROL},
        {"ALT", VK_MENU},
        {"LALT", VK_LMENU},
        {"RALT", VK_RMENU},
        {"SHIFT", VK_SHIFT},
        {"LSHIFT", VK_LSHIFT},
        {"RSHIFT", VK_RSHIFT},
        {"WIN", VK_LWIN},
        {"LWIN", VK_LWIN},
        {"RWIN", VK_RWIN},
        {"WINDOWS", VK_LWIN},

        // Locks & Misc
        {"CAPSLOCK", VK_CAPITAL},
        {"NUMLOCK", VK_NUMLOCK},
        {"SCROLLLOCK", VK_SCROLL},
        {"PRINTSCREEN", VK_SNAPSHOT},
        {"PRTSC", VK_SNAPSHOT},
        {"PAUSE", VK_PAUSE},

        // Media
        {"VOLUP", VK_VOLUME_UP},
        {"VOLUMEUP", VK_VOLUME_UP},
        {"VOLDOWN", VK_VOLUME_DOWN},
        {"VOLUMEDOWN", VK_VOLUME_DOWN},
        {"MUTE", VK_VOLUME_MUTE},
        {"PLAYPAUSE", VK_MEDIA_PLAY_PAUSE},
        {"NEXTTRACK", VK_MEDIA_NEXT_TRACK},
        {"PREVTRACK", VK_MEDIA_PREV_TRACK},

        // Function keys F1-F24
        {"F1", VK_F1},   {"F2", VK_F2},   {"F3", VK_F3},   {"F4", VK_F4},
        {"F5", VK_F5},   {"F6", VK_F6},   {"F7", VK_F7},   {"F8", VK_F8},
        {"F9", VK_F9},   {"F10", VK_F10}, {"F11", VK_F11}, {"F12", VK_F12},
        {"F13", VK_F13}, {"F14", VK_F14}, {"F15", VK_F15}, {"F16", VK_F16},
        {"F17", VK_F17}, {"F18", VK_F18}, {"F19", VK_F19}, {"F20", VK_F20},
        {"F21", VK_F21}, {"F22", VK_F22}, {"F23", VK_F23}, {"F24", VK_F24}
    };

    auto it = keyMap.find(upperKey);
    if (it != keyMap.end()) {
        return it->second;
    }

    // Single character A-Z or 0-9
    if (upperKey.length() == 1) {
        char c = upperKey[0];
        if (c >= 'A' && c <= 'Z') {
            return static_cast<WORD>(c);
        }
        if (c >= '0' && c <= '9') {
            return static_cast<WORD>(c);
        }
    }

    return 0;
}

MouseButton InputController::ResolveMouseButton(const std::string& btnName, bool& ok) {
    std::string upper = btnName;
    std::transform(upper.begin(), upper.end(), upper.begin(), [](unsigned char c) {
        return static_cast<char>(std::toupper(c));
    });

    ok = true;
    if (upper == "LEFT" || upper == "L" || upper == "1") return MouseButton::Left;
    if (upper == "RIGHT" || upper == "R" || upper == "2") return MouseButton::Right;
    if (upper == "MIDDLE" || upper == "M" || upper == "3") return MouseButton::Middle;

    ok = false;
    return MouseButton::Left;
}

MediaAction InputController::ResolveMediaAction(const std::string& actName, bool& ok) {
    std::string upper = actName;
    std::transform(upper.begin(), upper.end(), upper.begin(), [](unsigned char c) {
        return static_cast<char>(std::toupper(c));
    });

    ok = true;
    if (upper == "PLAY" || upper == "PAUSE" || upper == "PLAY_PAUSE" || upper == "PLAYPAUSE") return MediaAction::PlayPause;
    if (upper == "NEXT" || upper == "NEXT_TRACK") return MediaAction::NextTrack;
    if (upper == "PREV" || upper == "PREVIOUS" || upper == "PREV_TRACK") return MediaAction::PreviousTrack;
    if (upper == "STOP") return MediaAction::Stop;
    if (upper == "VOL_UP" || upper == "VOLUP" || upper == "VOLUME_UP") return MediaAction::VolumeUp;
    if (upper == "VOL_DOWN" || upper == "VOLDOWN" || upper == "VOLUME_DOWN") return MediaAction::VolumeDown;
    if (upper == "MUTE" || upper == "VOLUME_MUTE") return MediaAction::VolumeMute;

    ok = false;
    return MediaAction::PlayPause;
}
