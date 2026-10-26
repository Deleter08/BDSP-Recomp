#if !SWITCH
// PC platform layer: fast-forward (s47, players' request: "game speed (fast-forward 2x/4x and so forth)"). NOT shipped code.
//
// The Fast-Forward action (PcBindings: Tab / the pad's left-stick press by default; Options > Fast-Forward Key = Hold or Toggle) runs
// the game at Options > Fast-Forward Speed (2x-8x) through Time.timeScale. Shipped code never writes Time.timeScale, and under
// PC_UNLOCKED_FPS every per-frame step already follows deltaTime (PcFrameClock), so the whole game speeds up with it.
// LAW: one rendered frame never advances the game more than one shipped 30 Hz tick. Past that PcFrameClock.Ticks hands out 2+ ticks
// per frame - the regime of a PC running below 30 fps, which the unlocked-fps conversions were never tested in. So the governor sets
// timeScale = min(speed, measured fps / 30), and while fast-forward runs PcFrameRate raises a lower frame cap to speed x 30 and lifts
// V-Sync when the monitor's refresh is below that (tearing only while fast-forwarding; with V-Sync kept, a 60 Hz monitor would stop
// at 2x). A PC that cannot draw speed x 30 fps gets less than the chosen speed, as an emulator's turbo does; the corner badge shows
// the speed in effect.
// Held at 1x (RequireNormalSpeed, called every frame by): the contest (its sections run on the Wwise BGM play position), Poffin
// cooking (stick speed is measured per deltaTime) and movies (VideoPlayer runs on real time). Wwise audio is never sped up.
// Without PC_UNLOCKED_FPS (the shipped per-frame pace) the same governor holds and fast-forward also sets targetFrameRate = speed x 30
// with V-Sync off, so each frame stays one shipped frame. Play-driver runs (Time.captureFramerate set) never fast-forward.
// pc_settings.ini: `ff_speed=<2|3|4|6|8>`, `ff_key=toggle|hold`.
using System.Globalization;
using UnityEngine;

namespace PcPlatform
{
    [DefaultExecutionOrder(-31999)]
    public class PcGameSpeed : MonoBehaviour
    {
        public static readonly int[] Speeds = { 2, 3, 4, 6, 8 };
        public const int DefaultSpeed = 4;
        public const bool DefaultToggle = true;   // the pad's key is the left-stick press: holding it while steering with it is awkward

        // the fast-forward speed (Options > Fast-Forward Speed)
        public static int Speed { get; private set; }
        // true = the key toggles fast-forward, false = fast-forward while it is held (Options > Fast-Forward Key)
        public static bool ToggleMode { get; private set; }
        // fast-forward wanted: the key is held, or it was toggled on
        public static bool On { get; private set; }
        // fast-forward in effect: wanted, not held at 1x this frame, not a play-driver run
        public static bool Active { get; private set; }
        // the timeScale in effect
        public static float Scale { get; private set; }

        static int normalFrame = -10;
        static bool lastHeld;
        static float avgDt;
        static float written = 1f;
#if !PC_UNLOCKED_FPS
        static bool lockstep;
        static int savedTarget, savedVSync;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Speed = ReadSpeed();
            ToggleMode = ReadToggle();
            Scale = 1f;
            var go = new GameObject("PcGameSpeed");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<PcGameSpeed>();
            PcConfig.Log("fast-forward: " + Speed + "x, key " + (ToggleMode ? "toggles" : "held")
                + " (pc_settings.ini ff_speed / ff_key; Options > Fast-Forward Speed / Key)");
        }

        // Called every frame by a system that must run at 1x (it follows audio or video, or measures the player's input per second):
        // fast-forward pauses while the calls go on and resumes after.
        public static void RequireNormalSpeed()
        {
            normalFrame = Time.frameCount;
        }

        public static void SetSpeed(int speed, bool save)
        {
            Speed = System.Array.IndexOf(Speeds, speed) >= 0 ? speed : DefaultSpeed;
            PcConfig.Log("fast-forward speed: " + Speed + "x" + (save ? " (saved to pc_settings.ini)" : ""));
            if (save)
                PcSettingsIni.Set("# fast-forward: ff_speed = 2 | 3 | 4 | 6 | 8; ff_key = toggle | hold (Options > Fast-Forward Speed / Key)",
                    "ff_speed=" + Speed);
        }

        public static void SetToggleMode(bool toggle, bool save)
        {
            ToggleMode = toggle;
            PcConfig.Log("fast-forward key: " + (toggle ? "toggles" : "held") + (save ? " (saved to pc_settings.ini)" : ""));
            if (save)
                PcSettingsIni.Set("# fast-forward: ff_speed = 2 | 3 | 4 | 6 | 8; ff_key = toggle | hold (Options > Fast-Forward Speed / Key)",
                    "ff_key=" + (toggle ? "toggle" : "hold"));
        }

