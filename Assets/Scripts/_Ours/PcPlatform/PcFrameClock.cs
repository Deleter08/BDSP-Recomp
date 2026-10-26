#if !SWITCH
// PC platform layer: frame rates above the shipped 30 fps (author request, s37). NOT shipped code.
//
// The Switch game runs at 30 fps: Sequencer sets Application.targetFrameRate = 30 and the shipped QualitySettings vSyncCount is 2.
// Game code then treats "one Update" as "1/30 s" in three ways, each converted under `#if PC_UNLOCKED_FPS` (Scripting Define
// Symbols, Standalone) with the shipped line kept in the `#else` branch - delete the symbol and every game file compiles as shipped:
//   1. frame <-> second conversions through Application.targetFrameRate      -> PcFrameClock.LogicRate (30)
//   2. UIManager.Fps() = 60 / vSyncCount (= 30 shipped) as a speed unit      -> PcFrameClock.LogicRate
//   3. per-Update steps: counters (x++), waits (for n: yield return null), fixed per-frame motion (pos += 0.1f)
//        integer counters / frame waits  -> PcFrameClock.Ticks  (whole 30 Hz ticks elapsed this frame: 0, 1, 2 ...)
//        continuous float motion         -> PcFrameClock.Frames (fractional 30 Hz frames this frame = deltaTime * 30)
// With Time.captureFramerate = 30 (the play driver's Phase 6 runs) deltaTime is exactly 1/30, Frames = Ticks = 1 every frame and
// the game behaves exactly as shipped.
//
// The cap (PcFrameRate): <persistentDataPath>/pc_settings.ini, line `fps=<n>`; 0 or missing = uncapped (the author's default),
// 30 = the shipped pace. A play-driver run with Time.captureFramerate set (every Phase 6 comparison: play_capfps.txt = 30) runs at
// that pace; scripted runs without play_capfps.txt use the cap, so high-fps behaviour can be tested by script.
using System.Collections;
using System.IO;
using UnityEngine;

namespace PcPlatform
{
    public static class PcFrameClock
    {
        // The shipped logic rate: one shipped Update = 1/30 s.
        public const int LogicRate = 30;

        // Fractional 30 Hz frames elapsed this rendered frame (deltaTime is already clamped by Time.maximumDeltaTime, as every
        // deltaTime-based game system sees it).
        // Under Time.captureFramerate (Phase 6 runs) the value is the exact ratio, so captureFramerate = 30 gives exactly 1.0 and every
        // `x * Frames` conversion is bit-identical to the shipped `x` (deltaTime * 30 would be 0.99999994).
        public static float Frames
        {
            get
            {
                int capture = Time.captureFramerate;
                if (capture > 0)
                    return capture == LogicRate ? 1f : (float)LogicRate / capture;
                return Time.deltaTime * LogicRate;
            }
        }

        // 30 Hz frames in a deltaTime the caller was HANDED (a view system's OnUpdate(deltaTime) argument). Loops that simulate
        // frames inside one rendered frame pass 1/30, so each call is one frame. Within 1e-4 of 1 it returns exactly 1, so the
        // captureFramerate-30 deltaTime (0.0333333 -> 0.99999994) behaves as the shipped `+ 1`.
        public static float FramesOf(float deltaTime)
        {
            float f = deltaTime * LogicRate;
            return f > 0.9999f && f < 1.0001f ? 1f : f;
        }

        static int tickFrame = -1;
        static int ticks;
        static float accumulator;

        // Whole 30 Hz ticks elapsed this rendered frame, from one accumulator shared by every caller, so all counters of a frame
        // agree. The small epsilon makes 30 fps (deltaTime 0.0333333 -> 0.99999994 frames) give exactly 1 tick per frame.
        public static int Ticks
        {
            get
            {
                if (tickFrame != Time.frameCount)
                {
                    tickFrame = Time.frameCount;
                    accumulator += Frames;
                    ticks = Mathf.FloorToInt(accumulator + 0.001f);
                    accumulator -= ticks;
                    if (accumulator < 0f)
                        accumulator = 0f;
                }
                return ticks;
            }
        }
    }

