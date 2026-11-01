using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SocketIOClient;
using SocketIOClient.Transport;

namespace PCControl.Core
{
    /// <summary>
    /// Manages the persistent Socket.IO v4 connection to the Beatz relay server at
    /// https://backend-n4yg.onrender.com
    ///
    /// Protocol (derived from /health { registeredPcs } and Socket.IO EIO=4 handshake):
    ///
    ///   PC → server  : "register_pc"   { pcId, pcName, version }
    ///   server → PC  : "registered"    { pcId, roomId }        (confirmation)
    ///   server → PC  : "command"        { type, … }            (mobile → relay → PC)
    ///   PC → server  : "command_result" { pcId, requestId, result }
    ///   server → PC  : "mobile_joined"  { mobileId }
    ///   server → PC  : "mobile_left"    { mobileId }
    ///   server ↔ PC  : "ping" / "pong"  (keepalive)
    /// </summary>
    public class RelayConnectionService : IDisposable
    {
        // ─── Relay Configuration ─────────────────────────────────────────────
        public const string DefaultRelayUrl     = "https://backend-n4yg.onrender.com";
        private const int   ReconnectBaseMs     = 5_000;
        private const int   ReconnectMaxMs      = 60_000;

        // ─── Dependencies ────────────────────────────────────────────────────
        private readonly NativeHardwareController  _hardware;
        private readonly AudioEndpointController   _audio;
        private readonly WebSocketAccessPointServer _commandProcessor;

        // ─── Socket.IO Client ────────────────────────────────────────────────
        private SocketIOClient.SocketIO? _socket;
        private CancellationTokenSource _cts    = new();
        private int                    _retryMs = ReconnectBaseMs;
        private bool                   _disposed;

        // ─── Stable PC identity (derived from machine name, stable across runs) ─
        private readonly string _pcId;

        // ─── Public state ────────────────────────────────────────────────────
        public string RelayUrl    { get; private set; } = DefaultRelayUrl;
        public string RoomId      { get; private set; } = string.Empty;
        public bool   IsConnected { get; private set; }
        public long   PacketCount { get; private set; }

        // ─── Events ──────────────────────────────────────────────────────────
        /// <summary>Human-readable status / log line.</summary>
        public event Action<string>?         StatusChanged;
        /// <summary>Fires when the socket connects (true) or disconnects (false).</summary>
        public event Action<bool>?           ConnectionChanged;
        /// <summary>Fires on every inbound command packet.</summary>
        public event Action?                 PacketReceived;
        /// <summary>Fires after a command is executed locally.</summary>
        public event Action<string, string>? CommandExecuted;

        // ─────────────────────────────────────────────────────────────────────
        public RelayConnectionService(
            NativeHardwareController hardware,
            AudioEndpointController  audio,
            string?                  relayUrl = null)
        {
            _hardware = hardware;
            _audio    = audio;
            RelayUrl  = relayUrl ?? DefaultRelayUrl;

            // Stable ID: lowercase machine name, no spaces
            _pcId = Environment.MachineName.ToLowerInvariant().Replace(" ", "_");

            // Reuse existing command-dispatch logic; port 0 = no local listener
            _commandProcessor = new WebSocketAccessPointServer(0, _hardware, _audio);
            _commandProcessor.CommandExecuted += (ep, cmd) => CommandExecuted?.Invoke(ep, cmd);
        }

        // ─── Public API ──────────────────────────────────────────────────────

