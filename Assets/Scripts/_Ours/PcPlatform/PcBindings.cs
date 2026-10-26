#if !SWITCH
// PC platform layer: the player's keyboard and game-pad bindings (s47, players' request: "change keyboard / XInput keybinds"). NOT
// shipped code - the console has no keyboard and maps its own controller.
//
// One table per device maps the emulated Pro Controller's inputs (PcAction) to physical inputs:
//   keyboard: up to two Unity KeyCodes per action - every Switch button, the d-pad, both sticks' four directions, Fast-Forward;
//   game pad: one control per action - every Switch button, the d-pad and Fast-Forward (the sticks stay the sticks).
// Defaults = the s34-s46 hard-coded tables of PcHid / PcXInput (Ryujinx's keyboard defaults; pads by POSITION) + Fast-Forward
// (PcGameSpeed) on Tab and on the pad's left-stick press. The game never reads the left-stick press (no StickL read beyond
// GameController building its mask, no raw 16 / 0x10 mask), so on the pad that control moves from L-stick press to Fast-Forward;
// the right-stick press stays (Poketch next app, Underground big map, Amity Square camera reset).
// pc_settings.ini: `key.<action>=<KeyCode>[,<KeyCode>]` (Unity KeyCode names, empty = no key) and `pad.<action>=<control>`; a missing
// or unreadable line keeps that action's default, so an ini written before s47 plays exactly as before.
// Options > Keyboard Keys / Controller Buttons (PcControlsOptions) edit a pending copy, made live by the shipped "save these
// settings?" yes. Lock-out rules: the menu actions (A, B, X, Y, +, d-pad) always keep a key and a pad control, and a key / control
// taken from another action swaps with the edited one.
using System;
using System.Collections.Generic;
using System.Linq;
using nn.hid;
using UnityEngine;

namespace PcPlatform
{
    public enum PcAction
    {
        A, B, X, Y, L, R, ZL, ZR, Plus, Minus, LStickPress, RStickPress,
        DpadUp, DpadDown, DpadLeft, DpadRight,
        LStickUp, LStickDown, LStickLeft, LStickRight,
        RStickUp, RStickDown, RStickLeft, RStickRight,
        FastForward,
    }

    // XInput's controls (off Windows: Unity's legacy joystick 1 with the same Xbox layout). The bit of a control in a control set
    // is 1 << (int)control; None has no bit.
    public enum PadControl
    {
        None, A, B, X, Y, LB, RB, LT, RT, Back, Start, LS, RS, DpadUp, DpadDown, DpadLeft, DpadRight,
    }

    public class PcBindingTable
    {
        public const int Actions = (int)PcAction.FastForward + 1;
        public readonly KeyCode[,] keys = new KeyCode[Actions, 2];
        public readonly PadControl[] pad = new PadControl[Actions];

        public PcBindingTable Clone()
        {
            var t = new PcBindingTable();
            t.CopyFrom(this);
            return t;
        }

        public void CopyFrom(PcBindingTable other)
        {
            Array.Copy(other.keys, keys, keys.Length);
            Array.Copy(other.pad, pad, pad.Length);
        }

        public bool SameKeys(PcBindingTable other)
        {
            for (int a = 0; a < Actions; a++)
                if (keys[a, 0] != other.keys[a, 0] || keys[a, 1] != other.keys[a, 1])
                    return false;
            return true;
        }

        public bool SamePad(PcBindingTable other)
        {
            for (int a = 0; a < Actions; a++)
                if (pad[a] != other.pad[a])
                    return false;
            return true;
        }

        public void DefaultKeys()
        {
            for (int a = 0; a < Actions; a++)
            {
                keys[a, 0] = PcBindings.DefaultKeys[a][0];
                keys[a, 1] = PcBindings.DefaultKeys[a].Length > 1 ? PcBindings.DefaultKeys[a][1] : KeyCode.None;
            }
        }

        public void DefaultPad()
        {
            Array.Copy(PcBindings.DefaultPad, pad, pad.Length);
        }

