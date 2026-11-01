using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using PCControl.Core;
using Microsoft.Win32;

namespace PCControl.UI
{
    public partial class MainWindow : Window
    {
        #region Win32 Window Backdrop & Blur Interop
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38; // Windows 11 22H2+ Mica/Acrylic

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        private enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }

        private enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }
        #endregion

        // ─── Core services ────────────────────────────────────────────────
        private readonly NativeHardwareController _hardware;
        private readonly AudioEndpointController  _audio;
        private RelayConnectionService?           _relay;
        private WebSocketAccessPointServer?       _localServer;

        // ─── UI helpers ───────────────────────────────────────────────────
        private Forms.NotifyIcon? _notifyIcon;
        private DispatcherTimer?  _telemetryTimer;
        private bool              _isActualClose;
        private bool              _isHotspotRunning;
        private System.Windows.Point _lastTrackpadPt;
        private bool              _isMinimizing;

        // ─── Configuration ────────────────────────────────────────────────
        private const string DefaultRelayUrl = RelayConnectionService.DefaultRelayUrl;

        // ─────────────────────────────────────────────────────────────────
        public MainWindow()
        {
            _hardware = new NativeHardwareController();
            _audio    = new AudioEndpointController(_hardware);

            InitializeComponent();

            Loaded += MainWindow_Loaded;
            InitializeSystemTray();
            PopulatePcInfo();
            InitializeLocalServer();
            InitializeRelayService(DefaultRelayUrl);
            StartTelemetryTimer();
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
                // Start silently in the background / system tray
                Hide();
                WindowState = WindowState.Minimized;
                _notifyIcon?.ShowBalloonTip(2000, "Beatz Control",
                    "Started silently in background — connected to relay.",
                    Forms.ToolTipIcon.Info);
            }
            else
            {
                PlayWindowEntranceAnimation();
            }

            // Wire startup mode UI indicators
            if (silentStartup)
            {
                BannerAutostart.Visibility   = Visibility.Visible;
                BadgeStartupMode.Visibility  = Visibility.Visible;
                TxtStartupModeBar.Text       = "Auto-started (silent)";
                TxtStartupModeBar.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            }
        }

        private void EnableFluentBackdrop()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                // 1. Enable Immersive Dark Mode for Win32 frame/system elements
                int darkMode = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

                // 2. Try Windows 11 DWM Acrylic backdrop (DWMWA_SYSTEMBACKDROP_TYPE = 3 for Acrylic, 2 for Mica)
                int backdropType = 3; // Acrylic
                int res = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));

                // 3. Fallback to AccentPolicy Acrylic blur behind for Win10 / older Win11 builds
                if (res != 0)
                {
                    var policy = new AccentPolicy
                    {
                        AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                        AccentFlags = 2,
                        GradientColor = unchecked((int)0xC8080E1A) // Semi-transparent dark slate tint
                    };

                    int size = Marshal.SizeOf(policy);
                    IntPtr pPolicy = Marshal.AllocHGlobal(size);
                    try
                    {
                        Marshal.StructureToPtr(policy, pPolicy, false);
                        var data = new WindowCompositionAttributeData
                        {
                            Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                            SizeOfData = size,
                            Data = pPolicy
                        };
                        SetWindowCompositionAttribute(hwnd, ref data);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pPolicy);
                    }
                }
            }
            catch
            {
                // Graceful fallback to rich WPF dark acrylic gradients
            }
        }

        private void PlayWindowEntranceAnimation()
        {
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, fadeIn);
        }

        // ─── Initialisation ───────────────────────────────────────────────

        private void PopulatePcInfo()
        {
            TxtPcName.Text = AccessPointManager.GetMachineName();

            var (resW, resH) = _hardware.GetScreenResolution();
            TxtResolution.Text = $"{resW} × {resH}";

            int vol = _audio.GetMasterVolumePercent();
            SliderVolume.Value      = vol;
            TxtVolumePercent.Text   = $"{vol}%";

            // Pre-populate the relay URL text box in Settings tab.
            TxtRelayUrlInput.Text = RelayConnectionService.DefaultRelayUrl;

            // Detect current startup registry state and pre-populate the checkbox
            using var runKey = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
            bool startupEnabled = runKey?.GetValue("BeatzControl") != null;
            ChkStartWithWindows.IsChecked = startupEnabled;
        }

        private void InitializeLocalServer()
        {
            try
            {
                _localServer = new WebSocketAccessPointServer(8080, _hardware, _audio);
                _localServer.ClientConnected += endpoint => Log($"LAN client connected: {endpoint}");
                _localServer.ClientDisconnected += endpoint => Log($"LAN client disconnected: {endpoint}");
                _localServer.CommandExecuted += (endpoint, cmd) => Log($"[LAN {endpoint}] {cmd}");
                _localServer.Start();
                Log("Local LAN/Hotspot server listening on port 8080.");
            }
            catch (Exception ex)
            {
                Log($"Local server initialization note: {ex.Message}");
            }
        }

        private void InitializeRelayService(string relayUrl)
        {
            // Tear down any previous service before creating a new one.
            if (_relay != null)
            {
                _relay.StatusChanged      -= OnRelayStatusChanged;
                _relay.ConnectionChanged  -= OnRelayConnectionChanged;
                _relay.PacketReceived     -= OnPacketReceived;
                _relay.CommandExecuted    -= OnCommandExecuted;
                _relay.Stop();
                _relay.Dispose();
            }

            _relay = new RelayConnectionService(_hardware, _audio, relayUrl);
            _relay.StatusChanged      += OnRelayStatusChanged;
            _relay.ConnectionChanged  += OnRelayConnectionChanged;
            _relay.PacketReceived     += OnPacketReceived;
            _relay.CommandExecuted    += OnCommandExecuted;

            SetConnectionUI(false, "Connecting…");
            _relay.Start();

            Log($"Relay service started → {relayUrl}");
        }

        private void InitializeSystemTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text    = "Beatz Control",
                Icon    = System.Drawing.SystemIcons.Application,
                Visible = true
            };

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Open Beatz Control",    null, (_, __) => ShowAndActivate());
            menu.Items.Add("Lock Workstation",       null, (_, __) => _hardware.LockScreen());
            menu.Items.Add("Toggle Hotspot",         null, (_, __) => BtnToggleHotspot_Click(null!, null!));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit",                   null, (_, __) => ExitApp());

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick     += (_, __) => ShowAndActivate();
        }

        private void StartTelemetryTimer()
        {
            _telemetryTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _telemetryTimer.Tick += (_, __) =>
            {
                var (cx, cy) = _hardware.GetMousePosition();
                TxtCursorCoords.Text  = $"{cx}, {cy}";
                BadgeMuted.Visibility = _audio.GetMute() ? Visibility.Visible : Visibility.Collapsed;
            };
            _telemetryTimer.Start();
        }

        // ─── Relay Event Handlers ─────────────────────────────────────────

        private void OnRelayStatusChanged(string message)
        {
            Dispatcher.InvokeAsync(() =>
            {
                TxtRelayDetail.Text  = message;
                TxtStatusBar.Text    = message;
                Log($"[Relay] {message}");
            });
        }

        private void OnRelayConnectionChanged(bool connected)
        {
            Dispatcher.InvokeAsync(() =>
            {
                SetConnectionUI(connected, connected ? "Connected to relay" : "Disconnected");

                if (_relay is not null)
                {
                    TxtRoomId.Text     = _relay.RoomId;
                    TxtRoomIdFull.Text = _relay.RoomId;
                }

                if (connected)
                {
                    _notifyIcon?.ShowBalloonTip(3000, "Beatz Control",
                        $"Connected to relay.  Room: {_relay?.RoomId}", Forms.ToolTipIcon.Info);
                }
            });
        }

        private void OnPacketReceived()
        {
            Dispatcher.InvokeAsync(() =>
            {
                TxtPacketCount.Text = (_relay?.PacketCount ?? 0).ToString("N0");

                // Brief pulse: green flash → back to cyan
                DotPulse.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
                t.Tick += (_, __) =>
                {
                    DotPulse.Fill = new SolidColorBrush(Color.FromRgb(34, 211, 238));
                    t.Stop();
                };
                t.Start();
            });
        }

        private void OnCommandExecuted(string endpoint, string description)
        {
            Dispatcher.InvokeAsync(() => Log($"[{endpoint}] {description}"));
        }

        // ─── UI helpers ───────────────────────────────────────────────────

        private void SetConnectionUI(bool connected, string statusText)
        {
            if (connected)
            {
                BadgeRelay.Background   = new SolidColorBrush(Color.FromRgb(10, 30, 20));
                BadgeRelay.BorderBrush  = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                DotStatus.Fill          = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                DotStatusBar.Fill       = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                ((DropShadowEffect)DotStatus.Effect).Color = Color.FromRgb(16, 185, 129);
            }
            else
            {
                BadgeRelay.Background   = new SolidColorBrush(Color.FromRgb(26, 16, 0));
                BadgeRelay.BorderBrush  = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                DotStatus.Fill          = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                DotStatusBar.Fill       = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                ((DropShadowEffect)DotStatus.Effect).Color = Color.FromRgb(245, 158, 11);
            }

            TxtRelayStatus.Text  = statusText;
            TxtStatusBar.Text    = statusText;
        }

        private void ShowAndActivate()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();

            // Smooth restore animation
            var restoreAnim = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, restoreAnim);
        }

        private void ExitApp()
        {
            _isActualClose = true;
            _telemetryTimer?.Stop();
            _localServer?.Stop();
            _relay?.Stop();
            _relay?.Dispose();

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            Application.Current.Shutdown();
        }

        private void Log(string message)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (TxtLogs == null) return;
                string ts = DateTime.Now.ToString("HH:mm:ss.fff");
                TxtLogs.AppendText($"[{ts}]  {message}\n");
                TxtLogs.ScrollToEnd();
            });
        }

        // ─── Title-bar & Window Chrome ────────────────────────────────────

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left && e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            if (_isMinimizing) return;
            _isMinimizing = true;

            // Fluent smooth minimize transition
            var anim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            anim.Completed += (s, args) =>
            {
                WindowState = WindowState.Minimized;
                Opacity = 1.0;
                _isMinimizing = false;
            };

            BeginAnimation(OpacityProperty, anim);
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                MaximizeIcon.Data = Geometry.Parse("M4,4 H16 V16 H4 Z");
            }
            else
            {
                WindowState = WindowState.Maximized;
                // Switch to restore icon (two overlapping squares)
                MaximizeIcon.Data = Geometry.Parse("M6,2 H16 V12 M2,6 H12 V16 H2 Z");
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (ChkMinimizeToTray.IsChecked == true)
            {
                Hide();
                _notifyIcon?.ShowBalloonTip(2000, "Beatz Control",
                    "Running in background — double-click tray icon to restore.",
                    Forms.ToolTipIcon.Info);
            }
            else
            {
                ExitApp();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isActualClose && ChkMinimizeToTray.IsChecked == true)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnClosing(e);
        }

        // ─── Tab navigation ───────────────────────────────────────────────

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (ViewDashboard == null) return;

            ViewDashboard.Visibility = TabDashboard.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            ViewApps.Visibility      = TabApps.IsChecked      == true ? Visibility.Visible : Visibility.Collapsed;
            ViewSettings.Visibility  = TabSettings.IsChecked  == true ? Visibility.Visible : Visibility.Collapsed;
            ViewLogs.Visibility      = TabLogs.IsChecked      == true ? Visibility.Visible : Visibility.Collapsed;

            if (TabApps.IsChecked == true)
                RefreshActiveApps();
        }

        // ─── Dashboard Controls ───────────────────────────────────────────

        private void BtnReconnect_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtRelayUrlInput?.Text?.Trim() is { Length: > 0 } u ? u : DefaultRelayUrl;
            Log($"Manual reconnect requested → {url}");
            InitializeRelayService(url);
        }

        private void BtnCopyRoomId_Click(object sender, RoutedEventArgs e)
        {
            string room = _relay?.RoomId ?? string.Empty;
            if (string.IsNullOrEmpty(room)) return;

            Clipboard.SetText(room);
            Log($"Room ID copied: {room}");

            if (sender is Button btn)
            {
                btn.Content = "✓ Copied!";
                var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (_, __) => { btn.Content = "Copy Room ID"; t.Stop(); };
                t.Start();
            }
        }

        private void BtnToggleHotspot_Click(object sender, RoutedEventArgs e)
        {
            if (!_isHotspotRunning)
            {
                bool ok = AccessPointManager.StartHostedNetwork();
                _isHotspotRunning = true;
                BtnToggleHotspot.Content = "Stop Hotspot";
                Log(ok ? "LAN hotspot started." : "Hotspot toggle attempted (no Wi-Fi adapter?).");
            }
            else
            {
                AccessPointManager.StopHostedNetwork();
                _isHotspotRunning = false;
                BtnToggleHotspot.Content = "Start Hotspot";
                Log("LAN hotspot stopped.");
            }
        }

        // ─── Volume & Media ───────────────────────────────────────────────

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtVolumePercent == null || _audio == null) return;
            int vol = (int)e.NewValue;
            TxtVolumePercent.Text = $"{vol}%";
            _audio.SetMasterVolumePercent(vol);
        }

        private void BtnMute_Click(object sender, RoutedEventArgs e)
        {
            _audio.ToggleMute();
            bool m = _audio.GetMute();
            BtnMute.Content       = m ? "🔇  Unmute" : "🔊  Mute";
            BadgeMuted.Visibility = m ? Visibility.Visible : Visibility.Collapsed;
            Log($"Mute toggled → {m}");
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)      => _audio.PreviousTrack();
        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)  => _audio.PlayPause();
        private void BtnNext_Click(object sender, RoutedEventArgs e)       => _audio.NextTrack();

        // ─── System Actions ───────────────────────────────────────────────

        private void BtnLock_Click(object sender, RoutedEventArgs e)
        {
            _hardware.LockScreen();
            Log("Workstation locked.");
        }

        private void BtnShowDesktop_Click(object sender, RoutedEventArgs e)
        {
            _hardware.SendHotkey(0x5B, 0x44); // Win + D
            Log("Show Desktop triggered.");
        }

        private void BtnSleep_Click(object sender, RoutedEventArgs e)
        {
            Log("Entering sleep state…");
            _hardware.SleepComputer();
        }

        private void BtnTaskManager_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); }
            catch { }
        }

        // ─── Test Trackpad ────────────────────────────────────────────────

        private void TestTrackpad_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _lastTrackpadPt = e.GetPosition(TestTrackpadArea);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _hardware.MouseClick(PCControl.Core.MouseButton.Left);
                Log("[Trackpad] Left click");
            }
        }

        private void TestTrackpad_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var cur  = e.GetPosition(TestTrackpadArea);
            int dx   = (int)((cur.X - _lastTrackpadPt.X) * 2.0);
            int dy   = (int)((cur.Y - _lastTrackpadPt.Y) * 2.0);

            if (Math.Abs(dx) > 0 || Math.Abs(dy) > 0)
            {
                _hardware.MoveMouseRelative(dx, dy);
                _lastTrackpadPt = cur;
            }
        }

        // ─── Active Apps Tab ──────────────────────────────────────────────

        private void BtnRefreshApps_Click(object sender, RoutedEventArgs e)  => RefreshActiveApps();

        private void RefreshActiveApps()
        {
            try
            {
                var apps = _hardware.GetActiveWindows();
                DataGridApps.ItemsSource = apps;
                Log($"Found {apps.Count} active windows.");
            }
            catch (Exception ex)
            {
                Log($"App scan error: {ex.Message}");
            }
        }

        private void BtnCloseSelectedApp_Click(object sender, RoutedEventArgs e)
        {
            if (DataGridApps.SelectedItem is WindowInfo wi)
            {
                _hardware.CloseWindow(wi.Handle);
                Log($"Closed '{wi.Title}' (PID {wi.ProcessId})");
                RefreshActiveApps();
            }
        }

        // ─── Settings Tab ─────────────────────────────────────────────────

        private void BtnApplyRelayUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtRelayUrlInput.Text?.Trim() ?? DefaultRelayUrl;
            if (string.IsNullOrWhiteSpace(url)) url = DefaultRelayUrl;
            Log($"Applying new relay URL: {url}");
            TxtRelayUrl.Text = url;
            InitializeRelayService(url);
        }

        private void BtnInstallService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCControl.Service.exe");
                if (!File.Exists(exe)) { Log($"Service exe not found: {exe}"); return; }

                Process.Start(new ProcessStartInfo
                {
                    FileName         = exe,
                    Arguments        = "--install",
                    Verb             = "runas",
                    UseShellExecute  = true
                })?.WaitForExit();

                TxtServiceStatus.Text       = "Status: Background Service Installed ✓";
                TxtServiceStatus.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                Log("PCControl.Service installed.");
            }
            catch (Exception ex) { Log($"Install error: {ex.Message}"); }
        }

        private void BtnUninstallService_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCControl.Service.exe");
                if (!File.Exists(exe)) { Log($"Service exe not found: {exe}"); return; }

                Process.Start(new ProcessStartInfo
                {
                    FileName        = exe,
                    Arguments       = "--uninstall",
                    Verb            = "runas",
                    UseShellExecute = true
                })?.WaitForExit();

                TxtServiceStatus.Text       = "Status: Service Uninstalled";
                TxtServiceStatus.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                Log("PCControl.Service uninstalled.");
            }
            catch (Exception ex) { Log($"Uninstall error: {ex.Message}"); }
        }

        private void BtnAddFirewall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("netsh",
                    "advfirewall firewall add rule name=\"Beatz Control\" " +
                    "dir=in action=allow protocol=TCP localport=8080")
                {
                    CreateNoWindow  = true,
                    UseShellExecute = true,
                    Verb            = "runas"
                })?.WaitForExit();

                Log("Firewall rule added for TCP 8080.");
            }
            catch (Exception ex) { Log($"Firewall error: {ex.Message}"); }
        }

        private void ChkStartWithWindows_Changed(object sender, RoutedEventArgs e)
        {
            const string regKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            const string appName = "BeatzControl";

            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(regKey, writable: true);
                if (key == null) return;

                if (ChkStartWithWindows.IsChecked == true)
                {
                    string exePath = $"\"{Process.GetCurrentProcess().MainModule?.FileName}\" --autostart --minimized";
                    key.SetValue(appName, exePath);
                    Log("Added to Windows startup (silent background mode).");
                }
                else
                {
                    key.DeleteValue(appName, throwOnMissingValue: false);
                    Log("Removed from Windows startup.");
                }

                UpdateStartupIndicator(ChkStartWithWindows.IsChecked == true);
            }
            catch (Exception ex) { Log($"Startup registry error: {ex.Message}"); }
        }

        // ─── Log Tab ──────────────────────────────────────────────────────

        private void UpdateStartupIndicator(bool enabled)
        {
            if (DotStartupMode == null || TxtStartupMode == null) return;

            if (enabled)
            {
                DotStartupMode.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                TxtStartupMode.Text = "Windows startup: Enabled — launches silently with --autostart --minimized";
            }
            else
            {
                DotStartupMode.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                TxtStartupMode.Text = "Windows startup: Not configured";
            }
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e) => TxtLogs.Clear();
    }
}