        /// <summary>Start background connection loop.</summary>
        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ConnectLoopAsync(_cts.Token));
        }

        /// <summary>Gracefully stop the connection.</summary>
        public void Stop()
        {
            _cts.Cancel();
            try { _socket?.DisconnectAsync().GetAwaiter().GetResult(); } catch { }
            IsConnected = false;
        }

        // ─── Connection Loop ─────────────────────────────────────────────────

        private async Task ConnectLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await ConnectOnceAsync(ct);
                    _retryMs = ReconnectBaseMs; // reset on clean disconnect
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Notify($"Connection error: {ex.Message}");
                }

                if (ct.IsCancellationRequested) break;

                Notify($"Reconnecting in {_retryMs / 1000}s…");
                try { await Task.Delay(_retryMs, ct); } catch (OperationCanceledException) { break; }
                _retryMs = Math.Min(_retryMs * 2, ReconnectMaxMs);
            }
        }

        private async Task ConnectOnceAsync(CancellationToken ct)
        {
            Notify($"Connecting → {RelayUrl}");

            _socket?.Dispose();
            _socket = new SocketIOClient.SocketIO(RelayUrl, new SocketIOOptions
            {
                // EIO=4 is default for SocketIOClient 3.x
                Transport            = TransportProtocol.WebSocket,
                ReconnectionAttempts = 0,                          // we manage retries
                ConnectionTimeout    = TimeSpan.FromSeconds(25),
                ExtraHeaders         = new Dictionary<string, string>
                {
                    { "X-Client-Type", "pc" },
                    { "X-Client-Version", "2.0" }
                }
            });

            // ── Wire events ──────────────────────────────────────────────
            _socket.OnConnected   += OnConnected;
            _socket.OnDisconnected += OnDisconnected;
            _socket.OnError       += (_, err) => Notify($"Socket error: {err}");

            // Server → PC events
            _socket.On("registered",     OnRegistered);
            _socket.On("pc-registered",  OnRegistered);
            _socket.On("command",        OnCommand);
            _socket.On("trackpad-move",  OnTrackpadMove);
            _socket.On("mouse-click",    OnMouseClick);
            _socket.On("mouse-scroll",   OnMouseScroll);
            _socket.On("volume-change",  OnVolumeChange);
            _socket.On("control-event",  OnControlEvent);
            _socket.On("mobile_joined",  resp => Notify($"Mobile joined: {SafeGetString(resp, "mobileId")}"));
            _socket.On("mobile_left",    resp => Notify($"Mobile disconnected: {SafeGetString(resp, "mobileId")}"));
            _socket.On("ping",           OnPing);

            // ── Connect ──────────────────────────────────────────────────
            var connectTcs  = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var cancelledTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reg = ct.Register(() => cancelledTcs.TrySetCanceled());

            // OnConnected fires on the socket thread, so also resolve via event
            _socket.OnConnected += (_, __) => connectTcs.TrySetResult(true);

            var connectTask = _socket.ConnectAsync();
            var winner = await Task.WhenAny(connectTask, connectTcs.Task, cancelledTcs.Task);

            if (winner == cancelledTcs.Task) return;
            await connectTask; // surface any exception

            // ── Hold until disconnect or cancellation ─────────────────────
            var disconnTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _socket.OnDisconnected += (_, __) => disconnTcs.TrySetResult(true);

            await Task.WhenAny(disconnTcs.Task, cancelledTcs.Task);
        }

        // ─── Socket Event Handlers ───────────────────────────────────────────

        private async void OnConnected(object? sender, EventArgs e)
        {
            IsConnected = true;
            _retryMs    = ReconnectBaseMs;
            Notify("Socket connected — registering PC…");
            ConnectionChanged?.Invoke(true);

            try
            {
                // Emit both 'pc-register' (Render standard) and 'register_pc' (legacy)
                await _socket!.EmitAsync("pc-register", new
                {
                    pcId    = _pcId,
                    pcName  = Environment.MachineName,
                    version = "2.0"
                });
                await _socket!.EmitAsync("register_pc", new
                {
                    pcId    = _pcId,
                    pcName  = Environment.MachineName,
                    version = "2.0"
                });
            }
            catch (Exception ex)
            {
                Notify($"register error: {ex.Message}");
            }
        }

        private void OnDisconnected(object? sender, string reason)
        {
            IsConnected = false;
            ConnectionChanged?.Invoke(false);
            Notify($"Disconnected: {reason}");
        }

        private void OnRegistered(SocketIOResponse resp)
        {
            try
            {
                var data  = resp.GetValue<JsonElement>(0);
                RoomId    = data.TryGetProperty("roomId",  out var r) ? r.GetString() ?? _pcId : _pcId;
                string id = data.TryGetProperty("pcId",    out var p) ? p.GetString() ?? _pcId : _pcId;
                Notify($"Registered ✓  Room: {RoomId}  |  PC ID: {id}");
            }
            catch
            {
                RoomId = _pcId;
                Notify($"Registered (fallback room: {RoomId})");
            }
        }

        private void OnTrackpadMove(SocketIOResponse resp)
        {
            try
            {
                var payload = resp.GetValue<JsonElement>(0);
                int dx = payload.TryGetProperty("dx", out var x) ? (int)Math.Round(x.GetDouble()) : 0;
                int dy = payload.TryGetProperty("dy", out var y) ? (int)Math.Round(y.GetDouble()) : 0;
                PacketCount++;
                PacketReceived?.Invoke();
                _hardware.MoveMouseRelative(dx, dy);
                CommandExecuted?.Invoke("relay", $"Move ({dx}, {dy})");
            }
            catch (Exception ex)
            {
                Notify($"TrackpadMove error: {ex.Message}");
            }
        }

        private void OnMouseClick(SocketIOResponse resp)
        {
            try
            {
                var payload = resp.GetValue<JsonElement>(0);
                string btn = payload.TryGetProperty("button", out var b) ? b.GetString() ?? "left" : "left";
                string st = payload.TryGetProperty("state", out var s) ? s.GetString() ?? "tap" : "tap";
                PacketCount++;
                PacketReceived?.Invoke();

                MouseButton button = btn.ToLowerInvariant() switch
                {
                    "right" => MouseButton.Right,
                    "middle" => MouseButton.Middle,
                    _ => MouseButton.Left
                };

                if (st.Equals("double", StringComparison.OrdinalIgnoreCase))
                    _hardware.MouseDoubleClick(button);
                else if (st.Equals("down", StringComparison.OrdinalIgnoreCase))
                    _hardware.MouseButtonDown(button);
                else if (st.Equals("up", StringComparison.OrdinalIgnoreCase))
                    _hardware.MouseButtonUp(button);
                else
                    _hardware.MouseClick(button);

                CommandExecuted?.Invoke("relay", $"Click {button} ({st})");
            }
            catch (Exception ex)
            {
                Notify($"MouseClick error: {ex.Message}");
            }
        }

        private void OnMouseScroll(SocketIOResponse resp)
        {
            try
            {
                var payload = resp.GetValue<JsonElement>(0);
                double delta = 0;
                if (payload.TryGetProperty("deltaY", out var dy)) delta = dy.GetDouble();
                else if (payload.TryGetProperty("delta", out var d)) delta = d.GetDouble();

                PacketCount++;
                PacketReceived?.Invoke();

                int scrollDelta = (int)Math.Round(-delta * 1.5);
                _hardware.MouseScrollVertical(scrollDelta);
                CommandExecuted?.Invoke("relay", $"Scroll {scrollDelta}");
            }
            catch (Exception ex)
            {
                Notify($"MouseScroll error: {ex.Message}");
            }
        }

        private void OnVolumeChange(SocketIOResponse resp)
        {
            try
            {
                var payload = resp.GetValue<JsonElement>(0);
                if (payload.TryGetProperty("mute", out var m) && m.ValueKind == JsonValueKind.True)
                {
                    _audio.SetMute(true);
                    CommandExecuted?.Invoke("relay", "Volume Muted");
                }
                else
                {
                    int level = 50;
                    if (payload.TryGetProperty("level", out var l)) level = l.GetInt32();
                    else if (payload.TryGetProperty("percent", out var p)) level = p.GetInt32();

                    _audio.SetMasterVolumePercent(level);
                    if (payload.TryGetProperty("mute", out var m2) && m2.ValueKind == JsonValueKind.False)
                        _audio.SetMute(false);

                    CommandExecuted?.Invoke("relay", $"Volume Set {level}%");
                }
                PacketCount++;
                PacketReceived?.Invoke();
            }
            catch (Exception ex)
            {
                Notify($"VolumeChange error: {ex.Message}");
            }
        }

        private void OnControlEvent(SocketIOResponse resp)
        {
            try
            {
                var payload = resp.GetValue<JsonElement>(0);
                string rawJson = payload.GetRawText();
                PacketCount++;
                PacketReceived?.Invoke();
                string result = _commandProcessor.ExecuteCommandPayload(rawJson, "relay");
            }
            catch (Exception ex)
            {
                Notify($"ControlEvent error: {ex.Message}");
            }
        }

        private void OnCommand(SocketIOResponse resp)
        {
            string? requestId = null;
            try
            {
                var    payload = resp.GetValue<JsonElement>(0);
                requestId = payload.TryGetProperty("requestId", out var rid) ? rid.GetString() : null;
                string rawJson = payload.GetRawText();

                PacketCount++;
                PacketReceived?.Invoke();

                string result = _commandProcessor.ExecuteCommandPayload(rawJson, "relay");

                _ = _socket?.EmitAsync("command_result", new
                {
                    pcId      = _pcId,
                    requestId = requestId,
                    result    = JsonSerializer.Deserialize<object>(result)
                });
            }
            catch (Exception ex)
            {
                _ = _socket?.EmitAsync("command_result", new
                {
                    pcId      = _pcId,
                    requestId = requestId,
                    result    = new { status = "error", message = ex.Message }
                });
            }
        }

        private async void OnPing(SocketIOResponse resp)
        {
            if (_socket?.Connected == true)
            {
                await _socket.EmitAsync("pong", new
                {
                    pcId = _pcId,
                    time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private static string SafeGetString(SocketIOResponse resp, string key)
        {
            try
            {
                var el = resp.GetValue<JsonElement>(0);
                return el.TryGetProperty(key, out var v) ? v.GetString() ?? "?" : "?";
            }
            catch { return "?"; }
        }

        private void Notify(string msg) => StatusChanged?.Invoke(msg);

        // ─── IDisposable ─────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _socket?.Dispose();
        }
    }
}
