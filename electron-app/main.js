const { app, BrowserWindow, Tray, Menu, ipcMain, nativeImage } = require('electron');
const path = require('path');
const fs = require('fs');
const https = require('https');
const http = require('http');
const net = require('net');
const WebSocket = require('ws');
const { spawn, exec } = require('child_process');

let mainWindow = null;
let tray = null;
let wsClient = null;
let serviceProcess = null;
let reconnectTimer = null;
let isQuitting = false;
let handshakeVerified = false;
let handshakeStartTime = 0;
let handshakeLatency = 0;

// Cached state from .NET backend
let latestState = {
  serviceRunning: false,
  isConnected: false,
  deviceName: 'No Mobile Device',
  model: 'Awaiting Pairing',
  batteryPercentage: 0,
  isCharging: false,
  clientEndpoint: '',
  connectedClientsCount: 0,
  masterVolume: 50,
  isMuted: false,
  machineName: 'This PC',
  userName: '',
  activeWindowsCount: 0,
  relayConnected: false,
  relayRoomId: '',
  relayUrl: 'https://backend-n4yg.onrender.com',
  wifiSignal: 4,
  cellularSignal: 4,
  bluetoothEnabled: true,
  doNotDisturb: false,
  currentTrack: 'No Media Playing',
  currentArtist: 'Beatz Companion',
  isPlaying: false,
  version: '2.0.0',
  handshakeVerified: false,
  handshakeLatencyMs: 0,
  timestamp: new Date().toISOString()
};

// Version Control & Update State
const CURRENT_VERSION = '2.0.0';
const UPDATE_MANIFEST_URL = 'https://backend-n4yg.onrender.com/api/release-manifest.json';
let downloadedUpdatePath = null;
let pendingUpdateInfo = null;

// Ensure single instance lock
const gotTheLock = app.requestSingleInstanceLock();
if (!gotTheLock) {
  app.quit();
} else {
  app.on('second-instance', () => {
    if (mainWindow) {
      if (mainWindow.isMinimized()) mainWindow.restore();
      mainWindow.show();
      mainWindow.focus();
    }
  });
}

// Locate self-contained PCControl.Service.exe
function getServiceExecutablePath() {
  const possiblePaths = [
    // Production packaged path (inside resources/backend)
    path.join(process.resourcesPath, 'backend', 'PCControl.Service.exe'),
    // Dev publish path (win-x64 self-contained single-file publish)
    path.join(__dirname, '..', 'PCControl.Service', 'bin', 'Release', 'net8.0-windows', 'win-x64', 'publish', 'PCControl.Service.exe'),
    // Alternative relative build path
    path.join(__dirname, '..', 'dist', 'Service', 'PCControl.Service.exe')
  ];

  for (const p of possiblePaths) {
    if (fs.existsSync(p)) return p;
  }
  return null;
}

// Ensure the background service is running and initiate immediate handshake
function ensureBackgroundServiceRunning() {
  handshakeStartTime = performance.now();

  exec('tasklist /FI "IMAGENAME eq PCControl.Service.exe"', (err, stdout) => {
    const isRunning = !err && stdout.includes('PCControl.Service.exe');

    if (isRunning) {
      console.log('[Main] PCControl.Service is already running in background.');
      // Verify handshake immediately (0ms delay)
      verifyImmediateHandshake();
      return;
    }

    const exePath = getServiceExecutablePath();
    if (exePath) {
      console.log(`[Main] Launching background service immediately: ${exePath}`);
      try {
        serviceProcess = spawn(exePath, [], {
          detached: true,
          stdio: 'ignore',
          windowsHide: true
        });
        serviceProcess.unref();
      } catch (e) {
        console.error('[Main] Failed to spawn service process:', e);
      }
    } else {
      console.warn('[Main] PCControl.Service executable not found on disk. Probing loopback socket...');
    }

    // Fast-poll verification probe loop (100ms intervals instead of slow arbitrary sleep)
    let attempts = 0;
    const probeInterval = setInterval(() => {
      attempts++;
      if (handshakeVerified || attempts > 30) {
        clearInterval(probeInterval);
        return;
      }
      verifyImmediateHandshake();
    }, 120);
  });
}

