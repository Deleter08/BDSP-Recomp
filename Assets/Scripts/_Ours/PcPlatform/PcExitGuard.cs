#if !SWITCH
// PC platform layer: the Switch's "exit request handling section" for a PC player (s39). NOT shipped code.
//
// Shipped, SaveSystem.SaveAsync enters UnityEngine.Switch.Notification.EnterExitRequestHandlingSection() and OnPostSave leaves it: a
// system exit request (HOME -> close) is held back while the save file is written. A PC player closes through its window (X, Alt+F4)
// or Application.Quit; this holds that close back the same way (Application.wantsToQuit returns false while a save is in progress)
// and carries it out as soon as the save is done, so a quit never cuts a save file in half. SaveSystem's thread is a background
// thread on PC (an idle save loop must never keep the process alive - IL2CPP joins foreground threads at shutdown).
using UnityEngine;

namespace PcPlatform
{
    public static class PcExitGuard
    {
        static int depth;
        static bool quitRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Application.wantsToQuit += WantsToQuit;
        }

        static bool WantsToQuit()
        {
            if (depth <= 0)
                return true;
            if (!quitRequested)
                PcConfig.Log("exit: a save is being written - the game closes as soon as it is done");
            quitRequested = true;
            return false;
        }

        // SaveSystem.SaveAsync (main thread)
        public static void Enter()
        {
            depth++;
        }

        // SaveSystem.OnPostSave (posted back to the main thread)
        public static void Leave()
        {
            if (depth > 0)
                depth--;
            if (depth == 0 && quitRequested)
            {
                quitRequested = false;
                PcConfig.Log("exit: save written - closing");
                Application.Quit();
            }
        }
    }
}
#endif
