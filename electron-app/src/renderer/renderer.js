/**
 * Beatz Control — Renderer Logic
 * Microsoft Phone Link Fluent UI Controller with Smooth Window Animations & Theme Switching
 */

(function () {
  // ─── 1. Window Animations & Controls (Minimize, Maximize, Close) ────────────
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
        // Clear minimize class so when restored the window is visible and plays restore anim
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

  // Restore animation when window regains focus or visibility
  window.addEventListener('focus', () => {
    document.body.classList.remove('window-animating-minimize');
    document.body.classList.add('window-animating-restore');
    setTimeout(() => {
      document.body.classList.remove('window-animating-restore');
    }, 250);
  });

  // Maximize Icon synchronization
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

  // ─── 2. Dark Mode / Light Mode Theme Switching ─────────────────────────────
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

    // Update active button state
    Object.entries(themeButtons).forEach(([key, btn]) => {
      if (btn) btn.classList.toggle('active', key === themeChoice);
    });

    // Notify Electron main process for native titlebar & window chrome
    if (window.beatzAPI?.setTheme) {
      window.beatzAPI.setTheme(themeChoice);
    }
  }

  // Bind theme buttons
  Object.entries(themeButtons).forEach(([key, btn]) => {
    if (btn) {
      btn.addEventListener('click', () => applyTheme(key));
    }
  });

  // Listen to system preference changes when in 'system' mode
  prefersDarkMediaQuery.addEventListener('change', () => {
    const saved = localStorage.getItem('beatz-theme') || 'system';
    if (saved === 'system') {
      applyTheme('system');
    }
  });

  // Initialize theme on load
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

  // ─── 4. UI Elements & State ───────────────────────────────────────────────
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

  let packetCount = 0;
  const DEFAULT_RELAY_URL = 'https://backend-n4yg.onrender.com';

  function log(msg) {
    if (!logPane) return;
    const time = new Date().toLocaleTimeString();
    const entry = document.createElement('div');
    entry.textContent = `[${time}] ${msg}`;
    logPane.appendChild(entry);
    logPane.scrollTop = logPane.scrollHeight;
  }

  // Initial values
  if (txtPcName) txtPcName.textContent = window.navigator.platform || 'Windows PC';
  if (txtResolution) txtResolution.textContent = `${window.screen.width} × ${window.screen.height}`;
  if (txtRelayUrl) txtRelayUrl.textContent = DEFAULT_RELAY_URL;
  if (txtRelayUrlInput) txtRelayUrlInput.value = DEFAULT_RELAY_URL;

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

  // ─── 5. Simulated / Real Relay Handshake ──────────────────────────────────
  function setConnectionState(connected, message, roomId = 'BEATZ-8080') {
    if (dotStatus) dotStatus.classList.toggle('connected', connected);
    if (sidebarDot) sidebarDot.classList.toggle('connected', connected);
    if (dotStatusBar) dotStatusBar.classList.toggle('connected', connected);

    const statusText = connected ? 'Connected to Relay' : 'Disconnected';
    if (txtRelayStatus) txtRelayStatus.textContent = statusText;
    if (txtRelayDetail) txtRelayDetail.textContent = message;
    if (sidebarStatusText) sidebarStatusText.textContent = statusText;
    if (txtStatusBar) txtStatusBar.textContent = `${statusText} (${message})`;

    if (txtRoomId) txtRoomId.textContent = connected ? roomId : '—';
    if (txtRoomIdFull) txtRoomIdFull.textContent = connected ? roomId : '—';
  }

  function simulateRelayHandshake() {
    setConnectionState(false, 'Connecting to relay cluster...');
    log(`Connecting to ${txtRelayUrlInput?.value || DEFAULT_RELAY_URL}...`);

    setTimeout(() => {
      const generatedRoomId = 'BEATZ-' + Math.floor(1000 + Math.random() * 9000);
      setConnectionState(true, 'Ready for remote commands', generatedRoomId);
      log(`Connected to relay successfully. Room ID: ${generatedRoomId}`);
      if (txtLatency) txtLatency.textContent = '34 ms';

      setInterval(() => {
        packetCount++;
        if (txtPacketCount) txtPacketCount.textContent = packetCount.toLocaleString();
        if (dotPulse) {
          dotPulse.classList.add('pulsing');
          setTimeout(() => dotPulse.classList.remove('pulsing'), 150);
        }
      }, 3000);
    }, 1000);
  }

  simulateRelayHandshake();

  // Mouse telemetry tracking
  window.addEventListener('mousemove', (e) => {
    if (txtCursorCoords) {
      txtCursorCoords.textContent = `${e.clientX}, ${e.clientY}`;
    }
  });

  // Volume slider
  if (sliderVolume) {
    sliderVolume.addEventListener('input', () => {
      const val = sliderVolume.value;
      if (txtVolume) txtVolume.textContent = `${val}%`;
      if (badgeMuted) badgeMuted.hidden = val > 0;
    });
  }

  // Quick Action Buttons
  if (btnConnect) {
    btnConnect.addEventListener('click', () => {
      const url = txtRelayUrlInput?.value.trim();
      if (url && txtRelayUrl) {
        txtRelayUrl.textContent = url;
        simulateRelayHandshake();
      }
    });
  }

  if (btnReconnect) {
    btnReconnect.addEventListener('click', () => {
      simulateRelayHandshake();
    });
  }

  if (btnLock) {
    btnLock.addEventListener('click', () => {
      log('Workstation lock signal triggered');
    });
  }

  if (btnHotspot) {
    btnHotspot.addEventListener('click', () => {
      log('SoftAP Local Hotspot toggled');
    });
  }

  if (btnFirewall) {
    btnFirewall.addEventListener('click', () => {
      log('Firewall rule verified for port 8080');
    });
  }

  if (btnClearLogs) {
    btnClearLogs.addEventListener('click', () => {
      if (logPane) logPane.innerHTML = '';
    });
  }
})();
