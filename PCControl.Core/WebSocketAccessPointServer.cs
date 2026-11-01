using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PCControl.Core
{
    public class CommandPacket
    {
        public string? type { get; set; }
        public int? dx { get; set; }
        public int? dy { get; set; }
        public int? x { get; set; }
        public int? y { get; set; }
        public string? button { get; set; }
        public string? state { get; set; }
        public int? delta { get; set; }
        public double? deltaY { get; set; }
        public string? text { get; set; }
        public string? key { get; set; }
        public string? combo { get; set; }
        public int? percent { get; set; }
        public bool? mute { get; set; }
        public string? action { get; set; }
        public string? pin { get; set; }
        public string? token { get; set; }
        public long? hwnd { get; set; }
        public int? pid { get; set; }
        public int? level { get; set; }
    }

    public class WebSocketAccessPointServer
    {
        private readonly int _port;
        private readonly NativeHardwareController _hardware;
        private readonly AudioEndpointController _audio;
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<string, NetworkStream> _clients = new();

        public event Action<string>? ClientConnected;
        public event Action<string>? ClientDisconnected;
        public event Action<string, string>? CommandExecuted;
        public event Action? PacketReceived;

        public bool IsRunning { get; private set; }
        public int ConnectedClientsCount => _clients.Count;

        public WebSocketAccessPointServer(int port, NativeHardwareController hardware, AudioEndpointController audio)
        {
            _port = port;
            _hardware = hardware;
            _audio = audio;
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _cts = new CancellationTokenSource();
                _tcpListener = new TcpListener(IPAddress.Any, _port);
                _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _tcpListener.Server.NoDelay = true;
                _tcpListener.Start();
                IsRunning = true;

                Task.Run(() => AcceptLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebSocketServer] Failed to start on port {_port}: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;

            IsRunning = false;
            _cts?.Cancel();

            try { _tcpListener?.Stop(); } catch { }

            foreach (var stream in _clients.Values)
            {
                try { stream.Close(); } catch { }
            }
            _clients.Clear();
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsRunning)
            {
                try
                {
                    TcpClient client = await _tcpListener!.AcceptTcpClientAsync(ct);
                    client.NoDelay = true;
                    string endpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";

                    _ = Task.Run(() => HandleClientAsync(client, endpoint, ct), ct);
                }
                catch when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    if (!IsRunning) break;
                    await Task.Delay(100, ct);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, string endpoint, CancellationToken ct)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                _clients[endpoint] = stream;
                ClientConnected?.Invoke(endpoint);

                try
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead <= 0) return;

                    string initialData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Check if HTTP WebSocket upgrade request
                    if (initialData.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase))
                    {
                        await PerformWebSocketHandshake(stream, initialData, ct);
                        await ProcessWebSocketFramesAsync(stream, endpoint, ct);
                    }
                    else
                    {
                        // Direct Raw TCP streaming protocol (supports newline-delimited text & JSON)
                        await ProcessRawTcpStreamAsync(stream, initialData, endpoint, ct);
                    }
                }
                catch { }
                finally
                {
                    _clients.TryRemove(endpoint, out _);
                    ClientDisconnected?.Invoke(endpoint);
                }
            }
        }

        #region WebSocket RFC 6455 Framing

        private async Task PerformWebSocketHandshake(NetworkStream stream, string header, CancellationToken ct)
        {
            string secKey = "";
            var match = Regex.Match(header, @"Sec-WebSocket-Key:\s*(.*)\r\n", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                secKey = match.Groups[1].Value.Trim();
            }

            string acceptKey = Convert.ToBase64String(
                SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(secKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))
            );

            string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                              "Upgrade: websocket\r\n" +
                              "Connection: Upgrade\r\n" +
                              $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length, ct);
            await stream.FlushAsync(ct);
        }

        private async Task ProcessWebSocketFramesAsync(NetworkStream stream, string endpoint, CancellationToken ct)
        {
            byte[] header = new byte[2];

            while (!ct.IsCancellationRequested && IsRunning)
            {
                int read = await stream.ReadAsync(header, 0, 2, ct);
                if (read < 2) break;

                bool isMasked = (header[1] & 0x80) != 0;
                ulong payloadLen = (ulong)(header[1] & 0x7F);

                if (payloadLen == 126)
                {
                    byte[] ext = new byte[2];
                    await stream.ReadAsync(ext, 0, 2, ct);
                    payloadLen = (ulong)((ext[0] << 8) | ext[1]);
                }
                else if (payloadLen == 127)
                {
                    byte[] ext = new byte[8];
                    await stream.ReadAsync(ext, 0, 8, ct);
                    payloadLen = BitConverter.ToUInt64(ext, 0);
                }

                byte[] masks = new byte[4];
                if (isMasked)
                {
                    await stream.ReadAsync(masks, 0, 4, ct);
                }

                byte[] payload = new byte[payloadLen];
                ulong totalRead = 0;
                while (totalRead < payloadLen)
                {
                    int r = await stream.ReadAsync(payload, (int)totalRead, (int)(payloadLen - totalRead), ct);
                    if (r <= 0) break;
                    totalRead += (ulong)r;
                }

                if (isMasked)
                {
                    for (ulong i = 0; i < payloadLen; i++)
                    {
                        payload[i] = (byte)(payload[i] ^ masks[i % 4]);
                    }
                }

                string message = Encoding.UTF8.GetString(payload);
                PacketReceived?.Invoke();
                string responseJson = ExecuteCommandPayload(message, endpoint);

                if (!string.IsNullOrEmpty(responseJson))
                {
                    await SendWebSocketFrameAsync(stream, responseJson, ct);
                }
            }
        }

        private async Task SendWebSocketFrameAsync(NetworkStream stream, string text, CancellationToken ct)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            List<byte> frame = new() { 0x81 }; // FIN + Text frame

            if (payload.Length <= 125)
            {
                frame.Add((byte)payload.Length);
            }
            else if (payload.Length <= 65535)
            {
                frame.Add(126);
                frame.Add((byte)((payload.Length >> 8) & 0xFF));
                frame.Add((byte)(payload.Length & 0xFF));
            }
            else
            {
                frame.Add(127);
                for (int i = 7; i >= 0; i--)
                    frame.Add((byte)((payload.Length >> (8 * i)) & 0xFF));
            }

            frame.AddRange(payload);
            byte[] data = frame.ToArray();
            await stream.WriteAsync(data, 0, data.Length, ct);
            await stream.FlushAsync(ct);
        }

        #endregion

        #region Raw TCP Stream Handler

        private async Task ProcessRawTcpStreamAsync(NetworkStream stream, string initialData, string endpoint, CancellationToken ct)
        {
            byte[] banner = Encoding.UTF8.GetBytes("{\"status\":\"READY\",\"server\":\"PCControl\",\"version\":\"1.0\"}\n");
            await stream.WriteAsync(banner, 0, banner.Length, ct);

            StringBuilder sb = new(initialData);
            byte[] buffer = new byte[4096];

            while (!ct.IsCancellationRequested && IsRunning)
            {
                string accumulated = sb.ToString();
                int newlineIdx;
                while ((newlineIdx = accumulated.IndexOf('\n')) >= 0)
                {
                    string line = accumulated.Substring(0, newlineIdx).Trim('\r', ' ', '\t');
                    accumulated = accumulated.Substring(newlineIdx + 1);
                    sb.Clear();
                    sb.Append(accumulated);

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        PacketReceived?.Invoke();
                        string resp = ExecuteCommandPayload(line, endpoint);
                        byte[] respBytes = Encoding.UTF8.GetBytes(resp + "\n");
                        await stream.WriteAsync(respBytes, 0, respBytes.Length, ct);
                    }
                }

                int read = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                if (read <= 0) break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
            }
        }

        #endregion

        #region Command Parsing & Execution

        public string ExecuteCommandPayload(string payload, string endpoint)
        {
            try
            {
                payload = payload.Trim();
                if (payload.StartsWith("{") && payload.EndsWith("}"))
                {
                    CommandPacket? packet = JsonSerializer.Deserialize<CommandPacket>(payload);
                    if (packet != null && !string.IsNullOrEmpty(packet.type))
                    {
                        return HandleJsonCommand(packet, endpoint);
                    }
                }
                else
                {
                    // Fallback to plain text tokens: MOVE 10 5, CLICK LEFT, etc.
                    return HandlePlainCommand(payload, endpoint);
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { status = "error", message = ex.Message });
            }

            return JsonSerializer.Serialize(new { status = "ok" });
        }

        private string HandleJsonCommand(CommandPacket cmd, string endpoint)
        {
            string type = cmd.type?.ToLowerInvariant() ?? "";

            switch (type)
            {
                case "mouse_move":
                case "trackpad-move":
                    _hardware.MoveMouseRelative(cmd.dx ?? 0, cmd.dy ?? 0);
                    CommandExecuted?.Invoke(endpoint, $"Move ({cmd.dx}, {cmd.dy})");
                    break;

                case "mouse_setpos":
                    _hardware.SetMousePosition(cmd.x ?? 0, cmd.y ?? 0);
                    CommandExecuted?.Invoke(endpoint, $"SetPos ({cmd.x}, {cmd.y})");
                    break;

                case "mouse_click":
                case "mouse-click":
                    MouseButton btn = ResolveButton(cmd.button);
                    string clickState = cmd.state?.ToLowerInvariant() ?? "tap";
                    if (clickState == "double")
                        _hardware.MouseDoubleClick(btn);
                    else if (clickState == "down")
                        _hardware.MouseButtonDown(btn);
                    else if (clickState == "up")
                        _hardware.MouseButtonUp(btn);
                    else
                        _hardware.MouseClick(btn);
                    CommandExecuted?.Invoke(endpoint, $"Click {btn} ({clickState})");
                    break;

                case "mouse_dblclick":
                    _hardware.MouseDoubleClick(ResolveButton(cmd.button));
                    CommandExecuted?.Invoke(endpoint, $"DblClick");
                    break;

                case "mouse_down":
                    _hardware.MouseButtonDown(ResolveButton(cmd.button));
                    CommandExecuted?.Invoke(endpoint, $"MouseDown");
                    break;

                case "mouse_up":
                    _hardware.MouseButtonUp(ResolveButton(cmd.button));
                    CommandExecuted?.Invoke(endpoint, $"MouseUp");
                    break;

                case "mouse_scroll":
                case "mouse-scroll":
                    int scrollAmount = cmd.delta ?? (cmd.deltaY.HasValue ? (int)Math.Round(-cmd.deltaY.Value * 1.5) : 0);
                    _hardware.MouseScrollVertical(scrollAmount);
                    break;

                case "mouse_hscroll":
                    _hardware.MouseScrollHorizontal(cmd.delta ?? 0);
                    break;

                case "type_text":
                case "keyboard_input":
                    _hardware.TypeText(cmd.text ?? "");
                    CommandExecuted?.Invoke(endpoint, $"Typed text ({cmd.text?.Length} chars)");
                    break;

                case "key_press":
                    ushort vk = ResolveVirtualKey(cmd.key ?? "");
                    if (vk != 0) _hardware.KeyPress(vk);
                    CommandExecuted?.Invoke(endpoint, $"Key {cmd.key}");
                    break;

                case "hotkey":
                    ExecuteHotkey(cmd.combo ?? "");
                    CommandExecuted?.Invoke(endpoint, $"Hotkey {cmd.combo}");
                    break;

                case "volume_set":
                case "volume-change":
                    if (cmd.mute == true)
                    {
                        _audio.SetMute(true);
                        CommandExecuted?.Invoke(endpoint, "Volume Set Mute");
                    }
                    else
                    {
                        int volTarget = cmd.percent ?? cmd.level ?? 50;
                        _audio.SetMasterVolumePercent(volTarget);
                        if (cmd.mute == false) _audio.SetMute(false);
                        CommandExecuted?.Invoke(endpoint, $"Volume Set {volTarget}%");
                    }
                    return JsonSerializer.Serialize(new { status = "ok", volume = _audio.GetMasterVolumePercent(), mute = _audio.GetMute() });

                case "volume_get":
                    return JsonSerializer.Serialize(new { status = "ok", volume = _audio.GetMasterVolumePercent(), mute = _audio.GetMute() });

                case "volume_mute":
                    if (cmd.mute.HasValue) _audio.SetMute(cmd.mute.Value);
                    else _audio.ToggleMute();
                    CommandExecuted?.Invoke(endpoint, $"Mute: {_audio.GetMute()}");
                    break;

                case "media":
                case "media_control":
                    ExecuteMediaAction(cmd.action ?? "");
                    CommandExecuted?.Invoke(endpoint, $"Media {cmd.action}");
                    break;

                case "lock":
                    _hardware.LockScreen();
                    CommandExecuted?.Invoke(endpoint, "Screen Locked");
                    return JsonSerializer.Serialize(new { status = "ok", action = "locked" });

                case "unlock":
                case "system_unlock":
                    string pinToUse = cmd.pin ?? cmd.token ?? "";
                    bool unlocked = _hardware.UnlockWithPin(pinToUse);
                    CommandExecuted?.Invoke(endpoint, $"Screen Unlock (Result: {unlocked})");
                    return JsonSerializer.Serialize(new { status = unlocked ? "ok" : "fail", action = "unlocked" });

                case "get_apps":
                case "app_list":
                    var apps = _hardware.GetActiveWindows();
                    return JsonSerializer.Serialize(new { status = "ok", apps });

                case "close_app":
                    if (cmd.hwnd.HasValue)
                    {
                        _hardware.CloseWindow((IntPtr)cmd.hwnd.Value);
                        CommandExecuted?.Invoke(endpoint, $"Closed Window Hwnd {cmd.hwnd.Value}");
                    }
                    break;

                case "app_action":
                    string appAct = cmd.action?.ToLowerInvariant() ?? "";
                    if (appAct == "kill" && cmd.pid.HasValue)
                    {
                        try
                        {
                            var proc = System.Diagnostics.Process.GetProcessById(cmd.pid.Value);
                            proc.Kill();
                            CommandExecuted?.Invoke(endpoint, $"Killed PID {cmd.pid.Value}");
                        }
                        catch (Exception ex)
                        {
                            CommandExecuted?.Invoke(endpoint, $"Kill error: {ex.Message}");
                        }
                    }
                    else if (appAct == "focus" && cmd.pid.HasValue)
                    {
                        try
                        {
                            var proc = System.Diagnostics.Process.GetProcessById(cmd.pid.Value);
                            if (proc.MainWindowHandle != IntPtr.Zero)
                            {
                                NativeHardwareController.SetForegroundWindow(proc.MainWindowHandle);
                                CommandExecuted?.Invoke(endpoint, $"Focused PID {cmd.pid.Value}");
                            }
                        }
                        catch {}
                    }
                    break;

                case "set_brightness":
                    _hardware.SetBrightness(cmd.level ?? 100);
                    CommandExecuted?.Invoke(endpoint, $"Brightness {cmd.level}%");
                    break;

                case "power":
                case "system_power":
                    string act = cmd.action?.ToLowerInvariant() ?? "";
                    if (act == "sleep") _hardware.SleepComputer();
                    else if (act == "restart") _hardware.RestartComputer();
                    else if (act == "shutdown") _hardware.ShutdownComputer();
                    else if (act == "lock") _hardware.LockScreen();
                    CommandExecuted?.Invoke(endpoint, $"Power: {act}");
                    break;

                case "ping":
                    return JsonSerializer.Serialize(new { status = "pong", time = DateTime.UtcNow.ToString("O") });

                default:
                    return JsonSerializer.Serialize(new { status = "error", message = "unknown_command" });
            }

            return JsonSerializer.Serialize(new { status = "ok" });
        }

        private string HandlePlainCommand(string line, string endpoint)
        {
            string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "OK";

            string cmd = parts[0].ToUpperInvariant();
            string args = parts.Length > 1 ? parts[1].Trim() : "";

            switch (cmd)
            {
                case "MOVE":
                    var coords = args.Split(' ');
                    if (coords.Length >= 2 && int.TryParse(coords[0], out int dx) && int.TryParse(coords[1], out int dy))
                    {
                        _hardware.MoveMouseRelative(dx, dy);
                    }
                    break;
                case "CLICK":
                    _hardware.MouseClick(ResolveButton(args));
                    break;
                case "TYPE":
                    _hardware.TypeText(args);
                    break;
                case "HOTKEY":
                    ExecuteHotkey(args);
                    break;
                case "LOCK":
                    _hardware.LockScreen();
                    break;
                case "PING":
                    return "PONG";
            }

            return "OK";
        }

        private static MouseButton ResolveButton(string? btn)
        {
            return (btn?.ToUpperInvariant()) switch
            {
                "RIGHT" => MouseButton.Right,
                "MIDDLE" => MouseButton.Middle,
                _ => MouseButton.Left
            };
        }

        private void ExecuteHotkey(string combo)
        {
            var parts = combo.Split('+');
            List<ushort> keys = new();
            foreach (var p in parts)
            {
                ushort vk = ResolveVirtualKey(p.Trim());
                if (vk != 0) keys.Add(vk);
            }
            if (keys.Count > 0)
            {
                _hardware.SendHotkey(keys.ToArray());
            }
        }

        private void ExecuteMediaAction(string action)
        {
            switch (action.ToLowerInvariant())
            {
                case "play_pause": _audio.PlayPause(); break;
                case "next": _audio.NextTrack(); break;
                case "prev": _audio.PreviousTrack(); break;
                case "vol_up": _audio.VolumeUp(); break;
                case "vol_down": _audio.VolumeDown(); break;
                case "mute": _audio.ToggleMute(); break;
            }
        }

        private static ushort ResolveVirtualKey(string keyName)
        {
            return keyName.ToUpperInvariant() switch
            {
                "ENTER" or "RETURN" => 0x0D,
                "ESC" or "ESCAPE" => 0x1B,
                "BACKSPACE" or "BKSP" => 0x08,
                "TAB" => 0x09,
                "SPACE" => 0x20,
                "DELETE" or "DEL" => 0x2E,
                "INSERT" => 0x2D,
                "HOME" => 0x24,
                "END" => 0x23,
                "PAGEUP" => 0x21,
                "PAGEDOWN" => 0x22,
                "UP" => 0x26,
                "DOWN" => 0x28,
                "LEFT" => 0x25,
                "RIGHT" => 0x27,
                "CTRL" or "CONTROL" => 0x11,
                "ALT" => 0x12,
                "SHIFT" => 0x10,
                "WIN" or "WINDOWS" => 0x5B,
                "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
                "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
                "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
                _ when keyName.Length == 1 && char.IsLetterOrDigit(keyName[0]) => (ushort)char.ToUpper(keyName[0]),
                _ => 0
            };
        }

        #endregion
    }
}
