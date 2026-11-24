const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('pcControlApi', {
  // Query state immediately upon UI load
  getState: () => ipcRenderer.invoke('get-state'),

  // Listen for real-time state broadcasts from .NET service
  onStateUpdate: (callback) => {
    const handler = (event, state) => callback(state);
    ipcRenderer.on('service-state', handler);
    return () => ipcRenderer.removeListener('service-state', handler);
  },

  // Handshake verification event on launch
  onHandshakeVerified: (callback) => {
    const handler = (event, data) => callback(data);
    ipcRenderer.on('handshake-verified', handler);
    return () => ipcRenderer.removeListener('handshake-verified', handler);
  },

  // Listen for active apps list
  onAppsList: (callback) => {
    const handler = (event, apps) => callback(apps);
    ipcRenderer.on('apps-list', handler);
    return () => ipcRenderer.removeListener('apps-list', handler);
  },

  // Automated Version Control & Update Pipeline
  checkForUpdates: () => ipcRenderer.invoke('check-for-updates'),
  applyUpdate: () => ipcRenderer.invoke('apply-update'),
  onUpdateAvailable: (callback) => {
    const handler = (event, info) => callback(info);
    ipcRenderer.on('update-available', handler);
    return () => ipcRenderer.removeListener('update-available', handler);
  },
  onUpdateReady: (callback) => {
    const handler = (event, info) => callback(info);
    ipcRenderer.on('update-ready', handler);
    return () => ipcRenderer.removeListener('update-ready', handler);
  },

  // Send control commands (volume, power, simulation, etc.)
  sendCommand: (cmd) => ipcRenderer.invoke('send-command', cmd),

  // Standard Frameless Window Controls
  minimize: () => ipcRenderer.send('window-minimize'),
  maximize: () => ipcRenderer.send('window-maximize'),
  unmaximize: () => ipcRenderer.send('window-unmaximize'),
  toggleMaximize: () => ipcRenderer.send('window-toggle-maximize'),
  isMaximized: () => ipcRenderer.invoke('window-is-maximized'),
  onWindowStateChange: (callback) => {
    const handler = (event, state) => callback(state);
    ipcRenderer.on('window-state-changed', handler);
    return () => ipcRenderer.removeListener('window-state-changed', handler);
  },
  hideToTray: () => ipcRenderer.send('window-hide'),
  close: () => ipcRenderer.send('window-close'),
  quit: () => ipcRenderer.send('app-quit')
});
