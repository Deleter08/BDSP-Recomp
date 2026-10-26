#if !SWITCH && (UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_IOS && !UNITY_EDITOR))
// PC platform layer, macOS: game pads through Apple's GameController framework (Assets/Plugins/macOS/PcGamepad.bundle, source
// setup/native/pcgamepad). NOT shipped code.
//
// Unity's legacy "Joystick 1" numbers a DualSense / DualShock 4's buttons and axes its own way (Square = button 0, L2 rests at -1 on
// an axis the Xbox layout reads as a stick - the s41 report), so the Mac reads pads as GCExtendedGamepad instead: one layout by
// POSITION for every pad the system supports. Its controls become the XInput-named PadControls by position too - bottom face button
// (Cross) = A, right (Circle) = B, left (Square) = X, top (Triangle) = Y, L1/R1 = LB/RB, L2/R2 = LT/RT, Create = Back,
// Options = Start, L3/R3 = LS/RS - so PcBindings' defaults hold: Switch A = Circle, B = Cross, X = Triangle, Y = Square,
// + = Options, - = Create, L3 = Fast-Forward (Options > Button Layout "labels" puts Switch A on Cross). The PS button and the
// touchpad click are no Switch input here. While this reads at least one pad, PcHid skips the legacy joystick (the same pad would be
// read twice in two layouts).
// iOS (2026-10-24): the same reader. GameController is the same framework there (DualSense / DualShock 4 / Xbox / MFi over Bluetooth);
// PcGamepad.m is compiled into the Xcode project from Assets/Plugins/iOS (a copy of setup/native/pcgamepad/PcGamepad.m that
// setup/stages/build_ios.py keeps identical) and linked statically, so the import is "__Internal".
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace PcPlatform
{
    public static class PcMacPad
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct State
        {
            public int pads;
            public uint buttons;
            public float lx, ly, rx, ry, lt, rt;
        }

        // PcGamepad.m's bit numbers
        public enum Bit
        {
            South, East, West, North, L1, R1, L2, R2, Share, Options, L3, R3, Up, Down, Left, Right, Home, Touchpad,
        }

#if UNITY_IOS && !UNITY_EDITOR
        const string Lib = "__Internal";
#else
        const string Lib = "PcGamepad";
#endif

        [DllImport(Lib)]
        static extern int PcGc_Read(out State s);

        [DllImport(Lib)]
        static extern int PcGc_Describe(StringBuilder buf, int cap);

        static readonly PadControl[] ControlOf =
        {
            PadControl.A, PadControl.B, PadControl.X, PadControl.Y, PadControl.LB, PadControl.RB, PadControl.LT, PadControl.RT,
            PadControl.Back, PadControl.Start, PadControl.LS, PadControl.RS,
            PadControl.DpadUp, PadControl.DpadDown, PadControl.DpadLeft, PadControl.DpadRight,
        };

        const int StickMax = 32767;
        const float StickDead = 0.1f;   // PcXInput's / the s34 InputManager dead zone

        static bool missing;
        static int lastPads = -1;

        // false = no pad through the framework (or no plugin): the caller falls back to the legacy joystick
        public static bool Read(ref int controls, ref int lx, ref int ly, ref int rx, ref int ry)
        {
            State s;
            if (!Raw(out s))
                return false;
            if (s.pads != lastPads)
            {
                if (lastPads >= 0 || s.pads > 0)
                    PcConfig.Log("hid: GameController pads " + s.pads + (s.pads > 0 ? " (" + Describe().Trim().Replace("\n", "; ") + ")" : ""));
                lastPads = s.pads;
            }
            if (s.pads == 0)
                return false;
            for (int i = 0; i < ControlOf.Length; i++)
                if (i != (int)Bit.L2 && i != (int)Bit.R2 && (s.buttons & (1u << i)) != 0)
                    controls |= PcBindings.Bit(ControlOf[i]);
            // the triggers past half travel, as PcXInput's TriggerOn and the s34 axis > 0.5
            if (s.lt > 0.5f) controls |= PcBindings.Bit(PadControl.LT);
            if (s.rt > 0.5f) controls |= PcBindings.Bit(PadControl.RT);
            // GameController's stick Y is up-positive, as nn::hid's
            lx += Axis(s.lx);
            ly += Axis(s.ly);
            rx += Axis(s.rx);
            ry += Axis(s.ry);
            return true;
        }

        // the plugin's state as is (PcPadDiag); false when the plugin is missing
        public static bool Raw(out State s)
        {
            s = default(State);
            if (missing)
                return false;
            try
            {
                PcGc_Read(out s);
                return true;
            }
            catch (Exception e)
            {
                missing = true;
                PcConfig.Log("hid: no PcGamepad plugin (" + e.GetType().Name + ") - Mac pads through Unity's legacy joystick only");
                return false;
            }
        }

        public static string Describe()
        {
            if (missing)
                return "(no PcGamepad plugin)";
            try
            {
                var sb = new StringBuilder(4096);
                PcGc_Describe(sb, sb.Capacity);
                return sb.ToString();
            }
            catch (Exception e)
            {
                return "(PcGc_Describe failed: " + e.GetType().Name + ")";
            }
        }

        static int Axis(float v)
        {
            v = Mathf.Clamp(v, -1f, 1f);
            float a = Mathf.Abs(v);
            if (a < StickDead)
                return 0;
            return (int)(Mathf.Sign(v) * (a - StickDead) / (1f - StickDead) * StickMax);
        }
    }
}
#endif
