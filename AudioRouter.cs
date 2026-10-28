using System;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace MKei
{
    public class AudioRouter
    {
        private readonly MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

        /// <summary>Redirects the process's default audio output device to <paramref name="outputDeviceId"/> until changed again.</summary>
        public void RouteProcess(int processId, string outputDeviceId)
        {
            AudioPolicyConfig.SetAppDevice((uint)processId, outputDeviceId);
        }

        /// <summary>Clears the process's device override so it goes back to dynamically following the system default.</summary>
        public void RestoreProcess(int processId)
        {
            AudioPolicyConfig.ClearAppDevice((uint)processId);
        }

        /// <summary>
        /// True if the process currently has an active audio session on the given
        /// device - i.e. it's actually rendering there right now, not just
        /// nominally assigned there. Used to auto-detect "already on VB-Cable"
        /// state without requiring the user to have toggled it through this app.
        /// </summary>
        public bool IsProcessOnDevice(int processId, string deviceId)
        {
            var device = FindDeviceById(deviceId);
            if (device == null) return false;

            var sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    if (session.GetProcessID != (uint)processId)
                        continue;

                    // A session lingers here in the Inactive state for a while
                    // after audio actually stops flowing to this device - only
                    // Active means "actually playing here right now". Without
                    // this check, toggling off looked like it never took effect
                    // because the stale Inactive session kept matching.
                    if (session.State == AudioSessionState.AudioSessionStateActive)
                        return true;
                }
                catch
                {
                    // session may have gone away mid-enumeration; ignore and continue
                }
            }
            return false;
        }

        public MMDevice? FindDeviceById(string deviceId)
        {
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                if (device.ID == deviceId)
                    return device;
            }
            return null;
        }
    }
}