    // Applies the frame-rate cap. Runs before every other script (order -32000), every frame, because the game's Sequencer writes
    // Application.targetFrameRate = 30 during startup.
    [DefaultExecutionOrder(-32000)]
    public class PcFrameRate : MonoBehaviour
    {
        // 0 = uncapped.
        public static int Cap { get; private set; }
        public static string SettingsPath { get { return Path.Combine(Application.persistentDataPath, "pc_settings.ini"); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Cap = ReadCap();
            VSync = ReadVSync();
            var go = new GameObject("PcFrameRate");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<PcFrameRate>();
#if PC_UNLOCKED_FPS
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
            timeBeginPeriod(1);   // 1 ms Sleep granularity for the limiter (Windows default 15.6 ms); reset by Windows at process exit
#endif
#if !UNITY_EDITOR && UNITY_STANDALONE_OSX && UNITY_6000_0_OR_NEWER
            MacFrameTimerFix();
#endif
            PcConfig.Log("frame rate: " + (Cap > 0 ? Cap + " fps" : "uncapped") + " (" + SettingsPath + ", fps=<n>, 0 = uncapped, 30 = shipped)");
#else
            PcConfig.Log("frame rate: shipped 30 fps (PC_UNLOCKED_FPS not defined)");
#endif
        }

        // s39: Options > Frame Rate (PcDisplayOptions). 0 = uncapped; Update() applies it from the next frame.
        public static void SetCap(int cap, bool save)
        {
            Cap = Mathf.Max(0, cap);
            PcConfig.Log("frame rate: " + (Cap > 0 ? Cap + " fps" : "uncapped") + (save ? " (saved to pc_settings.ini)" : ""));
            if (save)
                PcSettingsIni.Set(null, "fps=" + Cap);
        }

        static int ReadCap()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    File.WriteAllText(SettingsPath, "# PC frame-rate cap: fps=0 uncapped, fps=30 the shipped pace, any other value = that cap\nfps=0\n");
                    return 0;
                }
                foreach (var raw in File.ReadAllLines(SettingsPath))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("fps="))
                    {
                        int v;
                        if (int.TryParse(line.Substring(4).Trim(), out v) && v >= 0)
                            return v;
                    }
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning("[PcPlatform] pc_settings.ini: " + e.Message);
            }
            return 0;
        }

        // s39 (author: "The screen is tearing clearly while watching the opening video"): V-Sync, pc_settings.ini `vsync=1|0`,
        // default ON. The s37 pacing ran with vSyncCount 0 = frames swapped mid-scan = tearing (worst in exclusive full screen and on
        // panning video); the shipped game paces by vSync (vSyncCount 2). Options > V-Sync (PcDisplayOptions) sets it.
        public static bool VSync { get; private set; }

        public static void SetVSync(bool on, bool save)
        {
            VSync = on;
            PcConfig.Log("V-Sync: " + (on ? "on" : "off") + (save ? " (saved to pc_settings.ini)" : ""));
            if (save)
                PcSettingsIni.Set("# vsync: 1 = on (no tearing; the frame rate follows the monitor), 0 = off", "vsync=" + (on ? 1 : 0));
        }

        static bool ReadVSync()
        {
            string v = PcSettingsIni.Get("vsync");
            return v == null || v.Trim() != "0";
        }

        // V-Sync on: a cap the refresh rate divides (30 on 60 Hz, 60 on 120 / 240 Hz) becomes that whole swap interval (Unity allows
        // 1..4) = exact, tear-free pacing; any other cap swaps every refresh and the limiter holds the cap. Unlimited = every refresh.
        // (s39-s40 rounded every cap to the nearest interval: 120 on 144 Hz gave 144, 60 gave 72, 30 on 240 Hz gave 60.)
        static int SyncInterval(int cap, int refresh)
        {
            if (cap <= 0 || cap >= refresh)
                return 1;
            for (int n = 2; n <= 4; n++)
                if (Mathf.Abs((float)refresh / n - cap) <= 1f)
                    return n;
            return 1;
        }

#if !UNITY_EDITOR && UNITY_ANDROID
        static int androidMax;

        // Highest refresh rate of the current display (Screen.resolutions lists every mode Android reports, e.g. 60 / 90 / 120 on a Pixel 6 Pro).
        static int AndroidMaxRefresh()
        {
            if (androidMax > 0)
                return androidMax;
            double best = 60;
            try
            {
                best = System.Math.Max(best, Screen.currentResolution.refreshRateRatio.value);
                foreach (var r in Screen.resolutions)
                    best = System.Math.Max(best, r.refreshRateRatio.value);
            }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning("[PcPlatform] frame rate: refresh query failed: " + e.Message); }
            androidMax = (int)System.Math.Round(best);
            PcConfig.Log("frame rate: Android display max refresh " + androidMax + " Hz (current " + Screen.currentResolution.refreshRateRatio.value.ToString("F1") + " Hz): uncapped target = " + androidMax);
            return androidMax;
        }
