#pragma once
#include "Common.h"
#include <string>
#include <vector>

enum class MouseButton {
    Left,
    Right,
    Middle
};

enum class MediaAction {
    PlayPause,
    NextTrack,
    PreviousTrack,
    Stop,
    VolumeUp,
    VolumeDown,
    VolumeMute
};

class InputController {
public:
    InputController();
    ~InputController() = default;

    // Mouse control
    void MoveMouseRelative(int dx, int dy);
    void SetMousePosition(int x, int y);
    void GetMousePosition(int& x, int& y) const;
    void GetScreenResolution(int& width, int& height) const;

    void MouseButtonDown(MouseButton button);
    void MouseButtonUp(MouseButton button);
    void MouseClick(MouseButton button);
    void MouseDoubleClick(MouseButton button);

    void MouseScrollVertical(int delta);
    void MouseScrollHorizontal(int delta);

    // Keyboard control
    void KeyDown(WORD vkCode);
    void KeyUp(WORD vkCode);
    void KeyPress(WORD vkCode);
    void SendHotkey(const std::vector<WORD>& keys);

    // High-level text typing (Unicode safe)
    void TypeText(const std::string& utf8Text);

    // Media & System controls
    void TriggerMediaAction(MediaAction action);

    // Helper: Map friendly string to Virtual Key Code
    static WORD ResolveVirtualKey(const std::string& keyName);
    static MouseButton ResolveMouseButton(const std::string& btnName, bool& ok);
    static MediaAction ResolveMediaAction(const std::string& actName, bool& ok);
};
