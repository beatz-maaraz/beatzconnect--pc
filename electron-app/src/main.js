/**
 * Beatz Control — Electron Main Process
 * Wraps the cloud relay client in a Phone Link styled Fluent interface.
 * Handles: window creation, smooth animations, dark/light themes, system tray,
 *          auto-launch, and IPC management.
 */

const {
  app,
  BrowserWindow,
  Tray,
  Menu,
  ipcMain,
  nativeImage,
  shell,
  dialog,
  nativeTheme,
} = require('electron');
const path = require('path');
const fs = require('fs');

// ── Environment flags ─────────────────────────────────────────────────────
const IS_DEV = process.argv.includes('--dev');
const IS_AUTOSTART = process.argv.includes('--autostart') ||
                     process.argv.includes('--minimized') ||
                     process.argv.includes('-silent');

// ── App single-instance lock ──────────────────────────────────────────────
const gotLock = app.requestSingleInstanceLock();
if (!gotLock) {
  app.quit();
  process.exit(0);
}

// ── State ─────────────────────────────────────────────────────────────────
let mainWindow = null;
let tray = null;
let isQuitting = false;

// ── Window creation ───────────────────────────────────────────────────────
function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1080,
    height: 720,
    minWidth: 860,
    minHeight: 560,
    title: 'Beatz Control',
    icon: path.join(__dirname, '..', 'assets', 'icon.ico'),
    frame: false,
    transparent: false,
    backgroundColor: nativeTheme.shouldUseDarkColors ? '#1C1C1C' : '#F3F3F3',
    show: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
      webSecurity: !IS_DEV,
    },
  });

  mainWindow.loadFile(path.join(__dirname, 'renderer', 'index.html'));

  mainWindow.once('ready-to-show', () => {
    if (!IS_AUTOSTART) {
      mainWindow.show();
      mainWindow.focus();
    }
  });

  // Intercept close -> minimize to tray if not quitting
  mainWindow.on('close', (e) => {
    if (!isQuitting) {
      e.preventDefault();
      mainWindow.hide();
      if (tray) {
        tray.displayBalloon({
          iconType: 'info',
          title: 'Beatz Control',
          content: 'Running in background — relay stays connected. Click tray icon to restore.',
        });
      }
    }
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });

  if (IS_DEV) mainWindow.webContents.openDevTools({ mode: 'detach' });
}

// ── System tray ───────────────────────────────────────────────────────────
function createTray() {
  const iconPath = path.join(__dirname, '..', 'assets', 'tray-icon.ico');
  const img = fs.existsSync(iconPath)
    ? nativeImage.createFromPath(iconPath)
    : nativeImage.createEmpty();

  tray = new Tray(img);
  tray.setToolTip('Beatz Control — relay active');

  const menu = Menu.buildFromTemplate([
    {
      label: 'Open Beatz Control',
      click: () => showWindow(),
    },
    { type: 'separator' },
    {
      label: 'Started silently at boot',
      enabled: false,
      visible: IS_AUTOSTART,
    },
    { type: 'separator' },
    {
      label: 'Quit',
      click: () => quitApp(),
    },
  ]);

  tray.setContextMenu(menu);
  tray.on('double-click', () => showWindow());

  if (IS_AUTOSTART) {
    tray.displayBalloon({
      iconType: 'info',
      title: 'Beatz Control',
      content: 'Started silently — auto-connecting to relay.',
    });
  }
}

function showWindow() {
  if (!mainWindow) {
    createWindow();
    return;
  }
  if (mainWindow.isMinimized()) mainWindow.restore();
  mainWindow.show();
  mainWindow.focus();
}

function quitApp() {
  isQuitting = true;
  app.quit();
}

// ── IPC Handlers: Auto-launch ─────────────────────────────────────────────
ipcMain.handle('set-auto-launch', (_e, enabled) => {
  app.setLoginItemSettings({
    openAtLogin: enabled,
    openAsHidden: true,
    args: ['--autostart', '--minimized'],
  });
  return app.getLoginItemSettings();
});

ipcMain.handle('get-auto-launch', () => app.getLoginItemSettings());

// ── IPC Handlers: Theme Setting ───────────────────────────────────────────
ipcMain.handle('theme-set', (_e, theme) => {
  if (['system', 'light', 'dark'].includes(theme)) {
    nativeTheme.themeSource = theme;
  }
  return {
    themeSource: nativeTheme.themeSource,
    shouldUseDarkColors: nativeTheme.shouldUseDarkColors,
  };
});

ipcMain.handle('theme-get', () => {
  return {
    themeSource: nativeTheme.themeSource,
    shouldUseDarkColors: nativeTheme.shouldUseDarkColors,
  };
});

// ── IPC Handlers: Window Chrome Actions ───────────────────────────────────
ipcMain.on('window-minimize', () => {
  if (mainWindow) {
    mainWindow.minimize();
  }
});

ipcMain.on('window-maximize', () => {
  if (!mainWindow) return;
  if (mainWindow.isMaximized()) {
    mainWindow.unmaximize();
  } else {
    mainWindow.maximize();
  }
});

ipcMain.on('window-close', () => {
  if (mainWindow) {
    mainWindow.close();
  }
});

ipcMain.on('window-hide', () => {
  if (mainWindow) {
    mainWindow.hide();
  }
});

function sendMaxState() {
  mainWindow?.webContents.send('window-max-changed', mainWindow.isMaximized());
}

app.on('browser-window-created', (_, win) => {
  win.on('maximize', sendMaxState);
  win.on('unmaximize', sendMaxState);
});

// ── App lifecycle ─────────────────────────────────────────────────────────
app.whenReady().then(() => {
  createTray();
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('second-instance', () => showWindow());

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin' && isQuitting) app.quit();
});

app.on('before-quit', () => {
  isQuitting = true;
});
