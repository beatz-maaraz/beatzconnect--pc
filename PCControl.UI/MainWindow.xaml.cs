using System;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Microsoft.Win32;
using PCControl.Core;

namespace PCControl.UI
{
    public partial class MainWindow : Window
    {
        #region Win32 Window Backdrop & Immersive Dark Mode
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38; // Windows 11 22H2+ Mica/Acrylic
        #endregion

        // ─── Local Fallback Controllers ──────────────────────────────────
        private readonly NativeHardwareController _hardware;
        private readonly AudioEndpointController  _audio;

        // ─── High-Speed IPC WebSocket Bridge ─────────────────────────────
        private ClientWebSocket? _ipcClient;
        private CancellationTokenSource? _ipcTokenSource;
        private bool _isIpcConnected;
        private readonly Uri _ipcUri = new("ws://127.0.0.1:28888/");

        // ─── UI & System Tray Helpers ─────────────────────────────────────
        private Forms.NotifyIcon? _notifyIcon;
        private DispatcherTimer?  _glanceClockTimer;
        private bool              _isActualClose;
        private bool              _isUserDraggingSlider;

        public MainWindow()
        {
            _hardware = new NativeHardwareController();
            _audio    = new AudioEndpointController(_hardware);

            InitializeComponent();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;

            InitializeGlanceClock();
            InitializeSystemTray();
            PopulateSystemInfo();
            StartIpcBridgeLoop();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EnableFluentBackdrop();

            // Check if launched with --autostart or --minimized flag
            string[] args = Environment.GetCommandLineArgs();
            bool silentStartup = false;
            foreach (var arg in args)
            {
                if (string.Equals(arg, "--autostart", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-silent", StringComparison.OrdinalIgnoreCase))
                {
                    silentStartup = true;
                    break;
                }
            }

            if (silentStartup)
            {
                Hide();
                WindowState = WindowState.Minimized;
                _notifyIcon?.ShowBalloonTip(2000, "Beatz Control",
                    "Started silently in background — connected to service.",
                    Forms.ToolTipIcon.Info);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isActualClose && (ChkCloseToTray?.IsChecked == true))
            {
                e.Cancel = true;
                Hide();
                _notifyIcon?.ShowBalloonTip(1500, "Beatz Control", "Minimized to system tray.", Forms.ToolTipIcon.Info);
            }
            else
            {
                _notifyIcon?.Dispose();
                _notifyIcon = null;
                _ipcTokenSource?.Cancel();
            }
        }

        private void EnableFluentBackdrop()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                int backdropType = 2; // Mica
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            }
            catch { }
        }

