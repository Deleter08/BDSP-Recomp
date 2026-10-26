#if !SWITCH
// PC platform layer: the controller diagnostic (players' pads mapped by hand). NOT shipped code.
//
// Started by the editor's "BDSP/Play/Play + controller diagnostic" (BdspPlay.PlayPadDiag, by reflection) or a player run with
// `-paddiag`. Writes to a text file (and the log, "[PadDiag]"), once: the pad path compiled in, Unity's joystick names, the
// GameController pads (macOS); then every CHANGE of
//   gc      the GameController buttons by position (South = Cross ...) and sticks / triggers (macOS, PcMacPad),
//   legacy  Unity's raw joysticks 1-4: buttons 0-19 and axes 1-12 (InputManager "PcDiag.J<n>.A<m>"),
//   switch  the emulated Pro Controller's buttons and sticks PcHid hands the game (what a press DOES).
// A stick / axis line is written when the value moves by 0.25 or more, so resting drift stays out.
using System;
using System.IO;
using System.Text;
using nn.hid;
using UnityEngine;

namespace PcPlatform
{
    public class PcPadDiag : MonoBehaviour
    {
        const int Joysticks = 4, Buttons = 20, Axes = 12;
        const float Step = 0.25f;

        static PcPadDiag instance;
        string path;
        readonly bool[,] legacyDown = new bool[Joysticks, Buttons];
        readonly float[,] legacyAxis = new float[Joysticks, Axes];
        bool axesOk = true, first = true;
        string joyNames = "";
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_IOS && !UNITY_EDITOR)
        PcMacPad.State gc;
        string gcDesc = "";
#endif
        [System.NonSerialized] NpadButton npad;   // a long enum: the player build's serializer check logs an error for it otherwise
        AnalogStickState sl, sr;
        bool fastForward;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FromCommandLine()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "-paddiag") < 0)
                return;
            Begin(Path.Combine(Application.persistentDataPath, "pad_diag.txt"));
        }

        // `file` receives the report (appended); called after Play mode / the player has started
        public static void Begin(string file)
        {
            if (instance != null)
                return;
            var go = new GameObject("PcPadDiag");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PcPadDiag>();
            instance.path = file;
            instance.Write("==== pad diagnostic " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ", unity " + Application.unityVersion
                + ", " + SystemInfo.operatingSystem + ", pad path: " + PadPath());
            instance.Write("press every button once, slowly, then move each stick and trigger fully; the file: " + file);
            Debug.Log("[PadDiag] writing " + file);
        }

        static string PadPath()
        {
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_IOS && !UNITY_EDITOR)
            return "macOS GameController (PcMacPad), legacy joystick 1 when it reads no pad";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return "XInput (PcXInput)";
#else
            return "legacy joystick 1";
