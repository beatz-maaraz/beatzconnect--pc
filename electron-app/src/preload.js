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

  // Live Relay Server API
  connectRelay: (url) => ipcRenderer.invoke('relay-connect', url),
  disconnectRelay: () => ipcRenderer.invoke('relay-disconnect'),
  getRelayState: () => ipcRenderer.invoke('relay-get-state'),

  onRelayState: (cb) => {
    const handler = (_e, state) => cb(state);
    ipcRenderer.on('relay-state', handler);
    return () => ipcRenderer.removeListener('relay-state', handler);
  },

  onRelayPacket: (cb) => {
    const handler = (_e, data) => cb(data);
    ipcRenderer.on('relay-packet', handler);
    return () => ipcRenderer.removeListener('relay-packet', handler);
  },

  onRelayLog: (cb) => {
    const handler = (_e, msg) => cb(msg);
    ipcRenderer.on('relay-log', handler);
    return () => ipcRenderer.removeListener('relay-log', handler);
  },

  // Native Hardware Actions
  lockWorkstation: () => ipcRenderer.invoke('hardware-lock'),
  toggleHotspot: () => ipcRenderer.invoke('hardware-hotspot'),
  addFirewallRule: () => ipcRenderer.invoke('hardware-firewall'),
  setVolume: (level) => ipcRenderer.invoke('hardware-volume', level),

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
