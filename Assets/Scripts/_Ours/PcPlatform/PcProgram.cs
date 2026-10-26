#if !SWITCH
// PC platform layer: process-level nn::oe services for the game natives (RestartUtility, SwitchNativeUtils). NOT shipped code.
using UnityEngine;

namespace PcPlatform
{
    public static class PcProgram
    {
        // nn::oe::SetExpectedVolumeBalance / GetExpectedVolumeBalance: the console mixes the application against system sounds; the
        // PC has no system mix, the pair is only kept for the getter.
        static float applicationVolume = 1.0f;
        static float sdkVolume = 1.0f;

        public static void SetExpectedVolumeBalance(float application, float sdk)
        {
            applicationVolume = application;
            sdkVolume = sdk;
        }

        public static void GetExpectedVolumeBalance(out float application, out float sdk)
        {
            application = applicationVolume;
            sdk = sdkVolume;
        }

        // nn::oe::RestartProgram (after nn::account::PushOpenUsers): the console relaunches the title. In the editor the program
        // ends: Play mode stops. A Windows player starts itself again with its own command line (PcProcessLaunch - CreateProcessW,
        // because IL2CPP 2019.4 does not implement Process.Start), then quits (s38b).
        public static void Restart()
        {
#if UNITY_EDITOR
            PcConfig.Log("RestartProgram requested - ending the program (Play mode stops; no relaunch in the editor)");
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_STANDALONE_WIN
            int error;
            int pid = PcProcessLaunch.Relaunch(out error);
            if (pid != 0)
                PcConfig.Log("RestartProgram requested - relaunched the player as process " + pid + ", quitting this one");
            else
                UnityEngine.Debug.LogWarning("[PcPlatform] RestartProgram: relaunch failed (Win32 error " + error + "), the player only quits");
            Application.Quit();
#else
            PcConfig.Log("RestartProgram requested - quitting (no relaunch on this platform)");
            Application.Quit();
#endif
        }
    }
}
#endif