        static int ReadSpeed()
        {
            int v;
            string s = PcSettingsIni.Get("ff_speed");
            if (s != null && int.TryParse(s.Trim().TrimEnd('x', 'X'), NumberStyles.Integer, CultureInfo.InvariantCulture, out v)
                && System.Array.IndexOf(Speeds, v) >= 0)
                return v;
            return DefaultSpeed;
        }

        static bool ReadToggle()
        {
            string s = PcSettingsIni.Get("ff_key");
            if (s == null)
                return DefaultToggle;
            s = s.Trim().ToLowerInvariant();
            return s == "hold" ? false : s == "toggle" ? true : DefaultToggle;
        }

        void Update()
        {
            bool held = PcHid.FastForwardHeld;
            if (ToggleMode)
            {
                if (held && !lastHeld)
                    On = !On;
            }
            else
                On = held;
            lastHeld = held;

            bool driver = Time.captureFramerate > 0;
            bool active = On && !driver && normalFrame < Time.frameCount - 1;
            if (active != Active)
                PcConfig.Log("fast-forward " + (active ? "on (" + Speed + "x)" : On && !driver ? "held at 1x" : "off"));
            Active = active;

            // the governor: never more than one 30 Hz tick per rendered frame, never below 1x
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f)
                avgDt = avgDt <= 0f ? dt : avgDt + (dt - avgDt) * 0.2f;
            float scale = 1f;
            if (Active)
            {
                float allowed = avgDt > 0f ? 1f / (PcFrameClock.LogicRate * avgDt) : Speed;
                scale = Mathf.Clamp(allowed, 1f, Speed);
            }
#if !PC_UNLOCKED_FPS
            // the shipped pace: frames carry the game, so fast-forward needs speed x 30 of them (Sequencer set targetFrameRate 30 and
            // the shipped vSyncCount is 2; both come back when fast-forward ends)
            if (Active && !lockstep)
            {
                lockstep = true;
                savedTarget = Application.targetFrameRate;
                savedVSync = QualitySettings.vSyncCount;
            }
            if (lockstep)
            {
                if (Active)
                {
                    Application.targetFrameRate = Speed * PcFrameClock.LogicRate;
                    QualitySettings.vSyncCount = 0;
                }
                else
                {
                    lockstep = false;
                    Application.targetFrameRate = savedTarget;
                    QualitySettings.vSyncCount = savedVSync;
                }
            }
#endif
            // only our own changes are written: nothing else in the game sets timeScale, and nothing is overwritten while off
            if (scale != written)
            {
                Time.timeScale = scale;
                written = scale;
            }
            Scale = scale;
            PcGameSpeedBadge.Show(On && !driver, Active ? scale : 1f);
        }
    }

    // The corner badge while fast-forward is on: ">> 4x", the speed in effect when the PC holds it lower, ">> 1x" while held at 1x.
    // A uGUI overlay canvas, NOT OnGUI: with an OnGUI live, IMGUI takes the Tab key for its keyboard focus and Input.GetKey(Tab) - the
    // default Fast-Forward key - stopped seeing it (s47 run r1: the first Tab turned fast-forward on, the second never turned it off).
    public static class PcGameSpeedBadge
    {
        static GameObject root;
        static UnityEngine.UI.Text label;
        static int shownTenths = -1;

        public static void Show(bool on, float scale)
        {
            if (root == null)
            {
                if (!on)
                    return;
                Build();
            }
            if (root.activeSelf != on)
                root.SetActive(on);
            int tenths = Mathf.RoundToInt(scale * 10f);
            if (!on || tenths == shownTenths)
                return;
            shownTenths = tenths;
            label.text = ">> " + (tenths % 10 == 0 ? (tenths / 10).ToString(CultureInfo.InvariantCulture)
                : (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture)) + "x";
        }

        static void Build()
        {
            root = new GameObject("PcGameSpeedBadge");
            root.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;   // above every game canvas
            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var go = new GameObject("Text");
            go.transform.SetParent(root.transform, false);
            label = go.AddComponent<UnityEngine.UI.Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.fontSize = 40;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.UpperRight;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.color = new Color(1f, 1f, 1f, 0.92f);
            label.raycastTarget = false;
            var shadow = go.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
            shadow.effectDistance = new Vector2(2f, -2f);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-28f, -20f);
            rt.sizeDelta = new Vector2(400f, 60f);
        }
    }
}
#endif