        public bool HasKey(PcAction a)
        {
            return keys[(int)a, 0] != KeyCode.None || keys[(int)a, 1] != KeyCode.None;
        }
    }

    public static class PcBindings
    {
        // The default keyboard (Ryujinx's defaults, s34) - one or two keys per action.
        internal static readonly KeyCode[][] DefaultKeys =
        {
            new[] { KeyCode.Z }, new[] { KeyCode.X }, new[] { KeyCode.C }, new[] { KeyCode.V },                     // A B X Y
            new[] { KeyCode.E }, new[] { KeyCode.U }, new[] { KeyCode.Q }, new[] { KeyCode.O },                     // L R ZL ZR
            new[] { KeyCode.Equals, KeyCode.KeypadPlus }, new[] { KeyCode.Minus, KeyCode.KeypadMinus },             // + -
            new[] { KeyCode.F }, new[] { KeyCode.H },                                                               // stick presses
            new[] { KeyCode.UpArrow }, new[] { KeyCode.DownArrow }, new[] { KeyCode.LeftArrow }, new[] { KeyCode.RightArrow },
            new[] { KeyCode.W }, new[] { KeyCode.S }, new[] { KeyCode.A }, new[] { KeyCode.D },                     // left stick
            new[] { KeyCode.I }, new[] { KeyCode.K }, new[] { KeyCode.J }, new[] { KeyCode.L },                     // right stick
            new[] { KeyCode.Tab },                                                                                  // Fast-Forward
        };

        // The default pad: by POSITION as Ryujinx maps it (Switch A = pad B, B = A, X = Y, Y = X), s41.
        internal static readonly PadControl[] DefaultPad =
        {
            PadControl.B, PadControl.A, PadControl.Y, PadControl.X,
            PadControl.LB, PadControl.RB, PadControl.LT, PadControl.RT,
            PadControl.Start, PadControl.Back, PadControl.None, PadControl.RS,
            PadControl.DpadUp, PadControl.DpadDown, PadControl.DpadLeft, PadControl.DpadRight,
            PadControl.None, PadControl.None, PadControl.None, PadControl.None,
            PadControl.None, PadControl.None, PadControl.None, PadControl.None,
            PadControl.LS,
        };

        // The four face buttons of the two pad layouts (Options > Button Layout): by position (default) or by printed letter.
        static readonly PadControl[] FacePositions = { PadControl.B, PadControl.A, PadControl.Y, PadControl.X };
        static readonly PadControl[] FaceLabels = { PadControl.A, PadControl.B, PadControl.X, PadControl.Y };

        static readonly NpadButton[] NpadOf =
        {
            NpadButton.A, NpadButton.B, NpadButton.X, NpadButton.Y, NpadButton.L, NpadButton.R, NpadButton.ZL, NpadButton.ZR,
            NpadButton.Plus, NpadButton.Minus, NpadButton.StickL, NpadButton.StickR,
            NpadButton.Up, NpadButton.Down, NpadButton.Left, NpadButton.Right,
        };

        const int StickMax = 32767;

        static PcBindingTable live;

        // loaded on first use: the game may sample input before Install has run
        public static PcBindingTable Live
        {
            get
            {
                if (live == null)
                    live = Load();
                return live;
            }
        }

        public static int Bit(PadControl c) { return c == PadControl.None ? 0 : 1 << (int)c; }

        // actions a game pad can carry (not the stick directions - the sticks stay the sticks)
        public static bool OnPad(PcAction a) { return a < PcAction.LStickUp || a == PcAction.FastForward; }

        // the menu actions: never left without a key / pad control (no lock-out)
        public static bool Required(PcAction a)
        {
            return a == PcAction.A || a == PcAction.B || a == PcAction.X || a == PcAction.Y || a == PcAction.Plus
                || (a >= PcAction.DpadUp && a <= PcAction.DpadRight);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            PcConfig.Log("bindings: keyboard " + (Live.SameKeys(Defaults()) ? "default" : "custom") + ", pad " + DescribePad(Live)
                + " (pc_settings.ini key.* / pad.*; Options > Keyboard Keys / Controller Buttons)");
        }

