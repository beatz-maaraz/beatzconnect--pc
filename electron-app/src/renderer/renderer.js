/**
 * Beatz Control — Live Fluent UI Renderer Controller
 * Wires real Socket.IO Cloud Relay connection, native Windows hardware integration,
 * theme switching, and smooth window animations.
 */

(function () {
  // ─── 1. Window Controls & Smooth Animations ────────────────────────────────
  const btnMinimize = document.getElementById('btnMinimize');
  const btnMaximize = document.getElementById('btnMaximize');
  const btnClose = document.getElementById('btnClose');
  const maxIcon = document.getElementById('maxIcon');

  if (btnMinimize) {
    btnMinimize.addEventListener('click', () => {
      document.body.classList.remove('window-animating-restore', 'window-enter');
      document.body.classList.add('window-animating-minimize');
      setTimeout(() => {
        window.beatzAPI?.minimize();
        setTimeout(() => {
          document.body.classList.remove('window-animating-minimize');
        }, 100);
      }, 150);
    });
  }

  if (btnMaximize) {
    btnMaximize.addEventListener('click', () => {
      window.beatzAPI?.maximize();
    });
  }

  if (btnClose) {
    btnClose.addEventListener('click', () => {
      document.body.classList.remove('window-animating-restore', 'window-enter');
      document.body.classList.add('window-animating-close');
      setTimeout(() => {
        window.beatzAPI?.close();
        setTimeout(() => {
          document.body.classList.remove('window-animating-close');
        }, 150);
      }, 150);
    });
  }

  // Restore animation when window regains focus
  window.addEventListener('focus', () => {
    document.body.classList.remove('window-animating-minimize');
    document.body.classList.add('window-animating-restore');
    setTimeout(() => {
      document.body.classList.remove('window-animating-restore');
    }, 250);
  });

  if (window.beatzAPI?.onMaxChanged) {
    window.beatzAPI.onMaxChanged((isMax) => {
      if (!maxIcon) return;
      if (isMax) {
        maxIcon.innerHTML = `
          <rect x="0.5" y="2.5" width="7" height="7" rx="1" stroke="currentColor" stroke-width="1" fill="none"/>
          <path d="M2.5 2.5 V0.5 H9.5 V7.5 H7.5" stroke="currentColor" stroke-width="1" fill="none"/>
        `;
      } else {
        maxIcon.innerHTML = `
          <rect x="0.5" y="0.5" width="9" height="9" rx="1" stroke="currentColor" stroke-width="1" fill="none"/>
        `;
      }
    });
  }

  // ─── 2. Dark / Light Mode Theme System ─────────────────────────────────────
  const themeButtons = {
    dark: document.getElementById('btnThemeDark'),
    light: document.getElementById('btnThemeLight'),
    system: document.getElementById('btnThemeSystem'),
  };

  const prefersDarkMediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

  function applyTheme(themeChoice) {
    let effectiveTheme = themeChoice;
    if (themeChoice === 'system') {
      effectiveTheme = prefersDarkMediaQuery.matches ? 'dark' : 'light';
    }

    document.documentElement.setAttribute('data-theme', effectiveTheme);
    localStorage.setItem('beatz-theme', themeChoice);

    Object.entries(themeButtons).forEach(([key, btn]) => {
      if (btn) btn.classList.toggle('active', key === themeChoice);
    });

    if (window.beatzAPI?.setTheme) {
      window.beatzAPI.setTheme(themeChoice);
    }
  }

  Object.entries(themeButtons).forEach(([key, btn]) => {
    if (btn) btn.addEventListener('click', () => applyTheme(key));
  });

  prefersDarkMediaQuery.addEventListener('change', () => {
    const saved = localStorage.getItem('beatz-theme') || 'system';
    if (saved === 'system') applyTheme('system');
  });

  const initialTheme = localStorage.getItem('beatz-theme') || 'dark';
  applyTheme(initialTheme);

  // ─── 3. Navigation Switching ───────────────────────────────────────────────
  const navItems = document.querySelectorAll('.nav-item');
  const views = {
    dashboard: document.getElementById('view-dashboard'),
    apps: document.getElementById('view-apps'),
    settings: document.getElementById('view-settings'),
    logs: document.getElementById('view-logs')
  };

  navItems.forEach(item => {
    item.addEventListener('click', () => {
      const targetView = item.getAttribute('data-view');
      navItems.forEach(n => n.classList.remove('nav-item--active'));
      item.classList.add('nav-item--active');

      Object.entries(views).forEach(([key, viewEl]) => {
        if (viewEl) {
          viewEl.classList.toggle('view--active', key === targetView);
        }
      });
    });
  });

  // ─── 4. Live UI State Elements ─────────────────────────────────────────────
  const badgeAutostart = document.getElementById('badgeAutostart');
  const bannerAutostart = document.getElementById('bannerAutostart');
  const txtStartupModeBar = document.getElementById('txtStartupModeBar');
  const dotStartupMode = document.getElementById('dotStartupMode');
  const txtStartupMode = document.getElementById('txtStartupMode');
  const chkAutoLaunch = document.getElementById('chkAutoLaunch');

  const dotStatus = document.getElementById('dotStatus');
  const sidebarDot = document.getElementById('sidebarDot');
  const dotStatusBar = document.getElementById('dotStatusBar');
  const txtRelayStatus = document.getElementById('txtRelayStatus');
  const txtRelayDetail = document.getElementById('txtRelayDetail');
  const sidebarStatusText = document.getElementById('sidebarStatusText');
  const txtStatusBar = document.getElementById('txtStatusBar');
  const txtRoomId = document.getElementById('badgeRoomId');
  const txtRoomIdFull = document.getElementById('txtRoomIdFull');
  const txtPcName = document.getElementById('txtPcName');
  const txtResolution = document.getElementById('txtResolution');
  const txtRelayUrl = document.getElementById('txtRelayUrl');
  const txtRelayUrlInput = document.getElementById('txtRelayUrlInput');
  const dotPulse = document.getElementById('dotPulse');
  const txtPacketCount = document.getElementById('txtPacketCount');
  const txtCursorCoords = document.getElementById('txtCursorCoords');
  const txtLatency = document.getElementById('txtLatency');
  const sliderVolume = document.getElementById('sliderVolume');
  const txtVolume = document.getElementById('txtVolume');
  const badgeMuted = document.getElementById('badgeMuted');
  const logPane = document.getElementById('logPane');
  const btnClearLogs = document.getElementById('btnClearLogs');
  const btnConnect = document.getElementById('btnConnect');
  const btnReconnect = document.getElementById('btnReconnect');
  const btnLock = document.getElementById('btnLock');
  const btnHotspot = document.getElementById('btnHotspot');
  const btnFirewall = document.getElementById('btnFirewall');

  function log(msg) {
    if (!logPane) return;
    const time = new Date().toLocaleTimeString();
    const entry = document.createElement('div');
    entry.textContent = `[${time}] ${msg}`;
    logPane.appendChild(entry);
    logPane.scrollTop = logPane.scrollHeight;
  }

  if (txtResolution) {
    txtResolution.textContent = `${window.screen.width} × ${window.screen.height}`;
  }

  // Autostart indicator detection
  if (window.beatzAPI?.isAutostart) {
    if (badgeAutostart) badgeAutostart.hidden = false;
    if (bannerAutostart) bannerAutostart.hidden = false;
    if (txtStartupModeBar) {
      txtStartupModeBar.textContent = 'Auto-started (silent)';
      txtStartupModeBar.style.color = 'var(--color-success)';
    }
  }

  // Auto-launch registry state
  async function initAutoLaunch() {
    if (window.beatzAPI?.getAutoLaunch) {
      try {
        const settings = await window.beatzAPI.getAutoLaunch();
        const isEnabled = settings.openAtLogin;
        if (chkAutoLaunch) chkAutoLaunch.checked = isEnabled;
        updateStartupStatus(isEnabled);
      } catch (e) {
        log(`Failed to query auto-launch: ${e.message}`);
      }
    }
  }

  function updateStartupStatus(enabled) {
    if (dotStartupMode) {
      dotStartupMode.className = `status-dot ${enabled ? 'connected' : 'status-dot--amber'}`;
    }
    if (txtStartupMode) {
      txtStartupMode.textContent = enabled
        ? 'Windows startup: Enabled — launches silently with --autostart --minimized'
        : 'Windows startup: Not configured';
    }
  }

  if (chkAutoLaunch) {
    chkAutoLaunch.addEventListener('change', async () => {
      const enabled = chkAutoLaunch.checked;
      if (window.beatzAPI?.setAutoLaunch) {
        try {
          await window.beatzAPI.setAutoLaunch(enabled);
          updateStartupStatus(enabled);
          log(enabled ? 'Enabled Windows silent background startup' : 'Disabled Windows startup');
        } catch (e) {
          log(`Error setting auto-launch: ${e.message}`);
        }
      }
    });
  }

  initAutoLaunch();

  // ─── 5. LIVE Socket.IO Relay Connection Sync ──────────────────────────────
  function updateRelayUI(state) {
    if (!state) return;

    const isConn = state.isConnected === true;
    const isConnPending = state.status === 'connecting';

    if (dotStatus) {
      dotStatus.classList.toggle('connected', isConn);
      if (isConnPending) dotStatus.className = 'relay-indicator';
    }
    if (sidebarDot) sidebarDot.classList.toggle('connected', isConn);
    if (dotStatusBar) dotStatusBar.classList.toggle('connected', isConn);

    const mainStatusText = isConn
      ? 'Connected to Relay'
      : (isConnPending ? 'Connecting…' : 'Disconnected');

    if (txtRelayStatus) txtRelayStatus.textContent = mainStatusText;
    if (txtRelayDetail) txtRelayDetail.textContent = state.detail || '';
    if (sidebarStatusText) sidebarStatusText.textContent = mainStatusText;
    if (txtStatusBar) txtStatusBar.textContent = `${mainStatusText} · ${state.detail || ''}`;

    const displayRoom = state.roomId || (isConn ? state.pcId : '—');
    if (txtRoomId) txtRoomId.textContent = displayRoom;
    if (txtRoomIdFull) txtRoomIdFull.textContent = displayRoom;

    if (txtPcName && state.pcName) txtPcName.textContent = state.pcName;
    if (txtRelayUrl && state.relayUrl) txtRelayUrl.textContent = state.relayUrl;
    if (txtRelayUrlInput && state.relayUrl && !txtRelayUrlInput.value) {
      txtRelayUrlInput.value = state.relayUrl;
    }

    if (state.packetCount !== undefined && txtPacketCount) {
      txtPacketCount.textContent = Number(state.packetCount).toLocaleString();
    }
    if (state.latency !== undefined && txtLatency) {
      txtLatency.textContent = state.latency ? `${state.latency} ms` : '—';
    }
  }

  // Subscribe to real-time events from Main process
  if (window.beatzAPI?.onRelayState) {
    window.beatzAPI.onRelayState((state) => {
      updateRelayUI(state);
    });
  }

  if (window.beatzAPI?.onRelayPacket) {
    window.beatzAPI.onRelayPacket((data) => {
      if (txtPacketCount) txtPacketCount.textContent = Number(data.packetCount || 0).toLocaleString();
      if (txtLatency) txtLatency.textContent = data.latency ? `${data.latency} ms` : '—';
      if (dotPulse) {
        dotPulse.classList.add('pulsing');
        setTimeout(() => dotPulse.classList.remove('pulsing'), 150);
      }
    });
  }

  if (window.beatzAPI?.onRelayLog) {
    window.beatzAPI.onRelayLog((msg) => {
      log(msg);
    });
  }

  // Query initial live state on mount
  if (window.beatzAPI?.getRelayState) {
    window.beatzAPI.getRelayState().then((state) => {
      updateRelayUI(state);
    });
  }

  // Track cursor position
  window.addEventListener('mousemove', (e) => {
    if (txtCursorCoords) {
      txtCursorCoords.textContent = `${e.clientX}, ${e.clientY}`;
    }
  });

  // Slider Volume Control
  if (sliderVolume) {
    sliderVolume.addEventListener('input', () => {
      const val = Number(sliderVolume.value);
      if (txtVolume) txtVolume.textContent = `${val}%`;
      if (badgeMuted) badgeMuted.hidden = val > 0;
      window.beatzAPI?.setVolume(val);
    });
  }

  // ─── 6. Action Handlers (Connect, Reconnect, Lock, Hotspot, Firewall) ──────
  if (btnConnect) {
    btnConnect.addEventListener('click', async () => {
      const url = txtRelayUrlInput?.value.trim();
      log(`Initiating manual connection to ${url || 'default relay'}…`);
      const state = await window.beatzAPI?.connectRelay(url);
      updateRelayUI(state);
    });
  }

  if (btnReconnect) {
    btnReconnect.addEventListener('click', async () => {
      const url = txtRelayUrlInput?.value.trim();
      log('Triggering relay reconnect…');
      const state = await window.beatzAPI?.connectRelay(url);
      updateRelayUI(state);
    });
  }

  if (btnLock) {
    btnLock.addEventListener('click', async () => {
      log('Triggering Workstation Lock…');
      const res = await window.beatzAPI?.lockWorkstation();
      if (res?.message) log(res.message);
    });
  }

  if (btnHotspot) {
    btnHotspot.addEventListener('click', async () => {
      log('Triggering SoftAP Local Hotspot…');
      const res = await window.beatzAPI?.toggleHotspot();
      if (res?.message) log(res.message);
    });
  }

  if (btnFirewall) {
    btnFirewall.addEventListener('click', async () => {
      log('Verifying Windows Defender Firewall rule for port 8080…');
      const res = await window.beatzAPI?.addFirewallRule();
      if (res?.message) log(res.message);
      if (res?.error) log(`Firewall notice: ${res.error}`);
    });
  }

  if (btnClearLogs) {
    btnClearLogs.addEventListener('click', () => {
      if (logPane) logPane.innerHTML = '';
    });
  }
})();