#endif
        }

        void Write(string line)
        {
            Debug.Log("[PadDiag] " + line);
            try
            {
                File.AppendAllText(path, line + "\n");
            }
            catch (Exception)
            {
                // a diagnostic never breaks the game
            }
        }

        string T() { return string.Format("[{0,8:F2}] ", Time.unscaledTime); }

        void Update()
        {
            var sb = new StringBuilder();

            var names = string.Join(" | ", Array.ConvertAll(Input.GetJoystickNames(), n => "'" + n + "'"));
            if (names != joyNames || first)
            {
                joyNames = names;
                sb.AppendLine(T() + "legacy joysticks (1, 2, ...): " + (names.Length > 0 ? names : "(none)"));
            }

#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_IOS && !UNITY_EDITOR)
            PcMacPad.State s;
            if (PcMacPad.Raw(out s))
            {
                var desc = PcMacPad.Describe().Trim();
                if (desc != gcDesc || first)
                {
                    gcDesc = desc;
                    sb.AppendLine(T() + "gc controllers: " + (desc.Length > 0 ? desc.Replace("\n", "; ") : "(none)"));
                }
                uint changed = s.buttons ^ gc.buttons;
                for (int i = 0; i < 18; i++)
                    if ((changed & (1u << i)) != 0)
                        sb.AppendLine(T() + "gc " + ((s.buttons & (1u << i)) != 0 ? "press   " : "release ") + GcName(i));
                if (first || Moved(s.lx, gc.lx) || Moved(s.ly, gc.ly) || Moved(s.rx, gc.rx) || Moved(s.ry, gc.ry) || Moved(s.lt, gc.lt) || Moved(s.rt, gc.rt))
                    sb.AppendLine(T() + string.Format("gc sticks L({0:+0.00;-0.00},{1:+0.00;-0.00}) R({2:+0.00;-0.00},{3:+0.00;-0.00}) L2 {4:0.00} R2 {5:0.00}",
                        s.lx, s.ly, s.rx, s.ry, s.lt, s.rt));
                else
                {
                    s.lx = gc.lx; s.ly = gc.ly; s.rx = gc.rx; s.ry = gc.ry; s.lt = gc.lt; s.rt = gc.rt;   // compare with the last written
                }
                gc = s;
            }
#endif

            for (int j = 0; j < Joysticks; j++)
            {
                for (int b = 0; b < Buttons; b++)
                {
                    bool down = Input.GetKey(KeyCode.Joystick1Button0 + j * Buttons + b);
                    if (down != legacyDown[j, b])
                    {
                        legacyDown[j, b] = down;
                        sb.AppendLine(T() + "legacy joystick " + (j + 1) + " button " + b + (down ? " press" : " release"));
                    }
                }
                if (!axesOk)
                    continue;
                for (int a = 0; a < Axes; a++)
                {
                    float v;
                    try
                    {
                        v = Input.GetAxisRaw("PcDiag.J" + (j + 1) + ".A" + (a + 1));
                    }
                    catch (ArgumentException)
                    {
                        axesOk = false;
                        sb.AppendLine(T() + "legacy axes: the PcDiag.* axes are missing from InputManager.asset - axes not shown");
                        break;
                    }
                    if (first ? Mathf.Abs(v) > 0.05f : Moved(v, legacyAxis[j, a]))
                    {
                        sb.AppendLine(T() + string.Format("legacy joystick {0} axis {1} = {2:+0.00;-0.00}{3}", j + 1, a + 1, v, first ? " (at rest)" : ""));
                        legacyAxis[j, a] = v;
                    }
                }
            }

            long sampling;
            NpadButton b2;
            AnalogStickState l, r;
            NpadAttribute attr;
            PcHid.Read(NpadId.No1, NpadStyle.FullKey, out sampling, out b2, out l, out r, out attr);
            const NpadButton stickDirs = NpadButton.StickLLeft | NpadButton.StickLRight | NpadButton.StickLUp | NpadButton.StickLDown
                | NpadButton.StickRLeft | NpadButton.StickRRight | NpadButton.StickRUp | NpadButton.StickRDown;
            if ((b2 & ~stickDirs) != (npad & ~stickDirs))
                sb.AppendLine(T() + "switch buttons: " + ((b2 & ~stickDirs) == NpadButton.None ? "(none)" : (b2 & ~stickDirs).ToString()));
            if (Moved(l.x / 32767f, sl.x / 32767f) || Moved(l.y / 32767f, sl.y / 32767f) || Moved(r.x / 32767f, sr.x / 32767f) || Moved(r.y / 32767f, sr.y / 32767f))
            {
                sb.AppendLine(T() + string.Format("switch sticks L({0:+0.00;-0.00},{1:+0.00;-0.00}) R({2:+0.00;-0.00},{3:+0.00;-0.00})",
                    l.x / 32767f, l.y / 32767f, r.x / 32767f, r.y / 32767f));
                sl = l;
                sr = r;
            }
            npad = b2;
            bool ff = PcHid.FastForwardHeld;
            if (ff != fastForward)
            {
                fastForward = ff;
                sb.AppendLine(T() + "fast-forward " + (ff ? "held" : "released"));
            }

            first = false;
            if (sb.Length > 0)
                Write(sb.ToString().TrimEnd('\n'));
        }

        static bool Moved(float a, float b) { return Mathf.Abs(a - b) >= Step; }

        static string GcName(int bit)
        {
            switch (bit)
            {
                case 0: return "South (DualSense Cross)";
                case 1: return "East (Circle)";
                case 2: return "West (Square)";
                case 3: return "North (Triangle)";
                case 4: return "L1";
                case 5: return "R1";
                case 6: return "L2";
                case 7: return "R2";
                case 8: return "Create / Share";
                case 9: return "Options";
                case 10: return "L3";
                case 11: return "R3";
                case 12: return "D-pad Up";
                case 13: return "D-pad Down";
                case 14: return "D-pad Left";
                case 15: return "D-pad Right";
                case 16: return "PS / Home";
                case 17: return "Touchpad click";
            }
            return "bit " + bit;
        }
    }
}
#endif
