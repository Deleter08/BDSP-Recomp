#if !SWITCH && (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
// PC platform layer: game pads through XInput itself (s41). NOT shipped code.
//
// s34-s40 read "Joystick 1" of Unity's legacy Input - whatever HID joystick Windows lists first - with Xbox axis numbers. A
// DirectInput device (DualShock 4 / DualSense, Switch Pro Controller, wheels, virtual joysticks) numbers its axes differently: a DS4's
// L2 trigger rests at -1 on the axis read as right stick X, so every menu that takes the right stick scrolled left forever (player
// report, s41). XInputGetState sees XInput pads only (Xbox pads and most PC pads; a DS4 / DualSense through Steam Input or DS4Windows)
// with one fixed layout, so no other device can inject input.
// Every connected pad (user index 0..3) is OR-ed into one set of pad controls (PadControl bits) + the two sticks; PcBindings maps
// the controls to the emulated Pro Controller's buttons (s47; default by POSITION as Ryujinx does: Switch A = pad B, B = A, X = Y,
// Y = X, L/R = LB/RB, ZL/ZR = LT/RT, Minus = Back, Plus = Start, R-stick press = RS, LS = Fast-Forward).
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace PcPlatform
{
    public static class PcXInput
    {
        [StructLayout(LayoutKind.Sequential)]
        struct Gamepad
        {
            public ushort buttons;
            public byte leftTrigger, rightTrigger;
            public short thumbLX, thumbLY, thumbRX, thumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct State
        {
            public uint packet;
            public Gamepad pad;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
        static extern uint GetState14(uint user, out State state);

        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
        static extern uint GetState910(uint user, out State state);

        const int Users = 4;
        const int StickMax = 32767;
        const float StickDead = 0.1f;   // the s34 InputManager stick dead zone (rescaled outside it, as Unity did)
        const int TriggerOn = 128;      // the s34 trigger threshold (axis > 0.5)

        static int dll;                 // 0 = not tried, 1 = xinput1_4.dll (Windows 8+), 2 = xinput9_1_0.dll, -1 = none
        static readonly bool[] connected = new bool[Users];
        static float nextScan;          // empty slots are polled once a second: XInputGetState on an empty slot is slow

        public static void Read(ref int controls, ref int lx, ref int ly, ref int rx, ref int ry)
        {
            bool scan = Time.unscaledTime >= nextScan;
            if (scan)
                nextScan = Time.unscaledTime + 1f;
            for (int user = 0; user < Users; user++)
            {
                if (!connected[user] && !scan)
                    continue;
                State s;
                bool now = Get(user, out s);
                if (now != connected[user])
                {
                    connected[user] = now;
                    PcConfig.Log("hid: XInput pad " + user + (now ? " connected" : " disconnected"));
                }
                if (!now)
                    continue;

                var g = s.pad;
                controls |= Controls(g);
                // XInput thumb Y is up-positive, as nn::hid's
                lx += Axis(g.thumbLX);
                ly += Axis(g.thumbLY);
                rx += Axis(g.thumbRX);
                ry += Axis(g.thumbRY);
            }
        }

        // XInput button bits + triggers -> PadControl bits (PcBindings.Bit)
        static int Controls(Gamepad g)
        {
            int k = g.buttons, c = 0;
            if ((k & 0x0001) != 0) c |= PcBindings.Bit(PadControl.DpadUp);
            if ((k & 0x0002) != 0) c |= PcBindings.Bit(PadControl.DpadDown);
            if ((k & 0x0004) != 0) c |= PcBindings.Bit(PadControl.DpadLeft);
            if ((k & 0x0008) != 0) c |= PcBindings.Bit(PadControl.DpadRight);
            if ((k & 0x0010) != 0) c |= PcBindings.Bit(PadControl.Start);
            if ((k & 0x0020) != 0) c |= PcBindings.Bit(PadControl.Back);
            if ((k & 0x0040) != 0) c |= PcBindings.Bit(PadControl.LS);
            if ((k & 0x0080) != 0) c |= PcBindings.Bit(PadControl.RS);
            if ((k & 0x0100) != 0) c |= PcBindings.Bit(PadControl.LB);
            if ((k & 0x0200) != 0) c |= PcBindings.Bit(PadControl.RB);
            if ((k & 0x1000) != 0) c |= PcBindings.Bit(PadControl.A);   // bottom
            if ((k & 0x2000) != 0) c |= PcBindings.Bit(PadControl.B);   // right
            if ((k & 0x4000) != 0) c |= PcBindings.Bit(PadControl.X);   // left
            if ((k & 0x8000) != 0) c |= PcBindings.Bit(PadControl.Y);   // top
            if (g.leftTrigger >= TriggerOn) c |= PcBindings.Bit(PadControl.LT);
            if (g.rightTrigger >= TriggerOn) c |= PcBindings.Bit(PadControl.RT);
            return c;
        }

        // s43: the controls of every pad seen connected, OR-ed (PadControl bits since s47) - for a modal Win32 dialog (PcSwkbdNative)
        // that runs while the game's frame is suspended. Only slots the game already found are read (an empty slot's XInputGetState is
        // slow, and Time does not advance inside the dialog, so Read's once-a-second scan would never come).
        public static int ReadRawControls()
        {
            int c = 0;
            for (int user = 0; user < Users; user++)
            {
                State s;
                if (connected[user] && Get(user, out s))
                    c |= Controls(s.pad);
            }
            return c;
        }

        static int Axis(short raw)
        {
            float v = Mathf.Clamp(raw / (float)StickMax, -1f, 1f);
            float a = Mathf.Abs(v);
            if (a < StickDead)
                return 0;
            return (int)(Mathf.Sign(v) * (a - StickDead) / (1f - StickDead) * StickMax);
        }

        static bool Get(int user, out State s)
        {
            s = default(State);
            if (dll == 0)
            {
                try
                {
                    GetState14(0, out s);
                    dll = 1;
                }
                catch (Exception)
                {
                    try
                    {
                        GetState910(0, out s);
                        dll = 2;
                    }
                    catch (Exception)
                    {
                        dll = -1;
                    }
                }
                PcConfig.Log(dll > 0 ? "hid: XInput through " + (dll == 1 ? "xinput1_4.dll" : "xinput9_1_0.dll") : "hid: no XInput DLL - game pads off");
            }
            if (dll < 0)
                return false;
            return (dll == 1 ? GetState14((uint)user, out s) : GetState910((uint)user, out s)) == 0;
        }
    }
}
#endif
