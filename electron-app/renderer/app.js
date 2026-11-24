// ═══════════════════════════════════════════════════════════════════════════
// BEATZ CONTROL - NATIVE CLIENT CONTROLLER (CLEAN WINUI 3 ARCHITECTURE)
// Zero-latency IPC bridge, live state binding, telemetry & activity stream
// ═══════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────
// 1. DOM REFERENCES
// ─────────────────────────────────────────────────────────────

// Titlebar & Window Controls
const titlebarDrag = document.getElementById('titlebarDrag');
const btnMinimize = document.getElementById('btnMinimize');
const btnMaximize = document.getElementById('btnMaximize');
const btnHideTray = document.getElementById('btnHideTray');
const btnClose = document.getElementById('btnClose');
const iconMax = document.querySelector('.icon-max');
const iconRes = document.querySelector('.icon-res');

// Live IPC Status Pill
const ipcStatusPill = document.getElementById('ipcStatusPill');
const statusDot = document.getElementById('statusDot');
const ipcStatusLabel = document.getElementById('ipcStatusLabel');

// Sidebar Elements
const deviceGlanceCard = document.getElementById('deviceGlanceCard');
const sidebarDeviceName = document.getElementById('sidebarDeviceName');
const sidebarBatteryBar = document.getElementById('sidebarBatteryBar');
const sidebarChargingLightning = document.getElementById('sidebarChargingLightning');
const sidebarBatteryLabel = document.getElementById('sidebarBatteryLabel');
const sidebarConnDot = document.getElementById('sidebarConnDot');
const sidebarConnText = document.getElementById('sidebarConnText');
const glanceClock = document.getElementById('glanceClock');

// Navigation & Pages
const navItems = document.querySelectorAll('.fluent-nav-item');
const pages = document.querySelectorAll('.fluent-page');

// Sidebar Media Player
const mediaTitle = document.getElementById('mediaTitle');
const mediaArtist = document.getElementById('mediaArtist');
const btnMediaPrev = document.getElementById('btnMediaPrev');
const btnMediaPlay = document.getElementById('btnMediaPlay');
const iconMediaPlay = document.getElementById('iconMediaPlay');
const iconMediaPause = document.getElementById('iconMediaPause');
const btnMediaNext = document.getElementById('btnMediaNext');

// Update Notification Banner
const updateBanner = document.getElementById('updateBanner');
const updateTitle = document.getElementById('updateTitle');
const updateDesc = document.getElementById('updateDesc');
const btnApplyUpdate = document.getElementById('btnApplyUpdate');
const btnDismissUpdate = document.getElementById('btnDismissUpdate');
const btnCheckUpdatesNow = document.getElementById('btnCheckUpdatesNow');
const updateStatusBadge = document.getElementById('updateStatusBadge');

// Status Page Elements
const mainHeaderDeviceName = document.getElementById('mainHeaderDeviceName');
const mainHeaderSub = document.getElementById('mainHeaderSub');
const relayStatusBadge = document.getElementById('relayStatusBadge');
const relayStatusText = document.getElementById('relayStatusText');
const heroConnStateTag = document.getElementById('heroConnStateTag');
const heroDeviceName = document.getElementById('heroDeviceName');
const heroDeviceDesc = document.getElementById('heroDeviceDesc');

// Circular Battery Ring
const ringProgress = document.getElementById('ringProgress');
const ringBolt = document.getElementById('ringBolt');
const ringBatteryNum = document.getElementById('ringBatteryNum');
const ringBatteryState = document.getElementById('ringBatteryState');

// Quick Toggles
const pillDnd = document.getElementById('pillDnd');
const valDnd = document.getElementById('valDnd');
const pillBluetooth = document.getElementById('pillBluetooth');
const valBt = document.getElementById('valBt');
const pillHotspot = document.getElementById('pillHotspot');
const valRelay = document.getElementById('valRelay');
const btnToggleBluetooth = document.getElementById('btnToggleBluetooth');
const btnToggleDnd = document.getElementById('btnToggleDnd');