        public static PcBindingTable Defaults()
        {
            var t = new PcBindingTable();
            t.DefaultKeys();
            t.DefaultPad();
            return t;
        }

        static string DescribePad(PcBindingTable t)
        {
            if (t.SamePad(Defaults()))
                return "default (Switch positions)";
            return Layout(t) == PadLayout.Labels ? "Xbox labels" : "custom";
        }

        // ---- the per-frame mapping (PcHid) ------------------------------------------------------------------------------------

        // keyboard -> buttons, stick deflection, Fast-Forward
        public static void ReadKeyboard(ref NpadButton b, ref int lx, ref int ly, ref int rx, ref int ry, ref bool ff)
        {
            var t = Live;
            for (int a = 0; a < PcBindingTable.Actions; a++)
            {
                KeyCode k0 = t.keys[a, 0], k1 = t.keys[a, 1];
                if (!((k0 != KeyCode.None && Input.GetKey(k0)) || (k1 != KeyCode.None && Input.GetKey(k1))))
                    continue;
                if (a < NpadOf.Length)
                    b |= NpadOf[a];
                else switch ((PcAction)a)
                {
                    case PcAction.LStickUp: ly += StickMax; break;
                    case PcAction.LStickDown: ly -= StickMax; break;
                    case PcAction.LStickLeft: lx -= StickMax; break;
                    case PcAction.LStickRight: lx += StickMax; break;
                    case PcAction.RStickUp: ry += StickMax; break;
                    case PcAction.RStickDown: ry -= StickMax; break;
                    case PcAction.RStickLeft: rx -= StickMax; break;
                    case PcAction.RStickRight: rx += StickMax; break;
                    case PcAction.FastForward: ff = true; break;
                }
            }
        }

        // a pad control set (PcXInput) -> buttons, Fast-Forward
        public static void ApplyPad(int controls, ref NpadButton b, ref bool ff)
        {
            if (controls == 0)
                return;
            var t = Live;
            for (int a = 0; a < NpadOf.Length; a++)
                if ((controls & Bit(t.pad[a])) != 0)
                    b |= NpadOf[a];
            if ((controls & Bit(t.pad[(int)PcAction.FastForward])) != 0)
                ff = true;
        }

        // the Switch buttons a pad control set presses (PcSwkbdNative's modal dialog)
        public static NpadButton PadButtons(int controls)
        {
            NpadButton b = NpadButton.None;
            bool ff = false;
            ApplyPad(controls, ref b, ref ff);
            return b;
        }

        // ---- the two pad layouts (Options > Button Layout) --------------------------------------------------------------------

        public enum PadLayout { Positions, Labels, Custom }

        public static PadLayout Layout(PcBindingTable t)
        {
            bool positions = true, labels = true;
            for (int i = 0; i < 4; i++)
            {
                positions &= t.pad[i] == FacePositions[i];
                labels &= t.pad[i] == FaceLabels[i];
            }
            return positions ? PadLayout.Positions : labels ? PadLayout.Labels : PadLayout.Custom;
        }

        // puts the layout's face buttons on A / B / X / Y; a control they take from another action swaps with it
        public static void SetLayout(PcBindingTable t, PadLayout layout)
        {
            var face = layout == PadLayout.Labels ? FaceLabels : FacePositions;
            for (int i = 0; i < 4; i++)
                AssignPad(t, (PcAction)i, face[i], false);
        }

        // ---- edits (the controls pages) ---------------------------------------------------------------------------------------

