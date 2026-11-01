using System;
using System.Runtime.InteropServices;

namespace PCControl.Core
{
    public class AudioEndpointController
    {
        #region Core Audio COM Interop

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int GetChannelCount(out uint pnChannelCount);
            [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
            [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        private static readonly Guid IID_IAudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

        #endregion

        private readonly NativeHardwareController _hardware;

        public AudioEndpointController(NativeHardwareController hardware)
        {
            _hardware = hardware;
        }

        private IAudioEndpointVolume? GetVolumeControl()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                // 0 = eRender, 1 = eMultimedia
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice dev);
                if (hr != 0 || dev == null) return null;

                Guid iid = IID_IAudioEndpointVolume;
                hr = dev.Activate(ref iid, 23, IntPtr.Zero, out object volumeObj); // CLSCTX_ALL = 23
                if (hr != 0) return null;

                return (IAudioEndpointVolume)volumeObj;
            }
            catch
            {
                return null;
            }
        }

        public int GetMasterVolumePercent()
        {
            try
            {
                var volume = GetVolumeControl();
                if (volume != null)
                {
                    volume.GetMasterVolumeLevelScalar(out float level);
                    return (int)Math.Round(level * 100f);
                }
            }
            catch { }
            return 50;
        }

        public void SetMasterVolumePercent(int percent)
        {
            try
            {
                percent = Math.Clamp(percent, 0, 100);
                float level = percent / 100.0f;
                var volume = GetVolumeControl();
                if (volume != null)
                {
                    Guid emptyGuid = Guid.Empty;
                    volume.SetMasterVolumeLevelScalar(level, ref emptyGuid);
                    return;
                }
            }
            catch { }

            // Fallback to media key pulses
            _hardware.KeyPress(0xAF); // VK_VOLUME_UP
        }

        public bool GetMute()
        {
            try
            {
                var volume = GetVolumeControl();
                if (volume != null)
                {
                    volume.GetMute(out bool mute);
                    return mute;
                }
            }
            catch { }
            return false;
        }

        public void SetMute(bool mute)
        {
            try
            {
                var volume = GetVolumeControl();
                if (volume != null)
                {
                    Guid emptyGuid = Guid.Empty;
                    volume.SetMute(mute, ref emptyGuid);
                    return;
                }
            }
            catch { }

            _hardware.KeyPress(0xAD); // VK_VOLUME_MUTE
        }

        public void ToggleMute()
        {
            SetMute(!GetMute());
        }

        public void PlayPause() => _hardware.KeyPress(0xB3); // VK_MEDIA_PLAY_PAUSE
        public void NextTrack() => _hardware.KeyPress(0xB0); // VK_MEDIA_NEXT_TRACK
        public void PreviousTrack() => _hardware.KeyPress(0xB1); // VK_MEDIA_PREV_TRACK
        public void VolumeUp() => _hardware.KeyPress(0xAF); // VK_VOLUME_UP
        public void VolumeDown() => _hardware.KeyPress(0xAE); // VK_VOLUME_DOWN
    }
}