// Immediate WebSocket & Pipe Handshake Verification
function verifyImmediateHandshake() {
  if (wsClient && (wsClient.readyState === WebSocket.CONNECTING || wsClient.readyState === WebSocket.OPEN)) {
    return;
  }

  const IPC_PORT = 28888;
  const url = `ws://127.0.0.1:${IPC_PORT}/`;
  const connectStart = performance.now();

  try {
    wsClient = new WebSocket(url, {
      handshakeTimeout: 2000
    });

    wsClient.on('open', () => {
      const elapsed = performance.now() - connectStart;
      handshakeLatency = Math.max(0.2, Math.round(elapsed * 10) / 10);
      handshakeVerified = true;
      latestState.serviceRunning = true;
      latestState.handshakeVerified = true;
      latestState.handshakeLatencyMs = handshakeLatency;

      console.log(`[Main] Handshake verified via WebSocket (ws://127.0.0.1:${IPC_PORT}/) in ${handshakeLatency}ms.`);
      updateTrayMenu();

      const verificationPayload = {
        verified: true,
        protocol: 'WebSocket Loopback (RFC 6455)',
        port: IPC_PORT,
        latencyMs: handshakeLatency,
        timestamp: new Date().toISOString()
      };

      if (mainWindow && !mainWindow.isDestroyed()) {
        mainWindow.webContents.send('handshake-verified', verificationPayload);
        mainWindow.webContents.send('service-state', latestState);
      }
    });

    wsClient.on('message', (data) => {
      try {
        const text = data.toString('utf8');
        const parsed = JSON.parse(text);

        if (parsed.type === 'state_update' && parsed.data) {
          latestState = {
            ...latestState,
            ...parsed.data,
            serviceRunning: true,
            handshakeVerified: true,
            handshakeLatencyMs: handshakeLatency
          };
          updateTrayMenu();
          if (mainWindow && !mainWindow.isDestroyed()) {
            mainWindow.webContents.send('service-state', latestState);
          }
        } else if (parsed.type === 'apps_list' && parsed.apps) {
          if (mainWindow && !mainWindow.isDestroyed()) {
            mainWindow.webContents.send('apps-list', parsed.apps);
          }
        }
      } catch (err) {
        console.error('[Main] Error parsing IPC message:', err);
      }
    });

    wsClient.on('close', () => {
      latestState.serviceRunning = false;
      latestState.handshakeVerified = false;
      handshakeVerified = false;
      updateTrayMenu();
      scheduleReconnect();
    });

    wsClient.on('error', () => {
      latestState.serviceRunning = false;
      latestState.handshakeVerified = false;
      handshakeVerified = false;
      updateTrayMenu();
      // Probe Named Pipe if WebSocket is not ready yet
      probeNamedPipeHandshake();
      scheduleReconnect();
    });
  } catch (err) {
    probeNamedPipeHandshake();
    scheduleReconnect();
  }
}

// Secondary fallback: Probe Named Pipe Handshake (PCControl_IPC_Pipe)
function probeNamedPipeHandshake() {
  const pipePath = '\\\\.\\pipe\\PCControl_IPC_Pipe';
  const client = net.connect(pipePath, () => {
    console.log('[Main] Named Pipe handshake verified on', pipePath);
    latestState.serviceRunning = true;
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('handshake-verified', {
        verified: true,
        protocol: 'Windows Named Pipe (PCControl_IPC_Pipe)',
        latencyMs: 0.5,
        timestamp: new Date().toISOString()
      });
    }
    client.end();
  });

  client.on('error', () => {
    // Pipe not responding yet
  });
}

function scheduleReconnect() {
  if (reconnectTimer || isQuitting) return;
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    verifyImmediateHandshake();
  }, 1500);
}

// Send command to .NET service via WebSocket
function sendIpcCommand(commandObj) {
  if (wsClient && wsClient.readyState === WebSocket.OPEN) {
    try {
      wsClient.send(JSON.stringify(commandObj));
      return true;
    } catch (e) {
      console.error('[Main] Failed to send IPC command:', e);
    }
  }
  return false;
}

// ─────────────────────────────────────────────────────────────
// Version Control & Self-Update Pipeline
// ─────────────────────────────────────────────────────────────
function compareVersions(v1, v2) {
  const p1 = v1.split('.').map(n => parseInt(n, 10) || 0);
  const p2 = v2.split('.').map(n => parseInt(n, 10) || 0);
  for (let i = 0; i < Math.max(p1.length, p2.length); i++) {
    const num1 = p1[i] || 0;
    const num2 = p2[i] || 0;
    if (num1 > num2) return 1;
    if (num1 < num2) return -1;
  }
  return 0;
}