        // Puts `key` into `action`'s `slot`. A key held by another action swaps with the slot's old key; refused (false, `owner` =
        // that action) when the swap would leave a menu action without any key.
        public static bool AssignKey(PcBindingTable t, PcAction action, int slot, KeyCode key, out PcAction owner)
        {
            owner = action;
            int a = (int)action;
            KeyCode old = t.keys[a, slot];
            if (old == key)
                return true;
            for (int o = 0; o < PcBindingTable.Actions; o++)
                for (int s = 0; s < 2; s++)
                {
                    if (t.keys[o, s] != key || (o == a && s == slot))
                        continue;
                    if (o != a && old == KeyCode.None && Required((PcAction)o) && t.keys[o, 1 - s] == KeyCode.None)
                    {
                        owner = (PcAction)o;
                        return false;
                    }
                    t.keys[o, s] = old;
                }
            t.keys[a, slot] = key;
            for (int o = 0; o < PcBindingTable.Actions; o++)
                Compact(t, o);
            return true;
        }

        // Clears `action`'s `slot`; refused for a menu action's last key.
        public static bool ClearKey(PcBindingTable t, PcAction action, int slot)
        {
            int a = (int)action;
            if (Required(action) && t.keys[a, 1 - slot] == KeyCode.None)
                return false;
            t.keys[a, slot] = KeyCode.None;
            Compact(t, a);
            return true;
        }

        // a lone second key moves to the first slot
        static void Compact(PcBindingTable t, int a)
        {
            if (t.keys[a, 0] == KeyCode.None && t.keys[a, 1] != KeyCode.None)
            {
                t.keys[a, 0] = t.keys[a, 1];
                t.keys[a, 1] = KeyCode.None;
            }
        }

        // Puts `control` on `action`; the action holding it gets `action`'s old control. With `check`, refused (false, `owner`) when
        // that would leave a menu action without a control.
        public static bool AssignPad(PcBindingTable t, PcAction action, PadControl control, bool check)
        {
            PcAction owner;
            return AssignPad(t, action, control, check, out owner);
        }

        public static bool AssignPad(PcBindingTable t, PcAction action, PadControl control, bool check, out PcAction owner)
        {
            owner = action;
            int a = (int)action;
            PadControl old = t.pad[a];
            if (old == control)
                return true;
            for (int o = 0; o < PcBindingTable.Actions; o++)
            {
                if (o == a || t.pad[o] != control || control == PadControl.None)
                    continue;
                if (check && old == PadControl.None && Required((PcAction)o))
                {
                    owner = (PcAction)o;
                    return false;
                }
                t.pad[o] = old;
            }
            t.pad[a] = control;
            return true;
        }

        public static bool ClearPad(PcBindingTable t, PcAction action)
        {
            if (Required(action))
                return false;
            t.pad[(int)action] = PadControl.None;
            return true;
        }

        // ---- pc_settings.ini --------------------------------------------------------------------------------------------------

        static PcBindingTable Load()
        {
            var t = Defaults();
            var ini = PcSettingsIni.ReadAll();
            int bad = 0;
            for (int a = 0; a < PcBindingTable.Actions; a++)
            {
                string name = ((PcAction)a).ToString(), v;
                if (ini.TryGetValue("key." + name, out v))
                {
                    var parsed = new List<KeyCode>();
                    bool ok = true;
                    foreach (var part in v.Split(','))
                    {
                        var p = part.Trim();
                        if (p.Length == 0)
                            continue;
                        KeyCode k;
                        if (TryParseKey(p, out k))
                            parsed.Add(k);
                        else
                            ok = false;
                    }
                    if (ok && parsed.Count <= 2 && (parsed.Count > 0 || !Required((PcAction)a)))
                    {
                        t.keys[a, 0] = parsed.Count > 0 ? parsed[0] : KeyCode.None;
                        t.keys[a, 1] = parsed.Count > 1 ? parsed[1] : KeyCode.None;
                    }
                    else
                        bad++;
                }
                if (OnPad((PcAction)a) && ini.TryGetValue("pad." + name, out v))
                {
                    PadControl c;
                    if (TryParsePad(v.Trim(), out c) && (c != PadControl.None || !Required((PcAction)a)))
                        t.pad[a] = c;
                    else
                        bad++;
                }
            }
            if (bad > 0)
                PcConfig.Log("bindings: " + bad + " unreadable pc_settings.ini key.* / pad.* line(s) - those actions keep their defaults");
            return t;
        }

