/**
 * Beatz Control — Preload (Context Bridge)
 * Exposes a safe, minimal API surface to the renderer.
 */
const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('beatzAPI', {
  // Window chrome actions
  minimize: () => ipcRenderer.send('window-minimize'),
  maximize: () => ipcRenderer.send('window-maximize'),
  close: () => ipcRenderer.send('window-close'),
  hide: () => ipcRenderer.send('window-hide'),

  // Auto-launch configuration
  setAutoLaunch: (enabled) => ipcRenderer.invoke('set-auto-launch', enabled),
  getAutoLaunch: () => ipcRenderer.invoke('get-auto-launch'),

  // Theme configuration (Dark / Light / System)
  setTheme: (theme) => ipcRenderer.invoke('theme-set', theme),
  getTheme: () => ipcRenderer.invoke('theme-get'),

  // Events from main process -> renderer
  onMaxChanged: (cb) => {
    const handler = (_e, isMax) => cb(isMax);
    ipcRenderer.on('window-max-changed', handler);
    return () => ipcRenderer.removeListener('window-max-changed', handler);
  },

  // Runtime environment metadata
  isAutostart: process.argv?.includes('--autostart') || false,
  version: '1.23.50',
});
