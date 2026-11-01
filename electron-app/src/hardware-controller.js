/**
 * Beatz Control — Native Hardware Controller for Windows
 * Handles local OS operations: Screen Lock, Firewall, Hotspot, Audio, and Input.
 */

const { exec, spawn } = require('child_process');
const os = require('os');

class HardwareController {
  constructor() {
    this.isWindows = os.platform() === 'win32';
  }

  /**
   * Immediately lock the Windows workstation.
   */
  lockScreen() {
    return new Promise((resolve, reject) => {
      if (!this.isWindows) {
        return resolve({ success: true, message: 'Simulated lock on non-Windows' });
      }
      exec('rundll32.exe user32.dll,LockWorkStation', (err) => {
        if (err) return reject(err);
        resolve({ success: true, message: 'Workstation locked' });
      });
    });
  }

  /**
   * Add Windows Defender Firewall rule for Beatz Control on port 8080.
   */
  addFirewallRule() {
    return new Promise((resolve) => {
      if (!this.isWindows) {
        return resolve({ success: true, message: 'Non-Windows platform' });
      }
      const cmd = 'netsh advfirewall firewall add rule name="Beatz Control" dir=in action=allow protocol=TCP localport=8080';
      exec(cmd, (err, stdout, stderr) => {
        if (err) {
          return resolve({ success: false, error: stderr || err.message });
        }
        resolve({ success: true, message: 'Firewall rule configured for TCP 8080' });
      });
    });
  }

  /**
   * Toggle HostedNetwork / SoftAP Hotspot on Windows.
   */
  toggleHotspot() {
    return new Promise((resolve) => {
      if (!this.isWindows) {
        return resolve({ success: true, message: 'Hotspot simulated' });
      }
      exec('netsh wlan start hostednetwork', (err, stdout, stderr) => {
        if (err) {
          // If hosted network isn't configured, return informative status
          return resolve({ success: false, message: stdout || stderr || 'Hotspot interface not available' });
        }
        resolve({ success: true, message: 'Hotspot activated' });
      });
    });
  }

  /**
   * Set Windows master audio volume percentage (0 - 100).
   */
  setVolume(percent) {
    return new Promise((resolve) => {
      if (!this.isWindows) return resolve({ success: true });
      const clamped = Math.max(0, Math.min(100, Math.round(percent)));
      // Set volume via PowerShell SndVol / WScript Media keys
      const psCommand = `
        $wsh = New-Object -ComObject WScript.Shell
        1..50 | ForEach-Object { $wsh.SendKeys([char]174) }
        $steps = [math]::Round(${clamped} / 2)
        1..$steps | ForEach-Object { $wsh.SendKeys([char]175) }
      `;
      exec(`powershell -NoProfile -Command "${psCommand.replace(/\n/g, ' ')}"`, () => {
        resolve({ success: true, level: clamped });
      });
    });
  }

  /**
   * Relative mouse movement.
   */
  moveMouse(dx, dy) {
    if (!this.isWindows) return;
    const ps = `
      Add-Type -AssemblyName System.Windows.Forms;
      $p = [System.Windows.Forms.Cursor]::Position;
      [System.Windows.Forms.Cursor]::Position = New-Object System.Drawing.Point(($p.X + ${dx}), ($p.Y + ${dy}))
    `;
    exec(`powershell -NoProfile -Command "${ps.replace(/\n/g, ' ')}"`, () => {});
  }

  /**
   * Mouse click emulation.
   */
  mouseClick(button = 'left', state = 'tap') {
    if (!this.isWindows) return;
    const flag = button.toLowerCase() === 'right' ? '0x08 | 0x10' : '0x02 | 0x04';
    const ps = `
      $sig = '[DllImport("user32.dll")] public static extern void mouse_event(int flags, int dx, int dy, int data, int extra);';
      $type = Add-Type -MemberDefinition $sig -Name 'User32Mouse' -Namespace 'Win32' -PassThru;
      $type::mouse_event(${flag}, 0, 0, 0, 0);
    `;
    exec(`powershell -NoProfile -Command "${ps.replace(/\n/g, ' ')}"`, () => {});
  }
}

module.exports = new HardwareController();