// Workstation Volume & Controls
const volumeSlider = document.getElementById('volumeSlider');
const volumeNumBadge = document.getElementById('volumeNumBadge');
const btnToggleMute = document.getElementById('btnToggleMute');
const muteIconSpeaker = document.getElementById('muteIconSpeaker');
const muteIconMuted = document.getElementById('muteIconMuted');
const muteLabel = document.getElementById('muteLabel');

// PC Power Buttons
const btnLockPc = document.getElementById('btnLockPc');
const btnSleepPc = document.getElementById('btnSleepPc');
const btnRestartPc = document.getElementById('btnRestartPc');
const btnShutdownPc = document.getElementById('btnShutdownPc');

// Telemetry Cells
const telemetryHost = document.getElementById('telemetryHost');
const telemetryUser = document.getElementById('telemetryUser');
const telemetryIpc = document.getElementById('telemetryIpc');
const telemetryRoomId = document.getElementById('telemetryRoomId');

// Messages Elements
const chatInput = document.getElementById('chatInput');
const btnSendMessage = document.getElementById('btnSendMessage');
const chatHistory = document.getElementById('chatHistory');
const convoRows = document.querySelectorAll('.convo-row');

// Activity Logs Console
const consoleBody = document.getElementById('consoleBody');
const btnClearLogs = document.getElementById('btnClearLogs');

// Active State Cache
let currentState = null;
let isUserDraggingSlider = false;

// ─────────────────────────────────────────────────────────────
// 2. ACTIVITY LOGGING MANAGER
// ─────────────────────────────────────────────────────────────
function appendLog(level, message) {
  if (!consoleBody) return;
  const now = new Date();
  const timeStr = now.toTimeString().split(' ')[0] + '.' + String(now.getMilliseconds()).padStart(3, '0');
  
  const line = document.createElement('div');
  line.className = `log-line ${level}`;
  
  const timeSpan = document.createElement('span');
  timeSpan.className = 'log-time';
  timeSpan.textContent = `[${timeStr}]`;
  
  const textSpan = document.createElement('span');
  textSpan.className = 'log-text';
  textSpan.textContent = `[${level.toUpperCase()}] ${message}`;
  
  line.appendChild(timeSpan);
  line.appendChild(textSpan);
  
  consoleBody.appendChild(line);
  consoleBody.scrollTop = consoleBody.scrollHeight;
}

btnClearLogs?.addEventListener('click', () => {
  if (consoleBody) {
    consoleBody.innerHTML = '';
    appendLog('info', 'Logs cleared by user.');
  }
});

// ─────────────────────────────────────────────────────────────
// 3. NAVIGATION TAB SWITCHING
// ─────────────────────────────────────────────────────────────
navItems.forEach(item => {
  item.addEventListener('click', () => {
    const targetTab = item.getAttribute('data-tab');
    navItems.forEach(n => n.classList.remove('active'));
    pages.forEach(p => p.classList.remove('active'));

    item.classList.add('active');
    const targetPane = document.getElementById(`pane-${targetTab}`);
    if (targetPane) {
      targetPane.classList.add('active');
      appendLog('info', `Switched view to '${targetTab}' tab`);
    }
  });
});

// Live Glance Clock
function updateClock() {
  const now = new Date();
  const hrs = String(now.getHours()).padStart(2, '0');
  const mins = String(now.getMinutes()).padStart(2, '0');
  if (glanceClock) glanceClock.textContent = `${hrs}:${mins}`;
}
setInterval(updateClock, 1000);
updateClock();

// ─────────────────────────────────────────────────────────────
// 4. FRAMELESS TITLEBAR CONTROLS & EVENTS
// ─────────────────────────────────────────────────────────────
btnMinimize?.addEventListener('click', () => {
  window.pcControlApi.minimize();
});

btnMaximize?.addEventListener('click', () => {
  window.pcControlApi.toggleMaximize();
});

titlebarDrag?.addEventListener('dblclick', () => {
  window.pcControlApi.toggleMaximize();
});

btnHideTray?.addEventListener('click', () => {
  window.pcControlApi.hideToTray();
  appendLog('info', 'Companion minimized to Windows system tray.');
});

btnClose?.addEventListener('click', () => {
  window.pcControlApi.close();
});

