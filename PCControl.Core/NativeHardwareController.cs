using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PCControl.Core
{
    public enum MouseButton
    {
        Left,
        Right,
        Middle
    }

    public record WindowInfo(IntPtr Handle, string Title, string ProcessName, int ProcessId);

    public class NativeHardwareController
    {
        #region Win32 P/Invoke & Constants

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;

        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_WHEEL = 0x0800;
        private const uint MOUSEEVENTF_HWHEEL = 0x1000;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        private const uint WM_CLOSE = 0x0010;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUTUNION u;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern bool LockWorkStation();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (IntPtr)(-4);

        #endregion

        static NativeHardwareController()
        {
            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch { }
        }

        #region Mouse Control

        public void MoveMouseRelative(int dx, int dy)
        {
            if (GetCursorPos(out POINT pt))
            {
                int screenX = GetSystemMetrics(SM_XVIRTUALSCREEN);
                int screenY = GetSystemMetrics(SM_YVIRTUALSCREEN);
                int screenW = GetSystemMetrics(SM_CXVIRTUALSCREEN);
                int screenH = GetSystemMetrics(SM_CYVIRTUALSCREEN);

                if (screenW == 0) screenW = GetSystemMetrics(SM_CXSCREEN);
                if (screenH == 0) screenH = GetSystemMetrics(SM_CYSCREEN);

                int targetX = Math.Clamp(pt.X + dx, screenX, screenX + screenW - 1);
                int targetY = Math.Clamp(pt.Y + dy, screenY, screenY + screenH - 1);

                SetCursorPos(targetX, targetY);
            }
            else
            {
                INPUT input = new()
                {
                    type = INPUT_MOUSE,
                    u = new INPUTUNION
                    {
                        mi = new MOUSEINPUT
                        {
                            dx = dx,
                            dy = dy,
                            dwFlags = MOUSEEVENTF_MOVE
                        }
                    }
                };
                SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
            }
        }

        public void SetMousePosition(int x, int y)
        {
            SetCursorPos(x, y);
        }

        public (int X, int Y) GetMousePosition()
        {
            if (GetCursorPos(out POINT pt))
                return (pt.X, pt.Y);
            return (0, 0);
        }

        public (int Width, int Height) GetScreenResolution()
        {
            return (GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
        }

        public void MouseButtonDown(MouseButton button)
        {
            uint flag = button switch
            {
                MouseButton.Left => MOUSEEVENTF_LEFTDOWN,
                MouseButton.Right => MOUSEEVENTF_RIGHTDOWN,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEDOWN,
                _ => MOUSEEVENTF_LEFTDOWN
            };

            INPUT input = new()
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flag } }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void MouseButtonUp(MouseButton button)
        {
            uint flag = button switch
            {
                MouseButton.Left => MOUSEEVENTF_LEFTUP,
                MouseButton.Right => MOUSEEVENTF_RIGHTUP,
                MouseButton.Middle => MOUSEEVENTF_MIDDLEUP,
                _ => MOUSEEVENTF_LEFTUP
            };

            INPUT input = new()
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flag } }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void MouseClick(MouseButton button)
        {
            MouseButtonDown(button);
            Thread.Sleep(15);
            MouseButtonUp(button);
        }

        public void MouseDoubleClick(MouseButton button)
        {
            MouseClick(button);
            Thread.Sleep(80);
            MouseClick(button);
        }

        public void MouseScrollVertical(int delta)
        {
            INPUT input = new()
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MOUSEEVENTF_WHEEL,
                        mouseData = unchecked((uint)delta)
                    }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void MouseScrollHorizontal(int delta)
        {
            INPUT input = new()
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MOUSEEVENTF_HWHEEL,
                        mouseData = unchecked((uint)delta)
                    }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        #endregion

        #region Keyboard & Typing Control

        public void KeyDown(ushort vkCode)
        {
            uint flags = 0;
            if (IsExtendedKey(vkCode)) flags |= KEYEVENTF_EXTENDEDKEY;

            INPUT input = new()
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vkCode,
                        wScan = (ushort)MapVirtualKey(vkCode, 0),
                        dwFlags = flags
                    }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void KeyUp(ushort vkCode)
        {
            uint flags = KEYEVENTF_KEYUP;
            if (IsExtendedKey(vkCode)) flags |= KEYEVENTF_EXTENDEDKEY;

            INPUT input = new()
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vkCode,
                        wScan = (ushort)MapVirtualKey(vkCode, 0),
                        dwFlags = flags
                    }
                }
            };
            SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
        }

        public void KeyPress(ushort vkCode)
        {
            KeyDown(vkCode);
            Thread.Sleep(15);
            KeyUp(vkCode);
        }

        public void SendHotkey(params ushort[] vkCodes)
        {
            if (vkCodes == null || vkCodes.Length == 0) return;

            foreach (var vk in vkCodes)
            {
                KeyDown(vk);
                Thread.Sleep(5);
            }

            Thread.Sleep(20);

            for (int i = vkCodes.Length - 1; i >= 0; i--)
            {
                KeyUp(vkCodes[i]);
                Thread.Sleep(5);
            }
        }

        public void TypeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            List<INPUT> inputs = new(text.Length * 2);

            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n')
                {
                    inputs.Add(CreateKeyInput(0x0D, 0, false));
                    inputs.Add(CreateKeyInput(0x0D, 0, true));
                    continue;
                }
                if (c == '\t')
                {
                    inputs.Add(CreateKeyInput(0x09, 0, false));
                    inputs.Add(CreateKeyInput(0x09, 0, true));
                    continue;
                }

                // Unicode character injection
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = KEYEVENTF_UNICODE
                        }
                    }
                });

                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new INPUTUNION
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                        }
                    }
                });
            }

            if (inputs.Count > 0)
            {
                SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
            }
        }

        private static INPUT CreateKeyInput(ushort vk, ushort scan, bool keyUp)
        {
            uint flags = keyUp ? KEYEVENTF_KEYUP : 0;
            if (IsExtendedKey(vk)) flags |= KEYEVENTF_EXTENDEDKEY;

            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = scan,
                        dwFlags = flags
                    }
                }
            };
        }

        private static bool IsExtendedKey(ushort vk)
        {
            return vk switch
            {
                0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2D or 0x2E or 0x90 or 0xA3 or 0xA5 => true,
                _ => false
            };
        }

        #endregion

        #region Windows Lock Screen & Pre-Login Unlocking

        public bool LockScreen()
        {
            return LockWorkStation();
        }

        /// <summary>
        /// Secure Pre-Login / Lock Screen Unlock Mechanism
        /// Injects keystrokes directly into the Winlogon desktop session to wake and submit PIN.
        /// </summary>
        public bool UnlockWithPin(string pin)
        {
            try
            {
                // Switch thread desktop to the active input desktop (which on the lock screen is Winlogon)
                IntPtr hDesktop = OpenInputDesktop(0, false, 0x01FF);
                if (hDesktop != IntPtr.Zero)
                {
                    SetThreadDesktop(hDesktop);
                }

                // 1. Wake screen / bring up PIN entry prompt (Space or Enter)
                KeyPress(0x20); // Spacebar
                Thread.Sleep(300);

                // 2. Type PIN digits
                TypeText(pin);
                Thread.Sleep(100);

                // 3. Press Enter to submit
                KeyPress(0x0D); // Enter

                if (hDesktop != IntPtr.Zero)
                {
                    CloseDesktop(hDesktop);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LockScreenManager] Error unlocking: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Power & System Actions

        public void SleepComputer()
        {
            SetSuspendState(false, true, false);
        }

        public void RestartComputer()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/r /t 0",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        public void ShutdownComputer()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown",
                Arguments = "/s /t 0",
                CreateNoWindow = true,
                UseShellExecute = false
            });
        }

        #endregion

        #region Active Window & App Management

        public List<WindowInfo> GetActiveWindows()
        {
            List<WindowInfo> windows = new();

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd)) return true;

                int length = GetWindowTextLength(hWnd);
                if (length == 0) return true;

                StringBuilder sb = new(length + 1);
                GetWindowText(hWnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (string.IsNullOrWhiteSpace(title) || title == "Program Manager") return true;

                GetWindowThreadProcessId(hWnd, out uint processId);
                string procName = "Unknown";
                try
                {
                    using Process proc = Process.GetProcessById((int)processId);
                    procName = proc.ProcessName;
                }
                catch { }

                windows.Add(new WindowInfo(hWnd, title, procName, (int)processId));
                return true;
            }, IntPtr.Zero);

            return windows;
        }

        public bool CloseWindow(IntPtr hWnd)
        {
            try
            {
                SendMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool SetBrightness(int percent)
        {
            try
            {
                percent = Math.Clamp(percent, 0, 100);
                using var searcher = new ManagementObjectSearcher(
                    new ManagementScope("root\\wmi"),
                    new SelectQuery("WmiMonitorBrightnessMethods")
                );

                using var instances = searcher.Get();
                foreach (ManagementObject instance in instances)
                {
                    instance.InvokeMethod("WmiSetBrightness", new object[] { uint.MaxValue, percent });
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