async function checkForUpdates(manual = false) {
  console.log(`[UpdateManager] Checking for updates against ${UPDATE_MANIFEST_URL}...`);
  return new Promise((resolve) => {
    try {
      const client = UPDATE_MANIFEST_URL.startsWith('https') ? https : http;
      const req = client.get(UPDATE_MANIFEST_URL, { timeout: 4000 }, (res) => {
        if (res.statusCode !== 200) {
          resolve({ updateAvailable: false, currentVersion: CURRENT_VERSION, message: 'Server status ' + res.statusCode });
          return;
        }

        let raw = '';
        res.on('data', chunk => raw += chunk);
        res.on('end', () => {
          try {
            const manifest = JSON.parse(raw);
            const latestVersion = manifest.version || '2.0.0';
            const hasUpdate = compareVersions(latestVersion, CURRENT_VERSION) > 0;

            if (hasUpdate) {
              pendingUpdateInfo = {
                currentVersion: CURRENT_VERSION,
                latestVersion: latestVersion,
                downloadUrl: manifest.downloadUrl,
                releaseNotes: manifest.releaseNotes || 'Critical stability, zero-latency IPC, and UI enhancements.'
              };

              console.log(`[UpdateManager] New version detected: v${latestVersion}. Initiating download...`);
              if (mainWindow && !mainWindow.isDestroyed()) {
                mainWindow.webContents.send('update-available', pendingUpdateInfo);
              }

              downloadUpdatePayload(manifest.downloadUrl);
              resolve({ updateAvailable: true, ...pendingUpdateInfo });
            } else {
              resolve({ updateAvailable: false, currentVersion: CURRENT_VERSION, latestVersion });
            }
          } catch (e) {
            resolve({ updateAvailable: false, currentVersion: CURRENT_VERSION, message: 'Invalid manifest format' });
          }
        });
      });

      req.on('error', () => {
        resolve({ updateAvailable: false, currentVersion: CURRENT_VERSION, offline: true });
      });

      req.on('timeout', () => {
        req.destroy();
        resolve({ updateAvailable: false, currentVersion: CURRENT_VERSION, timeout: true });
      });
    } catch (e) {
      resolve({ updateAvailable: false, currentVersion: CURRENT_VERSION, error: e.message });
    }
  });
}

function downloadUpdatePayload(url) {
  if (!url) return;
  const tempDir = app.getPath('temp');
  const targetFile = path.join(tempDir, 'BeatzControl_Update_Patch.exe');

  try {
    const file = fs.createWriteStream(targetFile);
    const client = url.startsWith('https') ? https : http;
    client.get(url, (res) => {
      res.pipe(file);
      file.on('finish', () => {
        file.close();
        downloadedUpdatePath = targetFile;
        console.log(`[UpdateManager] Update payload downloaded to ${targetFile}.`);
        if (mainWindow && !mainWindow.isDestroyed()) {
          mainWindow.webContents.send('update-ready', pendingUpdateInfo);
        }
      });
    }).on('error', (err) => {
      console.warn('[UpdateManager] Silent update download failed:', err.message);
    });
  } catch (err) {
    console.warn('[UpdateManager] Error preparing update download:', err);
  }
}

function applyDownloadedUpdate() {
  if (downloadedUpdatePath && fs.existsSync(downloadedUpdatePath)) {
    console.log(`[UpdateManager] Executing installer patch: ${downloadedUpdatePath}`);
    try {
      spawn(downloadedUpdatePath, ['/S'], {
        detached: true,
        stdio: 'ignore'
      });
      isQuitting = true;
      app.quit();
      return true;
    } catch (e) {
      console.error('[UpdateManager] Failed to launch update patch:', e);
    }
  }
  return false;
}

// ─────────────────────────────────────────────────────────────
// Windows 11 Fluent Frameless Window Creation & Controls
// ─────────────────────────────────────────────────────────────
function createWindow() {
  const isSilentStartup = process.argv.includes('--autostart') || process.argv.includes('--minimized') || process.argv.includes('-silent');

  mainWindow = new BrowserWindow({
    width: 1060,
    height: 740,
    minWidth: 920,
    minHeight: 640,
    show: !isSilentStartup,
    frame: false,
    titleBarStyle: 'hidden',
    backgroundColor: '#181c24',
    icon: path.join(__dirname, 'assets', 'icon.ico'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
      backgroundThrottling: false
    }
  });

  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));

  // Seamless handling of maximize and unmaximize/restore state events
  mainWindow.on('maximize', () => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('window-state-changed', { isMaximized: true });
    }
  });

  mainWindow.on('unmaximize', () => {
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.webContents.send('window-state-changed', { isMaximized: false });
    }
  });

  // Standard Windows close behavior: hide to system tray if background companion
  mainWindow.on('close', (e) => {
    if (!isQuitting) {
      e.preventDefault();
      mainWindow.hide();
      if (tray) {
        tray.displayBalloon({
          title: 'Beatz Phone Link',
          content: 'Running silently in system tray. Double-click to open dashboard.',
          iconType: 'info'
        });
      }
    }
  });

  mainWindow.webContents.on('did-finish-load', () => {
    // Immediately send cached state & window state upon UI launch
    mainWindow.webContents.send('service-state', latestState);
    mainWindow.webContents.send('window-state-changed', { isMaximized: mainWindow.isMaximized() });

    if (handshakeVerified) {
      mainWindow.webContents.send('handshake-verified', {
        verified: true,
        protocol: 'WebSocket Loopback (RFC 6455)',
        port: 28888,
        latencyMs: handshakeLatency,
        timestamp: new Date().toISOString()
      });
    }

    // Trigger update verification check 2.5s after launch
    setTimeout(() => {
      checkForUpdates(false);
    }, 2500);
  });
}

