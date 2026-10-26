#if !SWITCH
// PC platform layer: scripted controller input for automated editor play runs (HANDOFF §11.14). NOT shipped code.
// The editor play driver (Assets/Editor/BdspAudit/BdspPlay.cs) loads a script with SetScript before Play mode starts; PcHid ORs the
// current step into the Pro Controller state every frame. Times are game-frame seconds (Time.unscaledTime: real seconds, or exactly
// 1/N per frame when the play driver sets Time.captureFramerate = N). One script drives BOTH sides of a Phase 6 comparison: a line
// starting with `ed:` is editor-only (the prefix is removed), one starting with `ryu:` is for the reference emulator's BdspAuto
// runner only (skipped here).
//
// Script, one command per line ('#' starts a comment):
//   wait <seconds>                         nothing pressed for that long
//   press <buttons> [<hold seconds=0.1>]   press, hold, release, then a 0.15 s gap; <buttons> = A, A+B, Down ... (NpadButton names)
//   hold <buttons> <seconds>               press and hold, no gap
//   stick <L|R> <x> <y> <seconds>          tilt a stick (-1..1, y up), then centre
//   waitscene <text> [<timeout s=120>]     wait until a loaded scene's path contains <text> (logs a timeout and goes on)
//   repeat <n> <buttons> [<gap s=0.4>]     press n times, <gap> apart (text advance)
//   log <text>                             Debug.Log a marker into the play log
//   shot <name>                            ScreenCapture.CaptureScreenshot(<name>.png) into the driver's folder. The capture happens at the END
//                                          of the frame: instant steps after it (probe, the start of a press, swkbd) run in the same frame
//                                          and their effect can already be in the picture - put a `wait` after a shot that must freeze a state
//   probe <GameObject name>                editor only: BdspUiProbe.Dump (Assets/Editor/BdspProbe) writes the object's UI layout;
//   probe ui:<path substring>              ... every active object on matching paths: Image / sprite / CanvasRenderer / TMP text / Animator
//   probe audio                            ... every Wwise object's playing IDs and play positions
//   swkbd <text ...>                       queue the next software-keyboard answer (PcSwkbd; the rest of the line, spaces kept)
//   swkbdcancel                            queue a canceled software keyboard
using System;
using System.Collections.Generic;
using System.Globalization;
using nn.hid;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PcPlatform
{
    public static class PcInput
    {
        class Step
        {
            public string op;
            public NpadButton buttons;
            public float seconds, x, y, gap;
            public int count;
            public bool right;
            public string text;
        }

        static readonly List<Step> steps = new List<Step>();
        static int index;
        static float stepStart = -1f;
        static string shotFolder = "";

        public static bool Active { get { return index < steps.Count; } }

        // True for the rest of the session once a script with steps was loaded: PcHid then ignores the physical keyboard / pad.
        public static bool Scripted { get; private set; }

#if !UNITY_EDITOR
        // A player driven by a script (setup/verify_mac_app.sh <app> <seconds> <script>): `-bdspInput <file>` on the command line, the
        // `shot` captures (the game's own frame, ScreenCapture) go next to the file. Without the argument nothing changes.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void FromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-bdspInput");
            if (i < 0 || i + 1 >= args.Length)
                return;
            string file = args[i + 1];
            if (!System.IO.File.Exists(file))
            {
                UnityEngine.Debug.LogWarning("[PcInput] -bdspInput: no file " + file);
                return;
            }
            SetScript(System.IO.File.ReadAllText(file), System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file)));
            PcConfig.Log("input script from the command line: " + file);
        }