function updateMaximizedUI(isMaximized) {
  if (isMaximized) {
    document.body.classList.add('maximized');
    if (iconMax) iconMax.style.display = 'none';
    if (iconRes) iconRes.style.display = 'block';
    if (btnMaximize) btnMaximize.title = 'Restore Down';
  } else {
    document.body.classList.remove('maximized');
    if (iconMax) iconMax.style.display = 'block';
    if (iconRes) iconRes.style.display = 'none';
    if (btnMaximize) btnMaximize.title = 'Maximize';
  }
}

window.pcControlApi.onWindowStateChange?.((state) => {
  updateMaximizedUI(state.isMaximized);
});

// ─────────────────────────────────────────────────────────────
// 5. LIVE ZERO-LATENCY HANDSHAKE & IPC TELEMETRY
// ─────────────────────────────────────────────────────────────
window.pcControlApi.onHandshakeVerified?.((data) => {
  const lat = data.latencyMs ? `${data.latencyMs}ms` : '<1ms';
  if (statusDot) {
    statusDot.className = 'status-dot';
  }
  if (ipcStatusLabel) {
    ipcStatusLabel.textContent = `Connected • ${lat}`;
  }
  if (telemetryIpc) {
    telemetryIpc.textContent = `${lat} (${data.protocol || 'Loopback'})`;
  }
  appendLog('success', `Zero-latency IPC Handshake verified over ${data.protocol || 'WebSocket'} in ${lat}`);
});

// ─────────────────────────────────────────────────────────────
// 6. HARDWARE & WORKSTATION ACTIONS
// ─────────────────────────────────────────────────────────────
btnLockPc?.addEventListener('click', () => {
  appendLog('info', 'Command dispatched: lock_pc');
  window.pcControlApi.sendCommand({ type: 'lock_pc' });
});

btnSleepPc?.addEventListener('click', () => {
  if (confirm('Put workstation into Sleep mode?')) {
    appendLog('warning', 'Command dispatched: sleep');
    window.pcControlApi.sendCommand({ type: 'sleep' });
  }
});

btnRestartPc?.addEventListener('click', () => {
  if (confirm('Restart workstation immediately?')) {
    appendLog('warning', 'Command dispatched: restart');
    window.pcControlApi.sendCommand({ type: 'restart' });
  }
});

btnShutdownPc?.addEventListener('click', () => {
  if (confirm('Shutdown workstation?')) {
    appendLog('error', 'Command dispatched: shutdown');
    window.pcControlApi.sendCommand({ type: 'shutdown' });
  }
});

// Volume & Mute Control
volumeSlider?.addEventListener('input', (e) => {
  isUserDraggingSlider = true;
  const val = parseInt(e.target.value, 10);
  if (volumeNumBadge) volumeNumBadge.textContent = `${val}%`;
});

volumeSlider?.addEventListener('change', (e) => {
  isUserDraggingSlider = false;
  const val = parseInt(e.target.value, 10);
  appendLog('info', `Set master volume to ${val}%`);
  window.pcControlApi.sendCommand({ type: 'set_volume', volume: val });
});

btnToggleMute?.addEventListener('click', () => {
  appendLog('info', 'Toggled master audio mute');
  window.pcControlApi.sendCommand({ type: 'toggle_mute' });
});

// Media Controls
btnMediaPlay?.addEventListener('click', () => {
  appendLog('info', 'Media toggle play/pause');
  window.pcControlApi.sendCommand({ type: 'media_play_pause' });
});
btnMediaPrev?.addEventListener('click', () => {
  appendLog('info', 'Media previous track');
  window.pcControlApi.sendCommand({ type: 'media_prev' });
});
btnMediaNext?.addEventListener('click', () => {
  appendLog('info', 'Media next track');
  window.pcControlApi.sendCommand({ type: 'media_next' });
});

// Quick Toggles
btnToggleDnd?.addEventListener('click', () => {
  window.pcControlApi.sendCommand({ type: 'toggle_dnd' });
});
btnToggleBluetooth?.addEventListener('click', () => {
  window.pcControlApi.sendCommand({ type: 'toggle_bluetooth' });
});

