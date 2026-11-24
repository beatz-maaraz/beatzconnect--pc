using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PCControl.Core
{
    public class CompanionState
    {
        public bool ServiceRunning { get; set; } = true;
        public bool IsConnected { get; set; } = false;
        public string DeviceName { get; set; } = "Disconnected";
        public string Model { get; set; } = "Awaiting Pairing";
        public int BatteryPercentage { get; set; } = 100;
        public bool IsCharging { get; set; } = false;
        public string ClientEndpoint { get; set; } = "";
        public int ConnectedClientsCount { get; set; } = 0;
        public int MasterVolume { get; set; } = 50;
        public bool IsMuted { get; set; } = false;
        public string MachineName { get; set; } = Environment.MachineName;
        public string UserName { get; set; } = Environment.UserName;
        public int ActiveWindowsCount { get; set; } = 0;
        public bool RelayConnected { get; set; } = false;
        public string RelayRoomId { get; set; } = "";
        public string RelayUrl { get; set; } = "https://backend-n4yg.onrender.com";
        public int WifiSignal { get; set; } = 4;
        public int CellularSignal { get; set; } = 4;
        public bool BluetoothEnabled { get; set; } = true;
        public bool DoNotDisturb { get; set; } = false;
        public string CurrentTrack { get; set; } = "Beatz Ambient Stream";
        public string CurrentArtist { get; set; } = "Phone Audio Bridge";
        public bool IsPlaying { get; set; } = true;
        public string Version { get; set; } = "3.1.0";
        public string Timestamp { get; set; } = DateTime.UtcNow.ToString("O");
    }

    public class LocalIpcCommand
    {
        public string? type { get; set; }
        public int? volume { get; set; }
        public string? deviceName { get; set; }
        public int? batteryPercentage { get; set; }
        public bool? isCharging { get; set; }
        public bool? isConnected { get; set; }
    }

    public class LocalIpcServer
    {
        private readonly int _port;
        private readonly NativeHardwareController _hardware;
        private readonly AudioEndpointController _audio;
        private readonly WebSocketAccessPointServer _apServer;
        private TcpListener? _tcpListener;
        private CancellationTokenSource? _cts;
        private readonly ConcurrentDictionary<string, NetworkStream> _ipcClients = new();

        private bool _relayConnected = false;
        private string _relayRoomId = "";
        private bool _dnd = false;
        private bool _bluetooth = true;
        private string _track = "Midnight Lo-Fi Stream";
        private string _artist = "Beatz Mobile Studio";
        private bool _isPlaying = true;
        private Timer? _heartbeatTimer;

        public bool IsRunning { get; private set; }

        public LocalIpcServer(int port, NativeHardwareController hardware, AudioEndpointController audio, WebSocketAccessPointServer apServer)
        {
            _port = port;
            _hardware = hardware;
            _audio = audio;
            _apServer = apServer;

            _apServer.DeviceStateChanged += info =>
            {
                BroadcastState();
            };
        }

        public void UpdateRelayState(bool connected, string roomId)
        {
            _relayConnected = connected;
            _relayRoomId = roomId;
            BroadcastState();
        }

        public void Start()
        {
            if (IsRunning) return;

            try
            {
                _cts = new CancellationTokenSource();
                // Bind only to Loopback (127.0.0.1) for maximum security and zero firewall popups
                _tcpListener = new TcpListener(IPAddress.Loopback, _port);
                _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _tcpListener.Server.NoDelay = true;
                _tcpListener.Start();
                IsRunning = true;

                Task.Run(() => AcceptLoopAsync(_cts.Token));

                _heartbeatTimer = new Timer(_ =>
                {
                    if (IsRunning && !_ipcClients.IsEmpty)
                    {
                        BroadcastState();
                    }
                }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalIpcServer] Failed to bind to loopback port {_port}: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (!IsRunning) return;
            IsRunning = false;
            _cts?.Cancel();
            _heartbeatTimer?.Dispose();

            try { _tcpListener?.Stop(); } catch { }

            foreach (var stream in _ipcClients.Values)
            {
                try { stream.Close(); } catch { }
            }
            _ipcClients.Clear();
        }

        public CompanionState GetCurrentState()
        {
            var dev = _apServer.CurrentDevice;
            int vol = 50;
            bool mute = false;
            try
            {
                vol = _audio.GetMasterVolumePercent();
                mute = _audio.GetMute();
            }
            catch { }

            int winCount = 0;
            try
            {
                winCount = _hardware.GetActiveWindows().Count;
            }
            catch { }

            return new CompanionState
            {
                ServiceRunning = true,
                IsConnected = dev.IsConnected,
                DeviceName = dev.DeviceName,
                Model = dev.Model,
                BatteryPercentage = dev.BatteryPercentage,
                IsCharging = dev.IsCharging,
                ClientEndpoint = dev.Endpoint,
                ConnectedClientsCount = _apServer.ConnectedClientsCount,
                MasterVolume = vol,
                IsMuted = mute,
                MachineName = Environment.MachineName,
                UserName = Environment.UserName,
                ActiveWindowsCount = winCount,
                RelayConnected = _relayConnected,
                RelayRoomId = _relayRoomId,
                RelayUrl = "https://backend-n4yg.onrender.com",
                WifiSignal = 4,
                CellularSignal = 4,
                BluetoothEnabled = _bluetooth,
                DoNotDisturb = _dnd,
                CurrentTrack = _track,
                CurrentArtist = _artist,
                IsPlaying = _isPlaying,
                Version = "3.1.0",
                Timestamp = DateTime.UtcNow.ToString("O")
            };
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public void BroadcastState()
        {
            var state = GetCurrentState();
            string json = JsonSerializer.Serialize(new { type = "state_update", data = state }, JsonOpts);

            foreach (var kvp in _ipcClients)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await SendWebSocketFrameAsync(kvp.Value, json, CancellationToken.None);
                    }
                    catch
                    {
                        _ipcClients.TryRemove(kvp.Key, out _);
                    }
                });
            }
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && IsRunning)
            {
                try
                {
                    TcpClient client = await _tcpListener!.AcceptTcpClientAsync(ct);
                    client.NoDelay = true;
                    string endpoint = client.Client.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString();

                    _ = Task.Run(() => HandleIpcClientAsync(client, endpoint, ct), ct);
                }
                catch when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    if (!IsRunning) break;
                    await Task.Delay(100, ct);
                }
            }
        }

        private async Task HandleIpcClientAsync(TcpClient client, string endpoint, CancellationToken ct)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                _ipcClients[endpoint] = stream;

                try
                {
                    byte[] buffer = new byte[8192];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead <= 0) return;

                    string initialData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (initialData.Contains("Upgrade: websocket", StringComparison.OrdinalIgnoreCase))
                    {
                        await PerformWebSocketHandshake(stream, initialData, ct);

                        // Immediately dispatch snapshot so UI doesn't have to wait or poll!
                        var snapshot = JsonSerializer.Serialize(new { type = "state_update", data = GetCurrentState() }, JsonOpts);
                        await SendWebSocketFrameAsync(stream, snapshot, ct);

                        await ProcessWebSocketFramesAsync(stream, endpoint, ct);
                    }
                    else
                    {
                        // Raw JSON line fallback (for curl / pipe-style tools)
                        string snapshot = JsonSerializer.Serialize(new { type = "state_update", data = GetCurrentState() }, JsonOpts) + "\n";
                        byte[] bytes = Encoding.UTF8.GetBytes(snapshot);
                        await stream.WriteAsync(bytes, 0, bytes.Length, ct);
                    }
                }
                catch { }
                finally
                {
                    _ipcClients.TryRemove(endpoint, out _);
                }
            }
        }

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

                int opcode = header[0] & 0x0F;
                if (opcode == 0x8) // Connection Close
                {
                    break;
                }

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

                // Ping frame (0x9): Reply with Pong (0xA) immediately
                if (opcode == 0x9)
                {
                    byte[] pongHeader = new byte[] { 0x8A, (byte)payload.Length };
                    await stream.WriteAsync(pongHeader, 0, pongHeader.Length, ct);
                    if (payload.Length > 0)
                    {
                        await stream.WriteAsync(payload, 0, payload.Length, ct);
                    }
                    await stream.FlushAsync(ct);
                    continue;
                }

                // Pong frame (0xA)
                if (opcode == 0xA)
                {
                    continue;
                }

                string message = Encoding.UTF8.GetString(payload);
                string responseJson = ProcessIpcMessage(message);

                if (!string.IsNullOrEmpty(responseJson))
                {
                    await SendWebSocketFrameAsync(stream, responseJson, ct);
                }
            }
        }

        private string ProcessIpcMessage(string json)
        {
            try
            {
                var cmd = JsonSerializer.Deserialize<LocalIpcCommand>(json);
                if (cmd == null || string.IsNullOrEmpty(cmd.type)) return "";

                string type = cmd.type.ToLowerInvariant();
                switch (type)
                {
                    case "ping":
                        return JsonSerializer.Serialize(new { type = "pong", time = DateTime.UtcNow.ToString("O") });

                    case "get_state":
                        return JsonSerializer.Serialize(new { type = "state_update", data = GetCurrentState() }, JsonOpts);

                    case "set_volume":
                        if (cmd.volume.HasValue)
                        {
                            _audio.SetMasterVolumePercent(cmd.volume.Value);
                            BroadcastState();
                        }
                        return JsonSerializer.Serialize(new { status = "ok", volume = _audio.GetMasterVolumePercent() });

                    case "toggle_mute":
                        _audio.ToggleMute();
                        BroadcastState();
                        return JsonSerializer.Serialize(new { status = "ok", mute = _audio.GetMute() });

                    case "media_play_pause":
                        _audio.PlayPause();
                        _isPlaying = !_isPlaying;
                        BroadcastState();
                        return JsonSerializer.Serialize(new { status = "ok", isPlaying = _isPlaying });

                    case "media_next":
                        _audio.NextTrack();
                        return JsonSerializer.Serialize(new { status = "ok" });

                    case "media_prev":
                        _audio.PreviousTrack();
                        return JsonSerializer.Serialize(new { status = "ok" });

                    case "toggle_dnd":
                        _dnd = !_dnd;
                        BroadcastState();
                        return JsonSerializer.Serialize(new { status = "ok", dnd = _dnd });

                    case "toggle_bluetooth":
                        _bluetooth = !_bluetooth;
                        BroadcastState();
                        return JsonSerializer.Serialize(new { status = "ok", bluetooth = _bluetooth });

                    case "lock_pc":
                        _hardware.LockScreen();
                        return JsonSerializer.Serialize(new { status = "ok", action = "locked" });

                    case "sleep":
                        _hardware.SleepComputer();
                        return JsonSerializer.Serialize(new { status = "ok", action = "sleep" });

                    case "restart":
                        _hardware.RestartComputer();
                        return JsonSerializer.Serialize(new { status = "ok", action = "restart" });

                    case "shutdown":
                        _hardware.ShutdownComputer();
                        return JsonSerializer.Serialize(new { status = "ok", action = "shutdown" });

                    case "get_apps":
                        var apps = _hardware.GetActiveWindows();
                        return JsonSerializer.Serialize(new { type = "apps_list", apps });

                    case "simulate_device":
                        _apServer.SetSimulatedDevice(
                            cmd.deviceName ?? "Pixel 8 Pro",
                            cmd.batteryPercentage ?? 85,
                            cmd.isCharging ?? false,
                            cmd.isConnected ?? true
                        );
                        BroadcastState();
                        return JsonSerializer.Serialize(new { status = "ok", message = "simulated_device_set" });

                    case "disconnect_device":
                        _apServer.SetSimulatedDevice("Disconnected", 0, false, false);
                        BroadcastState();
                        return JsonSerializer.Serialize(new { status = "ok", message = "disconnected" });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new { status = "error", message = ex.Message });
            }

            return JsonSerializer.Serialize(new { status = "ok" });
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
    }
}