#endif

        // Called by the editor play driver (reflection) before Play mode; `folder` receives the `shot` captures.
        public static void SetScript(string text, string folder)
        {
            steps.Clear();
            index = 0;
            stepStart = -1f;
            shotFolder = folder ?? "";
            var lines = (text ?? "").Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                int hash = line.IndexOf('#');
                if (hash >= 0)
                    line = line.Substring(0, hash);
                line = line.Trim();
                if (line.StartsWith("ryu:", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.StartsWith("ed:", StringComparison.OrdinalIgnoreCase))
                    line = line.Substring(3);
                var t = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length == 0)
                    continue;
                var s = new Step { op = t[0].ToLowerInvariant() };
                try
                {
                    switch (s.op)
                    {
                        case "wait": s.seconds = F(t[1]); break;
                        case "press": s.buttons = B(t[1]); s.seconds = t.Length > 2 ? F(t[2]) : 0.1f; break;
                        case "hold": s.buttons = B(t[1]); s.seconds = F(t[2]); break;
                        case "stick": s.right = t[1].ToUpperInvariant() == "R"; s.x = F(t[2]); s.y = F(t[3]); s.seconds = F(t[4]); break;
                        case "waitscene": s.text = t[1]; s.seconds = t.Length > 2 ? F(t[2]) : 120f; break;
                        case "repeat": s.count = int.Parse(t[1], CultureInfo.InvariantCulture); s.buttons = B(t[2]); s.gap = t.Length > 3 ? F(t[3]) : 0.4f; break;
                        case "log": s.text = line.Trim().Substring(3).Trim(); break;
                        case "shot": s.text = t[1]; break;
                        case "probe": s.text = t[1]; break;
                        case "swkbd": s.text = line.Trim().Substring(5).Trim(); break;
                        case "swkbdcancel": s.text = null; break;
                        default: throw new FormatException("unknown command");
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError("[PcInput] script line " + (n + 1) + " '" + lines[n].Trim() + "': " + e.Message);
                    continue;
                }
                steps.Add(s);
            }
            Scripted = steps.Count > 0;
            PcConfig.Log("input script: " + steps.Count + " steps");
        }

        static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

        static NpadButton B(string s)
        {
            NpadButton b = NpadButton.None;
            foreach (var part in s.Split('+'))
                b |= (NpadButton)Enum.Parse(typeof(NpadButton), part, true);
            return b;
        }

        // Called once per sampled frame by PcHid.
        public static void Apply(ref NpadButton buttons, ref int lx, ref int ly, ref int rx, ref int ry)
        {
            // Game-frame clock (s34, PM4): unscaledTime follows Time.captureFramerate when the play driver sets it, so steps land on the
            // same frames every run; without it, it tracks real time like realtimeSinceStartup did.
            float now = Time.unscaledTime;
            while (index < steps.Count)
            {
                var s = steps[index];
                if (stepStart < 0f)
                {
                    stepStart = now;
                    if (s.op == "log")
                        UnityEngine.Debug.Log("[PcInput] " + s.text);
                    else if (s.op == "shot")
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(shotFolder, s.text + ".png"));
                    else if (s.op == "probe")
                        Probe(s.text);
                    else if (s.op == "swkbd" || s.op == "swkbdcancel")
                        PcSwkbd.Enqueue(s.text);
                }
                float t = now - stepStart;
                bool done;
                switch (s.op)
                {
                    case "wait":
                        done = t >= s.seconds;
                        break;
                    case "press":
                        if (t < s.seconds)
                            buttons |= s.buttons;
                        done = t >= s.seconds + 0.15f;
                        break;
                    case "hold":
                        if (t < s.seconds)
                            buttons |= s.buttons;
                        done = t >= s.seconds;
                        break;
                    case "stick":
                        if (t < s.seconds)
                        {
                            int x = (int)(Mathf.Clamp(s.x, -1f, 1f) * 32767f), y = (int)(Mathf.Clamp(s.y, -1f, 1f) * 32767f);
                            if (s.right) { rx += x; ry += y; } else { lx += x; ly += y; }
                        }
                        done = t >= s.seconds;
                        break;
                    case "waitscene":
                        done = SceneLoaded(s.text);
                        if (!done && t >= s.seconds)
                        {
                            UnityEngine.Debug.LogWarning("[PcInput] waitscene '" + s.text + "' timed out after " + s.seconds + " s");
                            done = true;
                        }
                        break;
                    case "shot":
                        // s34: hold 0.1 s - an editor ScreenCapture in the same frame as the next step's input (e.g. X opening the menu)
                        // left the menu's message window drawn under the blur for the rest of the run (HANDOFF §11.15 law)
                        done = t >= 0.1f;
                        break;
                    case "repeat":
                        float period = 0.1f + s.gap;
                        int k = (int)(t / period);
                        if (k < s.count && t - k * period < 0.1f)
                            buttons |= s.buttons;
                        done = t >= s.count * period;
                        break;
                    default:
                        done = true;
                        break;
                }
                if (!done)
                    return;
                index++;
                stepStart = -1f;
                if (index == steps.Count)
                    UnityEngine.Debug.Log("[PcInput] script finished");
            }
        }

        // The editor tooling's UI dump, found by reflection (Assembly-CSharp-Editor); absent in a player build.
        static void Probe(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("BdspUiProbe");
                if (t == null)
                    continue;
                t.GetMethod("Dump").Invoke(null, new object[] { name, shotFolder });
                return;
            }
            UnityEngine.Debug.LogWarning("[PcInput] probe: BdspUiProbe not found");
        }

        static bool SceneLoaded(string text)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.isLoaded && sc.path.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }
}
#endif