// ─────────────────────────────────────────────────────────────
// 7. APP LAUNCHER & SCREEN MIRROR ACTIONS
// ─────────────────────────────────────────────────────────────
document.querySelectorAll('.app-launcher-card').forEach(card => {
  card.addEventListener('click', () => {
    const appName = card.getAttribute('data-app');
    appendLog('info', `Requested mobile app launch: ${appName}`);
    window.pcControlApi.sendCommand({ type: 'launch_app', app: appName });
  });
});

document.getElementById('btnOpenScreenMirror')?.addEventListener('click', () => {
  appendLog('info', 'Initiating phone screen mirroring session...');
  window.pcControlApi.sendCommand({ type: 'open_mirror' });
});

// Photos filter pills
document.querySelectorAll('.filter-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    document.querySelectorAll('.filter-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    appendLog('info', `Filtered photos view by: ${btn.textContent.trim()}`);
  });
});

// Convo list selection
convoRows.forEach(row => {
  row.addEventListener('click', () => {
    convoRows.forEach(r => r.classList.remove('active'));
    row.classList.add('active');
  });
});

// ─────────────────────────────────────────────────────────────
// 8. CHAT MESSAGING
// ─────────────────────────────────────────────────────────────
function sendMessage() {
  if (!chatInput) return;
  const text = chatInput.value.trim();
  if (!text) return;

  const bubble = document.createElement('div');
  bubble.className = 'chat-bubble sent';
  bubble.innerHTML = `<p>${escapeHtml(text)}</p><span class="time-label">${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>`;
  chatHistory.appendChild(bubble);
  chatHistory.scrollTop = chatHistory.scrollHeight;

  appendLog('info', `Message sent: "${text}"`);
  window.pcControlApi.sendCommand({ type: 'send_sms', message: text });
  chatInput.value = '';
}

btnSendMessage?.addEventListener('click', sendMessage);
chatInput?.addEventListener('keydown', (e) => {
  if (e.key === 'Enter') sendMessage();
});

function escapeHtml(str) {
  return str.replace(/[&<>'"]/g, tag => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    "'": '&#39;',
    '"': '&quot;'
  }[tag] || tag));
}

