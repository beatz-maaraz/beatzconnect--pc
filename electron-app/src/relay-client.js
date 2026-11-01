/**
 * Beatz Control — Live Relay Client
 * Connects to the Socket.IO v4 Relay at https://backend-n4yg.onrender.com
 * Handles bidirectional registration, room allocation, commands, and telemetry.
 */

const { io } = require('socket.io-client');
const EventEmitter = require('events');
const os = require('os');
const hardware = require('./hardware-controller');

class RelayClient extends EventEmitter {
  constructor() {
    super();
    this.defaultUrl = 'https://backend-n4yg.onrender.com';
    this.currentUrl = this.defaultUrl;
    this.socket = null;
    this.isConnected = false;
    this.roomId = '';
    this.packetCount = 0;
    this.latency = 0;
    this.mobileCount = 0;
    this.pingTimer = null;

    // Stable machine identity
    const cleanHost = (os.hostname() || 'pc').toLowerCase().replace(/[^a-z0-9_-]/g, '_');
    this.pcId = `${cleanHost}_${os.userInfo()?.username || 'user'}`;
    this.pcName = os.hostname() || 'Windows PC';
  }

  /**
   * Connect to specified or default relay URL.
   */
  connect(targetUrl) {
    if (targetUrl) {
      this.currentUrl = targetUrl.trim();
    }

    if (this.socket) {
      this.disconnect();
    }

    this.emitLog(`Connecting to relay server → ${this.currentUrl}`);
    this.emitState('connecting', 'Establishing Socket.IO connection…');

    try {
      this.socket = io(this.currentUrl, {
        transports: ['websocket', 'polling'],
        reconnection: true,
        reconnectionAttempts: Infinity,
        reconnectionDelay: 2000,
        reconnectionDelayMax: 30000,
        timeout: 15000,
        extraHeaders: {
          'X-Client-Type': 'pc',
          'X-Client-Version': '1.23.50',
        },
      });

      this.setupSocketEvents();
    } catch (err) {
      this.emitLog(`Socket initialization error: ${err.message}`);
      this.emitState('error', err.message);
    }
  }

  setupSocketEvents() {
    if (!this.socket) return;

    this.socket.on('connect', () => {
      this.isConnected = true;
      this.emitLog(`Socket connected (ID: ${this.socket.id}). Registering PC…`);
      this.emitState('connected', 'Socket connected, waiting for room assignment…');

      // Register identity with the relay cluster
      const payload = {
        pcId: this.pcId,
        pcName: this.pcName,
        version: '1.23.50',
      };

      this.socket.emit('pc-register', payload);
      this.socket.emit('register_pc', payload);

      this.startPingTelemetry();
    });

    // Server confirmation of registration
    const handleRegistered = (data) => {
      if (data && typeof data === 'object') {
        this.roomId = data.roomId || data.pcId || this.pcId;
      } else if (typeof data === 'string') {
        this.roomId = data;
      } else {
        this.roomId = this.pcId;
      }

      this.emitLog(`Registered successfully ✓  Room ID: ${this.roomId}`);
      this.emitState('connected', 'Ready for mobile remote commands');
    };

    this.socket.on('registered', handleRegistered);
    this.socket.on('pc-registered', handleRegistered);

    // Mobile connection events
    this.socket.on('mobile_joined', (data) => {
      this.mobileCount++;
      const mId = data?.mobileId || 'Mobile device';
      this.emitLog(`Mobile controller joined: ${mId}`);
      this.emit('mobile-change', { joined: true, count: this.mobileCount, mobileId: mId });
    });

    this.socket.on('mobile_left', (data) => {
      this.mobileCount = Math.max(0, this.mobileCount - 1);
      const mId = data?.mobileId || 'Mobile device';
      this.emitLog(`Mobile controller left: ${mId}`);
      this.emit('mobile-change', { joined: false, count: this.mobileCount, mobileId: mId });
    });

    // Incoming Command Handlers
    this.socket.on('trackpad-move', (data) => {
      this.recordPacket();
      const dx = Math.round(Number(data?.dx || 0));
      const dy = Math.round(Number(data?.dy || 0));
      if (dx !== 0 || dy !== 0) {
        hardware.moveMouse(dx, dy);
      }
    });

    this.socket.on('mouse-click', (data) => {
      this.recordPacket();
      const btn = data?.button || 'left';
      const state = data?.state || 'tap';
      hardware.mouseClick(btn, state);
      this.emitLog(`Click ${btn} (${state})`);
    });

    this.socket.on('volume-change', (data) => {
      this.recordPacket();
      const level = data?.level ?? data?.percent ?? 50;
      hardware.setVolume(level);
      this.emitLog(`Volume set to ${level}%`);
    });

    this.socket.on('control-event', (data) => {
      this.recordPacket();
      const action = data?.action || data?.command || '';
      if (action.includes('LOCK')) {
        hardware.lockScreen();
        this.emitLog('Workstation lock commanded via relay');
      }
    });

    this.socket.on('command', (data) => {
      this.recordPacket();
      this.emitLog(`Command received: ${JSON.stringify(data)}`);
    });

    this.socket.on('ping', () => {
      this.recordPacket();
      this.socket.emit('pong');
    });

    this.socket.on('disconnect', (reason) => {
      this.isConnected = false;
      this.stopPingTelemetry();
      this.emitLog(`Disconnected from relay: ${reason}`);
      this.emitState('disconnected', `Disconnected (${reason})`);
    });

    this.socket.on('connect_error', (err) => {
      this.emitLog(`Connection warning: ${err.message}`);
      this.emitState('connecting', `Retrying… (${err.message})`);
    });
  }

  recordPacket() {
    this.packetCount++;
    this.emit('packet', {
      packetCount: this.packetCount,
      latency: this.latency,
    });
  }

  startPingTelemetry() {
    this.stopPingTelemetry();
    this.pingTimer = setInterval(() => {
      if (!this.socket || !this.isConnected) return;
      const start = Date.now();
      // Measure round-trip ping time
      this.socket.emit('ping', () => {
        this.latency = Math.max(1, Date.now() - start);
        this.emit('packet', {
          packetCount: this.packetCount,
          latency: this.latency,
        });
      });
    }, 4000);
  }

  stopPingTelemetry() {
    if (this.pingTimer) {
      clearInterval(this.pingTimer);
      this.pingTimer = null;
    }
  }

  disconnect() {
    this.stopPingTelemetry();
    if (this.socket) {
      this.socket.removeAllListeners();
      this.socket.disconnect();
      this.socket = null;
    }
    this.isConnected = false;
    this.emitState('disconnected', 'Disconnected by user');
  }

  emitLog(message) {
    this.emit('log', message);
  }

  emitState(status, detail) {
    this.emit('state-change', this.getState(status, detail));
  }

  getState(overrideStatus, overrideDetail) {
    return {
      isConnected: this.isConnected,
      status: overrideStatus || (this.isConnected ? 'connected' : 'disconnected'),
      detail: overrideDetail || (this.isConnected ? 'Ready for mobile commands' : 'Disconnected'),
      roomId: this.roomId,
      pcId: this.pcId,
      pcName: this.pcName,
      relayUrl: this.currentUrl,
      packetCount: this.packetCount,
      latency: this.latency,
      mobileCount: this.mobileCount,
    };
  }
}

module.exports = new RelayClient();