#endif

        static int Refresh()
        {
            int refresh = Screen.currentResolution.refreshRate;
            return refresh > 0 ? refresh : 60;
        }

#if PC_UNLOCKED_FPS
#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
        [System.Runtime.InteropServices.DllImport("winmm.dll")]
        static extern uint timeBeginPeriod(uint period);
#endif

        // s-arm64 perf: true once Unity's own pacing is out of play - every cap is then this limiter's (V-Sync = vSyncCount 1 only).
        static bool limiterOnly;

#if !UNITY_EDITOR && UNITY_STANDALONE_OSX && UNITY_6000_0_OR_NEWER
        // The Unity 6000.6 macOS player ticks its PlayerLoop from an NSTimer at 1 / (refresh / vSyncCount) or 1 / targetFrameRate,
        // and that timer beats against the V-Sync present: 15-25 fps at 4K for a 4.5 ms GPU frame, the main thread 75 % idle (Metal
        // HUD, 2026-10-22; the 2019.4 x86_64 player: 60.0). PcMacFrameTimerFix (PcMacDialog.bundle) arms that timer at 0 instead -
        // Unity's own uncapped value - so the present paces V-Sync (60.0 fps, 0 missed V-Syncs) and the limiter every cap
        // (vSyncCount 2 and targetFrameRate both measured 10-20 fps under that timer). pc_settings.ini mac_frame_timer=unity: off.
        [System.Runtime.InteropServices.DllImport("PcMacDialog")]
        static extern int PcMacFrameTimerFix();

        static bool macTimerLogged;

        static void MacFrameTimerFix()
        {
            string mode = PcSettingsIni.Get("mac_frame_timer");
            if (mode != null && mode.Trim() == "unity")
            {
                if (!macTimerLogged)
                    PcConfig.Log("frame timer: Unity's (pc_settings.ini mac_frame_timer=unity)");
                macTimerLogged = true;
                return;
            }
            int r;
            try
            {
                r = PcMacFrameTimerFix();
            }
            catch (System.Exception e)
            {
                r = -2;
                UnityEngine.Debug.LogWarning("[PcPlatform] frame timer: " + e.GetType().Name + ": " + e.Message);
            }
            limiterOnly = r == 1;
            if (!macTimerLogged)
                PcConfig.Log("frame timer: " + (r == 1 ? "Unity 6 NSTimer at interval 0, the present / limiter pace the frames (pc_settings.ini mac_frame_timer=unity: off)"
                    : "Unity's (PcMacFrameTimerFix " + r + ": " + (r == 0 ? "no -[PlayerAppDelegate SetupPlayerTimer:]" : r == -1 ? "unexpected signature" : "plug-in missing") + ")"));
            macTimerLogged = true;
        }