// ─────────────────────────────────────────────────────────────
// 9. STATE RENDERING & TELEMETRY BINDING
// ─────────────────────────────────────────────────────────────
function renderState(state) {
  if (!state) return;
  currentState = state;

  // IPC Badge
  if (state.serviceRunning) {
    if (statusDot) statusDot.className = 'status-dot';
    const lat = state.handshakeLatencyMs ? `${state.handshakeLatencyMs}ms` : '<1ms';
    if (ipcStatusLabel) ipcStatusLabel.textContent = `Connected • ${lat}`;
  } else {
    if (statusDot) statusDot.className = 'status-dot connecting';
    if (ipcStatusLabel) ipcStatusLabel.textContent = 'Connecting...';
  }

  // Connection State & Device Hardware
  const isConn = state.isConnected;
  const dName = isConn ? (state.deviceName || 'Samsung Galaxy S24 Ultra') : 'No Device Connected';

  if (sidebarDeviceName) sidebarDeviceName.textContent = dName;
  if (mainHeaderDeviceName) mainHeaderDeviceName.textContent = dName;
  if (heroDeviceName) heroDeviceName.textContent = isConn ? dName : 'Waiting for Device';

  if (isConn) {
    deviceGlanceCard?.classList.add('connected');
    if (sidebarConnDot) sidebarConnDot.className = 'status-indicator-dot active';
    if (sidebarConnText) sidebarConnText.textContent = `Connected (${state.clientEndpoint || 'Wi-Fi'})`;
    
    if (heroConnStateTag) {
      heroConnStateTag.textContent = 'CONNECTED & ACTIVE';
      heroConnStateTag.style.background = 'rgba(16, 185, 129, 0.15)';
      heroConnStateTag.style.color = '#34d399';
    }
    if (heroDeviceDesc) {
      heroDeviceDesc.textContent = `Your mobile device is actively connected via ${state.clientEndpoint || 'Wi-Fi'}. High-throughput remote control, audio sync, and instant notifications are active with zero latency.`;
    }
  } else {
    deviceGlanceCard?.classList.remove('connected');
    if (sidebarConnDot) sidebarConnDot.className = 'status-indicator-dot';
    if (sidebarConnText) sidebarConnText.textContent = 'Awaiting pairing...';
    
    if (heroConnStateTag) {
      heroConnStateTag.textContent = 'SEARCHING FOR PHONE';
      heroConnStateTag.style.background = 'rgba(245, 158, 11, 0.15)';
      heroConnStateTag.style.color = '#fcd34d';
    }
    if (heroDeviceDesc) {
      heroDeviceDesc.textContent = 'Awaiting companion connection. Pair the mobile app on your local Wi-Fi or via the secure cloud relay to enable remote control, audio sync, and instant notifications.';
    }
  }

  // Battery Level & Charging Indicator
  const battery = Math.max(0, Math.min(100, state.batteryPercentage ?? 100));
  if (sidebarBatteryLabel) sidebarBatteryLabel.textContent = `${battery}%`;
  if (sidebarBatteryBar) {
    sidebarBatteryBar.style.width = `${battery}%`;
    sidebarBatteryBar.classList.remove('med', 'low');
    if (battery <= 20) sidebarBatteryBar.classList.add('low');
    else if (battery <= 50) sidebarBatteryBar.classList.add('med');
  }

  if (state.isCharging) {
    if (sidebarChargingLightning) sidebarChargingLightning.style.display = 'block';
    if (ringBolt) ringBolt.style.display = 'block';
    if (ringBatteryState) ringBatteryState.textContent = 'Fast Charging';
  } else {
    if (sidebarChargingLightning) sidebarChargingLightning.style.display = 'none';
    if (ringBolt) ringBolt.style.display = 'none';
    if (ringBatteryState) ringBatteryState.textContent = isConn ? 'On Battery' : 'Disconnected';
  }

  // Circular Ring Telemetry
  if (ringBatteryNum) ringBatteryNum.textContent = `${battery}%`;
  if (ringProgress) {
    const circumference = 2 * Math.PI * 42; // ~263.89
    const offset = circumference - (battery / 100) * circumference;
    ringProgress.style.strokeDashoffset = offset;

    if (battery <= 20) ringProgress.style.stroke = '#EF4444';
    else if (battery <= 50) ringProgress.style.stroke = '#F59E0B';
    else ringProgress.style.stroke = '#10B981';
  }

  // Relay State
  if (state.relayConnected) {
    if (relayStatusText) relayStatusText.textContent = 'Relay Online';
    if (valRelay) valRelay.textContent = state.relayRoomId ? state.relayRoomId.substring(0, 10) : 'Active';
    if (telemetryRoomId) telemetryRoomId.textContent = state.relayRoomId || 'Render Cloud';
  } else {
    if (relayStatusText) relayStatusText.textContent = 'Relay Ready';
    if (valRelay) valRelay.textContent = 'backend-n4yg';
    if (telemetryRoomId) telemetryRoomId.textContent = state.relayRoomId || 'backend-n4yg';
  }

  // Toggles
  if (valDnd) valDnd.textContent = state.doNotDisturb ? 'On' : 'Off';
  if (valBt) valBt.textContent = state.bluetoothEnabled ? 'Connected' : 'Off';

  // Media Player
  if (state.currentTrack && mediaTitle) mediaTitle.textContent = state.currentTrack;
  if (state.currentArtist && mediaArtist) mediaArtist.textContent = state.currentArtist;

  if (state.isPlaying) {
    if (iconMediaPause) iconMediaPause.style.display = 'block';
    if (iconMediaPlay) iconMediaPlay.style.display = 'none';
  } else {
    if (iconMediaPause) iconMediaPause.style.display = 'none';
    if (iconMediaPlay) iconMediaPlay.style.display = 'block';
  }

  // Master Volume
  if (!isUserDraggingSlider && typeof state.masterVolume === 'number' && volumeSlider) {
    volumeSlider.value = state.masterVolume;
    if (volumeNumBadge) volumeNumBadge.textContent = `${state.masterVolume}%`;
  }

  // Audio Mute State
  if (state.isMuted) {
    btnToggleMute?.classList.add('active');
    if (muteIconMuted) muteIconMuted.style.display = 'block';
    if (muteIconSpeaker) muteIconSpeaker.style.display = 'none';
    if (muteLabel) muteLabel.textContent = 'Muted';
  } else {
    btnToggleMute?.classList.remove('active');
    if (muteIconMuted) muteIconMuted.style.display = 'none';
    if (muteIconSpeaker) muteIconSpeaker.style.display = 'block';
    if (muteLabel) muteLabel.textContent = 'Mute';
  }

  // Workstation Telemetry Cells
  if (state.machineName && telemetryHost) telemetryHost.textContent = state.machineName;
  if (state.userName && telemetryUser) telemetryUser.textContent = state.userName;
}