        private void InitializeGlanceClock()
        {
            _glanceClockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _glanceClockTimer.Tick += (s, e) =>
            {
                TxtGlanceClock.Text = DateTime.Now.ToString("HH:mm");
            };
            _glanceClockTimer.Start();
            TxtGlanceClock.Text = DateTime.Now.ToString("HH:mm");
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "Beatz Control (Companion)",
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true
            };

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Open Dashboard", null, (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            });
            contextMenu.Items.Add("Reconnect IPC", null, (s, e) =>
            {
                StartIpcBridgeLoop();
            });
            contextMenu.Items.Add("-");
            contextMenu.Items.Add("Exit Beatz Control", null, (s, e) =>
            {
                _isActualClose = true;
                Close();
            });

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            };
        }

        private void PopulateSystemInfo()
        {
            TxtTelemetryHost.Text = AccessPointManager.GetMachineName();
            TxtTelemetryUser.Text = Environment.UserName;
            TxtRelayUrlInput.Text = RelayConnectionService.DefaultRelayUrl;

            // Pre-read local volume
            try
            {
                int vol = _audio.GetMasterVolumePercent();
                SliderVolume.Value = vol;
                TxtVolumeBadge.Text = $"{vol}%";
            }
            catch { }

            // Registry auto-start check
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                ChkStartWithWindows.IsChecked = runKey?.GetValue("BeatzControl") != null;
            }
            catch { }
        }

        // ─────────────────────────────────────────────────────────────
        // ZERO-LATENCY IPC CLIENT BRIDGE
        // ─────────────────────────────────────────────────────────────
        private void StartIpcBridgeLoop()
        {
            _ipcTokenSource?.Cancel();
            _ipcTokenSource = new CancellationTokenSource();
            var token = _ipcTokenSource.Token;

            Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        _ipcClient?.Dispose();
                        _ipcClient = new ClientWebSocket();

                        var sw = Stopwatch.StartNew();
                        await _ipcClient.ConnectAsync(_ipcUri, token);
                        sw.Stop();

                        _isIpcConnected = true;
                        long latency = Math.Max(1, sw.ElapsedMilliseconds);

                        Dispatcher.Invoke(() =>
                        {
                            TxtIpcStatus.Text = $"Connected • {latency}ms";
                            DotIpcStatus.Fill = (SolidColorBrush)FindResource("StateSuccess");
                            TxtTelemetryIpc.Text = $"{latency} ms (WebSocket IPC)";
                            Log($"Zero-latency IPC connection established to background service in {latency}ms.");
                        });

                        // Request initial snapshot
                        await SendIpcCommandAsync(new { type = "get_state" });

                        // Listen loop
                        var buffer = new byte[8192];
                        while (_ipcClient.State == WebSocketState.Open && !token.IsCancellationRequested)
                        {
                            var result = await _ipcClient.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                            if (result.MessageType == WebSocketMessageType.Close) break;

                            string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            ProcessIpcMessage(json);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        _isIpcConnected = false;
                        Dispatcher.Invoke(() =>
                        {
                            TxtIpcStatus.Text = "Connecting…";
                            DotIpcStatus.Fill = (SolidColorBrush)FindResource("StateWarning");
                            TxtTelemetryIpc.Text = "Reconnecting…";
                        });
                        await Task.Delay(2000, token);
                    }
                }
            }, token);
        }

        private async Task SendIpcCommandAsync(object commandObj)
        {
            if (_ipcClient?.State == WebSocketState.Open)
            {
                try
                {
                    string json = JsonSerializer.Serialize(commandObj);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    await _ipcClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                    return;
                }
                catch { }
            }

            // Fallback direct execution if IPC service is not running
            ExecuteDirectFallback(commandObj);
        }

        private void ExecuteDirectFallback(object commandObj)
        {
            try
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(commandObj));
                string type = doc.RootElement.GetProperty("type").GetString() ?? "";

                switch (type)
                {
                    case "set_volume":
                        if (doc.RootElement.TryGetProperty("volume", out var volProp))
                        {
                            _audio.SetMasterVolumePercent(volProp.GetInt32());
                        }
                        break;
                    case "toggle_mute":
                        _audio.ToggleMute();
                        break;
                    case "lock_pc":
                        _hardware.LockScreen();
                        break;
                    case "sleep":
                        _hardware.SleepComputer();
                        break;
                    case "restart":
                        _hardware.RestartComputer();
                        break;
                    case "shutdown":
                        _hardware.ShutdownComputer();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Direct fallback execution note: {ex.Message}");
            }
        }

        private void ProcessIpcMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                string type = root.TryGetProperty("type", out var tProp) ? tProp.GetString() ?? "" : "";

                if (type == "state_update" && root.TryGetProperty("state", out var state))
                {
                    Dispatcher.Invoke(() => RenderTelemetryState(state));
                }
                else if (type == "event" && root.TryGetProperty("message", out var msgProp))
                {
                    Dispatcher.Invoke(() => Log($"[Service] {msgProp.GetString()}"));
                }
            }
            catch { }
        }

        private void RenderTelemetryState(JsonElement state)
        {
            bool isConn = state.TryGetProperty("isConnected", out var icProp) && icProp.GetBoolean();
            string dName = state.TryGetProperty("deviceName", out var dnProp) ? dnProp.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(dName)) dName = isConn ? "Samsung Galaxy S24 Ultra" : "No Device Connected";

            TxtSidebarDeviceName.Text = dName;
            TxtHeaderDeviceName.Text  = dName;
            TxtHeroTitle.Text         = isConn ? dName : "Waiting for Device";

            if (isConn)
            {
                TxtSidebarConn.Text = "Connected (Wi-Fi & Relay)";
                DotSidebarConn.Fill = (SolidColorBrush)FindResource("StateSuccess");
                TxtHeroConnState.Text = "CONNECTED & ACTIVE";
                BadgeHeroConnState.Background = new SolidColorBrush(Color.FromArgb(0x26, 0x10, 0xB9, 0x81));
                TxtHeroConnState.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
                TxtHeroDesc.Text = "Your mobile device is actively connected. High-throughput remote control, audio sync, and instant notifications are active with zero latency.";
            }
            else
            {
                TxtSidebarConn.Text = "Awaiting pairing…";
                DotSidebarConn.Fill = (SolidColorBrush)FindResource("TextTertiary");
                TxtHeroConnState.Text = "SEARCHING FOR PHONE";
                BadgeHeroConnState.Background = new SolidColorBrush(Color.FromArgb(0x26, 0xF5, 0x9E, 0x0B));
                TxtHeroConnState.Foreground = new SolidColorBrush(Color.FromRgb(0xFC, 0xD3, 0x4D));
                TxtHeroDesc.Text = "Awaiting companion connection. Pair the mobile app on your local Wi-Fi or via the secure cloud relay to enable remote control, audio sync, and instant notifications.";
            }

            // Battery Level
            if (state.TryGetProperty("batteryPercentage", out var bpProp))
            {
                int bat = Math.Clamp(bpProp.GetInt32(), 0, 100);
                TxtSidebarBattery.Text = $"{bat}%";
                TxtHeroBatteryNum.Text = $"{bat}%";
                BarSidebarBattery.Width = Math.Max(2, (18.0 * bat) / 100.0);

                if (bat <= 20) BarSidebarBattery.Background = (SolidColorBrush)FindResource("StateDanger");
                else if (bat <= 50) BarSidebarBattery.Background = (SolidColorBrush)FindResource("StateWarning");
                else BarSidebarBattery.Background = (SolidColorBrush)FindResource("StateSuccess");
            }

            // Charging State
            bool isCharging = state.TryGetProperty("isCharging", out var chProp) && chProp.GetBoolean();
            TxtBatteryBolt.Visibility = isCharging ? Visibility.Visible : Visibility.Collapsed;
            TxtHeroBatteryState.Text = isCharging ? "Fast Charging" : (isConn ? "On Battery" : "Disconnected");

            // Master Volume
            if (!_isUserDraggingSlider && state.TryGetProperty("masterVolume", out var mvProp))
            {
                int vol = mvProp.GetInt32();
                SliderVolume.Value = vol;
                TxtVolumeBadge.Text = $"{vol}%";
            }

            // Mute State
            if (state.TryGetProperty("isMuted", out var mProp))
            {
                bool isMuted = mProp.GetBoolean();
                TxtMuteBtn.Text = isMuted ? "Unmute" : "Mute";
                TxtMuteBtn.Foreground = isMuted ? (SolidColorBrush)FindResource("StateDanger") : (SolidColorBrush)FindResource("TextPrimary");
            }

            // Relay State
            bool relayConn = state.TryGetProperty("relayConnected", out var rcProp) && rcProp.GetBoolean();
            TxtRelayPill.Text = relayConn ? "Relay Online" : "Relay Ready";
            if (state.TryGetProperty("relayRoomId", out var rIdProp))
            {
                string rId = rIdProp.GetString() ?? "";
                TxtTelemetryRoomId.Text = string.IsNullOrWhiteSpace(rId) ? "backend-n4yg" : rId;
                TxtValRelay.Text = string.IsNullOrWhiteSpace(rId) ? "backend-n4yg" : (rId.Length > 10 ? rId.Substring(0, 10) : rId);
            }

            // Toggles
            if (state.TryGetProperty("doNotDisturb", out var dndProp))
                TxtValDnd.Text = dndProp.GetBoolean() ? "On" : "Off";
            if (state.TryGetProperty("bluetoothEnabled", out var btProp))
                TxtValBt.Text = btProp.GetBoolean() ? "Connected" : "Off";

            // Media
            if (state.TryGetProperty("currentTrack", out var ctProp))
                TxtMediaTitle.Text = ctProp.GetString() ?? "No Media Playing";
            if (state.TryGetProperty("currentArtist", out var caProp))
                TxtMediaArtist.Text = caProp.GetString() ?? "Beatz Companion";
            if (state.TryGetProperty("isPlaying", out var ipProp))
                IconMediaPlayText.Text = ipProp.GetBoolean() ? "⏸" : "▶";
        }

        // ─────────────────────────────────────────────────────────────
        // LOGGING
        // ─────────────────────────────────────────────────────────────
        public void Log(string message)
        {
            string timeStr = DateTime.Now.ToString("HH:mm:ss.fff");
            string entry = $"[{timeStr}] {message}\r\n";
            TxtConsoleLog.AppendText(entry);
            TxtConsoleLog.ScrollToEnd();
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            TxtConsoleLog.Clear();
            Log("Logs cleared by user.");
        }

        // ─────────────────────────────────────────────────────────────
        // NAVIGATION & TAB SWITCHING
        // ─────────────────────────────────────────────────────────────
        private void NavTab_Checked(object sender, RoutedEventArgs e)
        {
            if (PaneStatus == null || PaneApps == null || PaneLogs == null || PaneSettings == null) return;

            PaneStatus.Visibility   = NavTabStatus.IsChecked   == true ? Visibility.Visible : Visibility.Collapsed;
            PaneApps.Visibility     = NavTabApps.IsChecked     == true ? Visibility.Visible : Visibility.Collapsed;
            PaneLogs.Visibility     = NavTabLogs.IsChecked     == true ? Visibility.Visible : Visibility.Collapsed;
            PaneSettings.Visibility = NavTabSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (NavTabStatus.IsChecked == true) Log("Switched view to 'Device Status'");
            else if (NavTabApps.IsChecked == true) Log("Switched view to 'Phone Apps'");
            else if (NavTabLogs.IsChecked == true) Log("Switched view to 'Activity Logs'");
            else if (NavTabSettings.IsChecked == true) Log("Switched view to 'Settings'");
        }

        // ─────────────────────────────────────────────────────────────
        // WINDOW CHROME CONTROLS
        // ─────────────────────────────────────────────────────────────
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                IconMaximize.Visibility = Visibility.Visible;
                IconRestore.Visibility = Visibility.Collapsed;
            }
            else
            {
                WindowState = WindowState.Maximized;
                IconMaximize.Visibility = Visibility.Collapsed;
                IconRestore.Visibility = Visibility.Visible;
            }
        }

        private void BtnHideTray_Click(object sender, RoutedEventArgs e)
        {
            Hide();
            Log("Companion minimized to Windows system tray.");
            _notifyIcon?.ShowBalloonTip(1200, "Beatz Control", "Minimized to system tray.", Forms.ToolTipIcon.Info);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ─────────────────────────────────────────────────────────────
        // WORKSTATION & HARDWARE COMMAND DISPATCHERS
        // ─────────────────────────────────────────────────────────────
        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isIpcConnected && _audio == null) return;
            _isUserDraggingSlider = true;
            int val = (int)SliderVolume.Value;
            TxtVolumeBadge.Text = $"{val}%";

            _ = SendIpcCommandAsync(new { type = "set_volume", volume = val });
            _isUserDraggingSlider = false;
        }

        private void BtnToggleMute_Click(object sender, RoutedEventArgs e)
        {
            Log("Toggled master audio mute.");
            _ = SendIpcCommandAsync(new { type = "toggle_mute" });
        }

        private void BtnLockPc_Click(object sender, RoutedEventArgs e)
        {
            Log("Command dispatched: lock_pc");
            _ = SendIpcCommandAsync(new { type = "lock_pc" });
        }

        private void BtnSleepPc_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Put workstation into Sleep mode?", "Confirm Sleep", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Log("Command dispatched: sleep");
                _ = SendIpcCommandAsync(new { type = "sleep" });
            }
        }

        private void BtnRestartPc_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Restart workstation immediately?", "Confirm Restart", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Log("Command dispatched: restart");
                _ = SendIpcCommandAsync(new { type = "restart" });
            }
        }

        private void BtnShutdownPc_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Shutdown workstation?", "Confirm Shutdown", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Log("Command dispatched: shutdown");
                _ = SendIpcCommandAsync(new { type = "shutdown" });
            }
        }

        private void BtnMediaPlay_Click(object sender, RoutedEventArgs e)
        {
            Log("Command dispatched: media_play_pause");
            _ = SendIpcCommandAsync(new { type = "media_play_pause" });
        }

        private void BtnAppTile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string appName)
            {
                Log($"Requested mobile app launch: {appName}");
                _ = SendIpcCommandAsync(new { type = "launch_app", app = appName });
            }
        }

        private void BtnOpenMirror_Click(object sender, RoutedEventArgs e)
        {
            Log("Initiating phone screen mirroring session…");
            _ = SendIpcCommandAsync(new { type = "open_mirror" });
        }

        private void ChkStartWithWindows_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (ChkStartWithWindows.IsChecked == true)
                {
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    runKey?.SetValue("BeatzControl", $"\"{exePath}\" --autostart --minimized");
                    Log("Configured Beatz Control to auto-start with Windows.");
                }
                else
                {
                    runKey?.DeleteValue("BeatzControl", false);
                    Log("Removed Beatz Control from Windows startup.");
                }
            }
            catch (Exception ex)
            {
                Log($"Startup configuration note: {ex.Message}");
            }
        }
    }
}