#endif

        int loggedTarget = int.MinValue, loggedVSync = int.MinValue, loggedLimit = int.MinValue;
        int target, vsync, limit;

        void Update()
        {
            // s47: fast-forward needs speed x 30 rendered frames (PcGameSpeed's one-tick-per-frame law): a lower cap rises to it, and
            // V-Sync lets go while the monitor's refresh is below it
            int cap = Cap;
            bool vsyncOn = VSync;
            if (PcGameSpeed.Active)
            {
                int need = PcGameSpeed.Speed * PcFrameClock.LogicRate;
                if (cap > 0 && cap < need)
                    cap = need;
                if (vsyncOn && Refresh() < need)
                    vsyncOn = false;
            }
            if (Time.captureFramerate > 0)
            {
                target = PcFrameClock.LogicRate;   // play-driver runs: the exact shipped pace, as s37
                vsync = 0;
                limit = limiterOnly ? PcFrameClock.LogicRate : 0;
            }
            else if (vsyncOn)
            {
                target = -1;
                vsync = limiterOnly ? 1 : SyncInterval(cap, Refresh());
                limit = cap;
            }
            else
            {
                target = cap > 0 && !limiterOnly ? cap : -1;   // no V-Sync: Unity's own targetFrameRate limiter, or this one
                vsync = 0;
                limit = limiterOnly ? cap : 0;
            }
#if !UNITY_EDITOR && UNITY_IOS
            // iOS (2026-10-24): the display link (CADisplayLink) paces every frame and the player ignores vSyncCount, so V-Sync is
            // always on; targetFrameRate -1 means 30 fps there, not "every refresh". The rate is the display link's: the cap, or 60
            // when uncapped (the 120 Hz ProMotion rate only when a cap asks for it - Info.plist CADisableMinimumFrameDurationOnPhone,
            // set by the iOS build). Fast-forward has already raised the cap to speed x 30 above. No limiter: the display link waits.
            if (Time.captureFramerate <= 0)
            {
                target = cap > 0 ? cap : 60;
                vsync = 0;
                limit = 0;
            }
#endif
#if !UNITY_EDITOR && UNITY_ANDROID
            // Android: Unity's targetFrameRate -1 means 30 fps (not "every refresh") and the compositor (Choreographer) paces the swap whatever
            // vSyncCount says. Uncapped therefore = the display's highest refresh rate (never below 60); a user cap is honoured as is.
            // vSyncCount 0 and no limiter: Optimized Frame Pacing (player settings) spreads the frames. Fast-forward has already raised the cap.
            if (Time.captureFramerate <= 0)
            {
                target = cap > 0 ? cap : AndroidMaxRefresh();
                vsync = 0;
                limit = 0;
            }
#endif
            if (Application.targetFrameRate != target)
                Application.targetFrameRate = target;
            // (The editor ignores vSyncCount.) UIManager.Fps() no longer reads it under PC_UNLOCKED_FPS.
            if (QualitySettings.vSyncCount != vsync)
                QualitySettings.vSyncCount = vsync;
            if (target != loggedTarget || vsync != loggedVSync || limit != loggedLimit)
            {
                loggedTarget = target;
                loggedVSync = vsync;
                loggedLimit = limit;
                PcConfig.Log(string.Format("frame pacing: vSyncCount {0}, targetFrameRate {1}, limiter {2} (monitor {3} Hz, cap {4}, V-Sync {5}{6})",
                    vsync, target, limit > 0 ? limit.ToString() : "off", Screen.currentResolution.refreshRate,
                    Cap > 0 ? Cap.ToString() : "unlimited", VSync ? "on" : "off",
                    PcGameSpeed.Active ? ", fast-forward " + PcGameSpeed.Speed + "x" : ""));
                measureFrom = Now + 1.0;   // let the change settle, then measure
                measureFrames = 0;
            }
        }

        // s41 (a player: "180 FPS, V-Sync on" ran at 319 fps): V-Sync is not a cap. A driver set to "V-Sync off" (or a display path that
        // ignores the swap interval) leaves vSyncCount without effect, and Unity ignores targetFrameRate while vSyncCount > 0 - so
        // nothing limited the rate. With V-Sync on and a cap, this limiter waits at the end of the frame (before Present) until the
        // frame's deadline. 0.5 % slack: when V-Sync works at an exact interval the swap paces the frames and the limiter never waits.
        static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        static double Now { get { return clock.Elapsed.TotalSeconds; } }
        double deadline;

        IEnumerator Start()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                Limit();
                Measure();
            }
        }

        void Limit()
        {
            if (limit <= 0)
            {
                deadline = 0;
                return;
            }
            double period = 1.0 / (limit * 1.005);
            double now = Now;
            if (deadline == 0 || now > deadline + period)
                deadline = now;   // first frame or a hitch: start over, no catch-up burst
            else
            {
                while (now < deadline)
                {
                    double left = deadline - now;
                    System.Threading.Thread.Sleep(left > 0.002 ? (int)((left - 0.0015) * 1000) : 0);
                    now = Now;
                }
            }
            deadline += period;
        }

        // s41: the rate the screen really got, logged once ~1 s after every pacing change (over 2 s), so a player's Player.log shows it.
        double measureFrom = -1, measureStart;
        int measureFrames;

        void Measure()
        {
            if (measureFrom < 0)
                return;
            double now = Now;
            if (now < measureFrom)
                return;
            if (measureFrames++ == 0)
            {
                measureStart = now;
                return;
            }
            if (now - measureStart < 2.0)
                return;
            double fps = (measureFrames - 1) / (now - measureStart);
            measureFrom = -1;
            int refresh = Refresh();
            string note = vsync > 0 && fps > refresh / (double)vsync * 1.1
                ? " - V-SYNC NOT HONOURED (driver / display override)" + (limit > 0 ? "; the limiter holds the cap" : "; nothing caps Unlimited")
                : "";
            PcConfig.Log("frame pacing measured: " + fps.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " fps" + note);
        }
#endif
    }
}
#endif