// ─────────────────────────────────────────────────────────────
// 10. AUTOMATED UPDATE & VERSION PIPELINE
// ─────────────────────────────────────────────────────────────
btnDismissUpdate?.addEventListener('click', () => {
  if (updateBanner) updateBanner.style.display = 'none';
});

btnCheckUpdatesNow?.addEventListener('click', async () => {
  if (updateStatusBadge) updateStatusBadge.textContent = 'Checking...';
  appendLog('info', 'Pinging remote manifest for update parity...');
  const res = await window.pcControlApi.checkForUpdates();
  if (res && res.updateAvailable) {
    appendLog('warning', `New release found: v${res.latestVersion}`);
    if (updateStatusBadge) updateStatusBadge.textContent = `v${res.latestVersion} Available`;
    showUpdateBanner(`New Release v${res.latestVersion}`, res.releaseNotes, false);
  } else {
    appendLog('success', 'Client is operating on the latest release build.');
    if (updateStatusBadge) updateStatusBadge.textContent = 'Up to date (v2.0.0)';
    setTimeout(() => {
      if (updateStatusBadge) updateStatusBadge.textContent = 'v2.0.0 (Latest)';
    }, 3000);
  }
});

btnApplyUpdate?.addEventListener('click', () => {
  appendLog('warning', 'Applying background update patch & restarting application...');
  window.pcControlApi.applyUpdate();
});

function showUpdateBanner(title, desc, isReady = false) {
  if (!updateBanner) return;
  updateTitle.textContent = title;
  updateDesc.textContent = desc;
  updateBanner.style.display = 'flex';
  if (isReady && btnApplyUpdate) {
    btnApplyUpdate.style.display = 'inline-block';
    btnApplyUpdate.textContent = 'Restart & Install Patch';
  } else if (btnApplyUpdate) {
    btnApplyUpdate.style.display = 'inline-block';
    btnApplyUpdate.textContent = 'Downloading in background...';
  }
}

window.pcControlApi.onUpdateAvailable?.((info) => {
  if (updateStatusBadge) updateStatusBadge.textContent = `v${info.latestVersion} Available`;
  showUpdateBanner(`Update Available: v${info.latestVersion}`, info.releaseNotes || 'Silent background patch downloading...', false);
});

window.pcControlApi.onUpdateReady?.((info) => {
  if (updateStatusBadge) updateStatusBadge.textContent = 'Update Ready to Install';
  showUpdateBanner(`Update Ready: v${info.latestVersion}`, 'Installer payload downloaded. Click to restart and apply patch.', true);
});

// ─────────────────────────────────────────────────────────────
// 11. BOOTSTRAP LIFECYCLE
// ─────────────────────────────────────────────────────────────
window.addEventListener('DOMContentLoaded', async () => {
  appendLog('info', 'Windows 11 Fluent UI initialized successfully.');

  // Check window maximize state
  try {
    const isMax = await window.pcControlApi.isMaximized?.();
    updateMaximizedUI(isMax);
  } catch {}

  // Immediate query of backend state upon launch (Phase 3 & 6)
  try {
    const initialState = await window.pcControlApi.getState();
    appendLog('info', 'Initial state query resolved.');
    renderState(initialState);
  } catch (e) {
    appendLog('error', `State initialization error: ${e.message}`);
  }

  // Subscribe to live state updates
  window.pcControlApi.onStateUpdate((updatedState) => {
    renderState(updatedState);
  });
});