        static bool TryParseKey(string s, out KeyCode k)
        {
            k = KeyCode.None;
            int dummy;
            if (int.TryParse(s, out dummy))
                return false;   // Enum.Parse would take a bare number
            try
            {
                k = (KeyCode)Enum.Parse(typeof(KeyCode), s, true);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return Array.IndexOf(CaptureKeys, k) >= 0;
        }

        static bool TryParsePad(string s, out PadControl c)
        {
            c = PadControl.None;
            int dummy;
            if (int.TryParse(s, out dummy))
                return false;
            try
            {
                c = (PadControl)Enum.Parse(typeof(PadControl), s, true);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }

        // the live table becomes `t`; `save` writes it to pc_settings.ini
        public static void Apply(PcBindingTable t, bool save)
        {
            bool keysChanged = !Live.SameKeys(t), padChanged = !Live.SamePad(t);
            live = t.Clone();
            if (!save || (!keysChanged && !padChanged))
                return;
            var lines = new List<string>();
            for (int a = 0; a < PcBindingTable.Actions; a++)
            {
                string name = ((PcAction)a).ToString();
                var keys = new[] { t.keys[a, 0], t.keys[a, 1] }.Where(k => k != KeyCode.None).Select(k => k.ToString());
                lines.Add("key." + name + "=" + string.Join(",", keys.ToArray()));
            }
            for (int a = 0; a < PcBindingTable.Actions; a++)
                if (OnPad((PcAction)a))
                    lines.Add("pad." + ((PcAction)a) + "=" + t.pad[a]);
            PcSettingsIni.Set("# controls (Options > Keyboard Keys / Controller Buttons): key.<action>=<Unity KeyCode>[,<KeyCode>] (empty = none),"
                + " pad.<action>=A|B|X|Y|LB|RB|LT|RT|Back|Start|LS|RS|DpadUp|DpadDown|DpadLeft|DpadRight|None; a missing line = the default",
                lines.ToArray());
            PcConfig.Log("bindings saved to pc_settings.ini: keyboard " + (t.SameKeys(Defaults()) ? "default" : "custom") + ", pad " + DescribePad(t));
        }

        // ---- capture (a controls page waits for the key / button to bind) -----------------------------------------------------

        public enum CaptureDevice { Keyboard, Pad }
        enum CaptureState { Idle, WaitRelease, Listen, AfterRelease }

        public const float CaptureTimeout = 5f;

        // every keyboard key a binding may use: Unity's keyboard KeyCodes (below the mouse buttons), minus Escape (cancels a capture)
        static readonly KeyCode[] CaptureKeys = ((KeyCode[])Enum.GetValues(typeof(KeyCode)))
            .Where(k => k > KeyCode.None && k < KeyCode.Mouse0 && k != KeyCode.Escape).Distinct().ToArray();

        static CaptureState state;
        static CaptureDevice device;
        static float listenStart;
        static int lastControls;
        static bool resultReady;
        static KeyCode resultKey;
        static PadControl resultPad;

        // true from BeginCapture until every key and pad control is up again after the capture: the game gets no physical input
        public static bool Capturing { get { return state != CaptureState.Idle; } }

        // true while waiting for the key / button (the controls page shows the prompt)
        public static bool Listening { get { return state == CaptureState.Listen || state == CaptureState.WaitRelease; } }

        public static float ListenSecondsLeft { get { return state == CaptureState.Listen ? Mathf.Max(0f, CaptureTimeout - (Time.unscaledTime - listenStart)) : CaptureTimeout; } }

        public static void BeginCapture(CaptureDevice d)
        {
            device = d;
            state = CaptureState.WaitRelease;   // the press that started the capture must come up first
            resultReady = false;
            lastControls = -1;
        }

        // the captured key / control once (KeyCode.None and PadControl.None = canceled); false until the capture ends
        public static bool TakeResult(out KeyCode key, out PadControl control)
        {
            key = resultKey;
            control = resultPad;
            if (!resultReady)
                return false;
            resultReady = false;
            return true;
        }

        // PcHid, once per sampled frame, with the pads' control set: runs the capture; true = withhold the physical input
        internal static bool CaptureFrame(int controls)
        {
            if (state == CaptureState.Idle)
                return false;
            bool anyDown = controls != 0 || Input.GetKey(KeyCode.Escape) || CaptureKeys.Any(k => Input.GetKey(k));
            switch (state)
            {
                case CaptureState.WaitRelease:
                    if (!anyDown)
                    {
                        state = CaptureState.Listen;
                        listenStart = Time.unscaledTime;
                    }
                    break;
                case CaptureState.Listen:
                    if (Input.GetKeyDown(KeyCode.Escape) || Time.unscaledTime - listenStart > CaptureTimeout)
                        Finish(KeyCode.None, PadControl.None);
                    else if (device == CaptureDevice.Keyboard)
                    {
                        foreach (var k in CaptureKeys)
                            if (Input.GetKeyDown(k))
                            {
                                Finish(k, PadControl.None);
                                break;
                            }
                    }
                    else
                    {
                        int pressed = lastControls < 0 ? 0 : controls & ~lastControls;
                        for (int c = 1; c <= (int)PadControl.DpadRight; c++)
                            if ((pressed & (1 << c)) != 0)
                            {
                                Finish(KeyCode.None, (PadControl)c);
                                break;
                            }
                    }
                    break;
                case CaptureState.AfterRelease:
                    if (!anyDown)
                        state = CaptureState.Idle;
                    break;
            }
            lastControls = controls;
            return true;
        }

        static void Finish(KeyCode key, PadControl control)
        {
            resultKey = key;
            resultPad = control;
            resultReady = true;
            state = CaptureState.AfterRelease;   // the captured press must come up before the game reads input again
        }

        // ---- names ------------------------------------------------------------------------------------------------------------

        public static string KeyName(KeyCode k)
        {
            if (k == KeyCode.None)
                return "-";
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9)
                return ((int)(k - KeyCode.Alpha0)).ToString();
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9)
                return "Num " + (int)(k - KeyCode.Keypad0);
            switch (k)
            {
                case KeyCode.Return: return "Enter";
                case KeyCode.KeypadEnter: return "Num Enter";
                case KeyCode.KeypadPlus: return "Num +";
                case KeyCode.KeypadMinus: return "Num -";
                case KeyCode.KeypadMultiply: return "Num *";
                case KeyCode.KeypadDivide: return "Num /";
                case KeyCode.KeypadPeriod: return "Num .";
                case KeyCode.KeypadEquals: return "Num =";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.Equals: return "=";
                case KeyCode.Minus: return "-";
                case KeyCode.Plus: return "+";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.BackQuote: return "`";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.LeftShift: return "L-Shift";
                case KeyCode.RightShift: return "R-Shift";
                case KeyCode.LeftControl: return "L-Ctrl";
                case KeyCode.RightControl: return "R-Ctrl";
                case KeyCode.LeftAlt: return "L-Alt";
                case KeyCode.RightAlt: return "R-Alt";
                case KeyCode.PageUp: return "PgUp";
                case KeyCode.PageDown: return "PgDn";
                case KeyCode.Backspace: return "BkSp";
                case KeyCode.Delete: return "Del";
                case KeyCode.Insert: return "Ins";
            }
            return k.ToString();
        }

        public static string PadName(PadControl c)
        {
            switch (c)
            {
                case PadControl.None: return "-";
                case PadControl.DpadUp: return "D-Pad Up";
                case PadControl.DpadDown: return "D-Pad Down";
                case PadControl.DpadLeft: return "D-Pad Left";
                case PadControl.DpadRight: return "D-Pad Right";
            }
            return c.ToString();
        }
    }
}
#endif