// ─────────────────────────────────────────────────────────────
// Dynamic System Tray
// ─────────────────────────────────────────────────────────────
function createSystemTray() {
  try {
    const icoPath = path.join(__dirname, 'assets', 'icon.ico');
    const fallbackPath = path.join(__dirname, 'assets', 'icon.png');
    const trayIconPath = fs.existsSync(icoPath) ? icoPath : fallbackPath;
    const trayIcon = nativeImage.createFromPath(trayIconPath);

    tray = new Tray(trayIcon);
    tray.setToolTip('Beatz Phone Link - Companion Service');

    updateTrayMenu();

    tray.on('click', () => {
      if (mainWindow) {
        if (mainWindow.isVisible()) {
          mainWindow.hide();
        } else {
          mainWindow.show();
          mainWindow.focus();
        }
      }
    });

    tray.on('double-click', () => {
      if (mainWindow) {
        mainWindow.show();
        mainWindow.focus();
      }
    });
  } catch (err) {
    console.warn('[Main] System tray initialization warning:', err.message);
  }
}

function updateTrayMenu() {
  if (!tray) return;

  try {
    const icoPath = path.join(__dirname, 'assets', 'icon.ico');
    if (fs.existsSync(icoPath)) {
      tray.setImage(nativeImage.createFromPath(icoPath));
    }

    const connText = latestState.isConnected
      ? `Device: ${latestState.deviceName} (${latestState.batteryPercentage}%${latestState.isCharging ? ' ⚡' : ''})`
      : 'Device: No mobile phone connected';

    const statusText = latestState.serviceRunning
      ? `Service: Active (8080 & 28888 • ${latestState.handshakeLatencyMs || '<1'}ms)`
      : 'Service: Offline';

    tray.setToolTip(`Beatz Phone Link\n${connText}\n${statusText}`);

    const contextMenu = Menu.buildFromTemplate([
      {
        label: 'Open Phone Link Dashboard',
        click: () => {
          if (mainWindow) {
            mainWindow.show();
            mainWindow.focus();
          }
        }
      },
      { type: 'separator' },
      {
        label: connText,
        enabled: false
      },
      {
        label: statusText,
        enabled: false
      },
      { type: 'separator' },
      {
        label: 'Lock PC',
        click: () => sendIpcCommand({ type: 'lock_pc' })
      },
      {
        label: 'Toggle Audio Mute',
        click: () => sendIpcCommand({ type: 'toggle_mute' })
      },
      { type: 'separator' },
      {
        label: 'Check for Updates...',
        click: () => checkForUpdates(true)
      },
      {
        label: 'Exit Beatz Phone Link',
        click: () => {
          isQuitting = true;
          app.quit();
        }
      }
    ]);

    tray.setContextMenu(contextMenu);
  } catch (err) {
    console.warn('[Main] Tray menu update warning:', err.message);
  }
}

// ─────────────────────────────────────────────────────────────
// App Lifecycle & IPC Registration
// ─────────────────────────────────────────────────────────────
app.whenReady().then(() => {
  createSystemTray();
  createWindow();
  ensureBackgroundServiceRunning();
});

app.on('before-quit', () => {
  isQuitting = true;
  if (wsClient) {
    try { wsClient.close(); } catch {}
  }
});

app.on('window-all-closed', (e) => {
  e.preventDefault();
});

// IPC handlers for renderer
ipcMain.handle('get-state', () => {
  return latestState;
});

ipcMain.handle('send-command', (event, cmd) => {
  return sendIpcCommand(cmd);
});

ipcMain.handle('check-for-updates', async () => {
  return await checkForUpdates(true);
});

ipcMain.handle('apply-update', () => {
  return applyDownloadedUpdate();
});

// Frameless Native Window Controls Handlers
ipcMain.on('window-minimize', () => {
  if (mainWindow) mainWindow.minimize();
});

ipcMain.on('window-maximize', () => {
  if (mainWindow) mainWindow.maximize();
});

ipcMain.on('window-unmaximize', () => {
  if (mainWindow) mainWindow.unmaximize();
});

ipcMain.on('window-toggle-maximize', () => {
  if (mainWindow) {
    if (mainWindow.isMaximized()) {
      mainWindow.unmaximize();
    } else {
      mainWindow.maximize();
    }
  }
});

ipcMain.handle('window-is-maximized', () => {
  return mainWindow ? mainWindow.isMaximized() : false;
});

ipcMain.on('window-hide', () => {
  if (mainWindow) mainWindow.hide();
});

ipcMain.on('window-close', () => {
  if (mainWindow) mainWindow.hide();
});

ipcMain.on('app-quit', () => {
  isQuitting = true;
  app.quit();
});
