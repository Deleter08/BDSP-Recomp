#if !SWITCH
// PC platform layer: nn::hid Npad on the PC (HANDOFF §11.7 "Input"). NOT shipped code.
//
// The emulated console has ONE controller: NpadId.No1 with style FullKey (a Pro Controller, Ryujinx's default player 1), always
// connected; Handheld and the other ids report style None. Its state is built every Unity frame from three sources, OR-ed:
//   1. the keyboard through the player's bindings (PcBindings, s47; default = Ryujinx's keys, so the editor plays like the
//      reference emulator: left stick W/A/S/D (click F), d-pad arrow keys, Minus = '-', L = E, ZL = Q, right stick I/J/K/L
//      (click H), A = Z, B = X, X = C, Y = V, Plus = '=' / keypad '+', R = U, ZR = O);
//   2. XInput pads (PcXInput, s41) through the bindings too (default by POSITION as Ryujinx maps them: Switch A = pad B, B = A,
//      X = Y, Y = X, L/R = LB/RB, ZL/ZR = LT/RT, Minus = Back, Plus = Start, R-stick press = RS; LS = Fast-Forward since s47).
//      macOS: Apple's GameController framework (PcMacPad: DualSense, DualShock 4, Xbox, MFi ... by position); with no pad there,
//      and off Windows / macOS: Unity's legacy Input joystick 1 with the "PcPad.*" axes of ProjectSettings/InputManager.asset (the
//      s34-s40 path), read into the same pad controls;
//   3. a scripted input queue (PcInput) that the editor play driver loads for automated runs.
// The bindings' Fast-Forward action is no Npad button: FastForwardHeld hands it to PcGameSpeed. While an Options controls page waits
// for a key / button (PcBindings capture), the physical keyboard and pads reach that page only.
// Stick values are -32767..32767 (nn::hid AnalogStickMax); the stick-direction buttons (StickLLeft ...) are set past half
// deflection, as Ryujinx's NpadDevices does (StickButtonThreshold = short.MaxValue / 2).
using System;
using nn.hid;
using UnityEngine;

namespace PcPlatform
{
    public static class PcHid
    {
        const int StickMax = 32767;
        const int StickButtonThreshold = short.MaxValue / 2;

        static NpadStyle supportedStyles = NpadStyle.FullKey | NpadStyle.Handheld | NpadStyle.JoyDual | NpadStyle.JoyLeft | NpadStyle.JoyRight;
        static int lastFrame = -1;
        static long samplingNumber;
        static NpadButton buttons;
        static AnalogStickState stickL, stickR;
        static bool axesChecked, axesPresent;
        static bool fastForward;

        // s47: the bindings' Fast-Forward action this frame (PcGameSpeed)
        public static bool FastForwardHeld
        {
            get
            {
                Sample();
                return fastForward;
            }
        }

        // nn::hid keeps the last 16 samples per pad (NpadStateCountMax); GetStates returns them newest first.
        const int HistoryMax = 16;
        static readonly long[] histSampling = new long[HistoryMax];
        static readonly NpadButton[] histButtons = new NpadButton[HistoryMax];
        static readonly AnalogStickState[] histL = new AnalogStickState[HistoryMax];
        static readonly AnalogStickState[] histR = new AnalogStickState[HistoryMax];
        static int histHead, histCount;

        public static void Initialize()
        {
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
            PcConfig.Log("hid: one Pro Controller (Npad No1, FullKey): keyboard + GameController / legacy joystick pad (PcBindings) + scripted input");
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            PcConfig.Log("hid: one Pro Controller (Npad No1, FullKey): keyboard + XInput pad (PcBindings) + scripted input");
#elif UNITY_ANDROID
            PcConfig.Log("hid: one Pro Controller (Npad No1, FullKey): on-screen touch controller (PcVirtualPad) + legacy joystick pad (PcBindings) + scripted input");
#elif UNITY_IOS
            PcConfig.Log("hid: one Pro Controller (Npad No1, FullKey): GameController pad (PcMacPad) + on-screen touch controller (PcVirtualPad) + scripted input");
#else
            PcConfig.Log("hid: one Pro Controller (Npad No1, FullKey): keyboard + legacy joystick pad (PcBindings) + scripted input");
#endif
        }

        public static void SetSupportedStyleSet(NpadStyle styles) { supportedStyles = styles; }

        public static NpadStyle GetSupportedStyleSet() { return supportedStyles; }

        public static NpadStyle GetStyleSet(NpadId id)
        {
            return id == NpadId.No1 && (supportedStyles & NpadStyle.FullKey) != 0 ? NpadStyle.FullKey : NpadStyle.None;
        }

        // The state of `id` read as `style`: the controller only answers for its own id and style; anything else reads as a
        // disconnected pad (zeroed, attributes None) with the sampling number still counting, as the console's shared memory does.
        public static void Read(NpadId id, NpadStyle style, out long sampling, out NpadButton outButtons, out AnalogStickState l,
            out AnalogStickState r, out NpadAttribute attributes)
        {
            Sample();
            sampling = samplingNumber;
            if (id == NpadId.No1 && style == NpadStyle.FullKey && (supportedStyles & NpadStyle.FullKey) != 0)
            {
                outButtons = buttons;
                l = stickL;
                r = stickR;
                attributes = NpadAttribute.IsConnected | NpadAttribute.IsWired;
            }
            else
            {
                outButtons = NpadButton.None;
                l = new AnalogStickState();
                r = new AnalogStickState();
                attributes = NpadAttribute.None;
            }
        }

        public delegate void StateSink(int index, long sampling, NpadButton buttons, AnalogStickState l, AnalogStickState r, NpadAttribute attributes);

        // GetStates: up to `count` samples of `id` read as `style`, newest first; returns how many were written.
        public static int ReadHistory(NpadId id, NpadStyle style, int count, StateSink sink)
        {
            Sample();
            int n = Math.Min(count, histCount);
            bool mine = id == NpadId.No1 && style == NpadStyle.FullKey && (supportedStyles & NpadStyle.FullKey) != 0;
            for (int i = 0; i < n; i++)
            {
                int k = (histHead - i + HistoryMax) % HistoryMax;
                if (mine)
                    sink(i, histSampling[k], histButtons[k], histL[k], histR[k], NpadAttribute.IsConnected | NpadAttribute.IsWired);
                else
                    sink(i, histSampling[k], NpadButton.None, new AnalogStickState(), new AnalogStickState(), NpadAttribute.None);
            }
            return n;
        }

        // Once per Unity frame (the game polls the pad once per Sequencer update).
        static void Sample()
        {
            int frame = Time.frameCount;
            if (frame == lastFrame)
                return;
            lastFrame = frame;
            samplingNumber++;

            NpadButton b = NpadButton.None;
            int lx = 0, ly = 0, rx = 0, ry = 0;
            int controls = 0, plx = 0, ply = 0, prx = 0, pry = 0;
            bool ff = false;

            // s34: a scripted run ignores the physical keyboard / pad for the whole session - the editor window takes focus when the
            // play driver starts it, and keys typed anywhere on the machine then drove the game (C = X opened the menu, Z = A, X = B).
            if (PcInput.Scripted)
                goto script;

            // the pads' controls + sticks (s41: XInputGetState in PcXInput; s34-s40 read Unity's "Joystick 1", which is any HID
            // joystick with its own axis numbers - a DualShock 4 held "right stick left" through its resting L2 trigger)
            // macOS: the GameController framework (PcMacPad), the legacy joystick only while it reads no pad
#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || (UNITY_IOS && !UNITY_EDITOR)
            if (!PcMacPad.Read(ref controls, ref plx, ref ply, ref prx, ref pry))
                ReadLegacyPad(ref controls, ref plx, ref ply, ref prx, ref pry);
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            PcXInput.Read(ref controls, ref plx, ref ply, ref prx, ref pry);
#else
            ReadLegacyPad(ref controls, ref plx, ref ply, ref prx, ref pry);
#endif

            // s47: an Options controls page is waiting for a key / button - it gets the physical input, the game gets none
            if (PcBindings.CaptureFrame(controls))
                goto script;

            // 1. keyboard, 2. pads - through the bindings
            PcBindings.ReadKeyboard(ref b, ref lx, ref ly, ref rx, ref ry, ref ff);
            PcBindings.ApplyPad(controls, ref b, ref ff);
            lx += plx;
            ly += ply;
            rx += prx;
            ry += pry;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // Android / iOS: the on-screen controller (touch), OR-ed like a second pad
            PcVirtualPad.Apply(ref b, ref lx, ref ly, ref rx, ref ry);
#endif

            // 3. scripted input
            script:
            PcInput.Apply(ref b, ref lx, ref ly, ref rx, ref ry);
            fastForward = ff;

            stickL = new AnalogStickState { x = Clamp(lx), y = Clamp(ly) };
            stickR = new AnalogStickState { x = Clamp(rx), y = Clamp(ry) };
            if (stickL.x < -StickButtonThreshold) b |= NpadButton.StickLLeft;
            if (stickL.x > StickButtonThreshold) b |= NpadButton.StickLRight;
            if (stickL.y > StickButtonThreshold) b |= NpadButton.StickLUp;
            if (stickL.y < -StickButtonThreshold) b |= NpadButton.StickLDown;
            if (stickR.x < -StickButtonThreshold) b |= NpadButton.StickRLeft;
            if (stickR.x > StickButtonThreshold) b |= NpadButton.StickRRight;
            if (stickR.y > StickButtonThreshold) b |= NpadButton.StickRUp;
            if (stickR.y < -StickButtonThreshold) b |= NpadButton.StickRDown;
            buttons = b;

            histHead = (histHead + 1) % HistoryMax;
            histSampling[histHead] = samplingNumber;
            histButtons[histHead] = b;
            histL[histHead] = stickL;
            histR[histHead] = stickR;
            if (histCount < HistoryMax)
                histCount++;
        }

        static int Clamp(int v)
        {
            return v < -StickMax ? -StickMax : (v > StickMax ? StickMax : v);
        }

#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR) || !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
        // Off Windows: legacy joystick 1 in the Xbox layout (buttons 0-9 = A B X Y LB RB Back Start LS RS) -> pad controls
        static void ReadLegacyPad(ref int controls, ref int lx, ref int ly, ref int rx, ref int ry)
        {
            if (Input.GetKey(KeyCode.Joystick1Button0)) controls |= PcBindings.Bit(PadControl.A);
            if (Input.GetKey(KeyCode.Joystick1Button1)) controls |= PcBindings.Bit(PadControl.B);
            if (Input.GetKey(KeyCode.Joystick1Button2)) controls |= PcBindings.Bit(PadControl.X);
            if (Input.GetKey(KeyCode.Joystick1Button3)) controls |= PcBindings.Bit(PadControl.Y);
            if (Input.GetKey(KeyCode.Joystick1Button4)) controls |= PcBindings.Bit(PadControl.LB);
            if (Input.GetKey(KeyCode.Joystick1Button5)) controls |= PcBindings.Bit(PadControl.RB);
            if (Input.GetKey(KeyCode.Joystick1Button6)) controls |= PcBindings.Bit(PadControl.Back);
            if (Input.GetKey(KeyCode.Joystick1Button7)) controls |= PcBindings.Bit(PadControl.Start);
            if (Input.GetKey(KeyCode.Joystick1Button8)) controls |= PcBindings.Bit(PadControl.LS);
            if (Input.GetKey(KeyCode.Joystick1Button9)) controls |= PcBindings.Bit(PadControl.RS);
            if (!AxesPresent())
                return;
            if (Input.GetAxisRaw("PcPad.LT") > 0.5f) controls |= PcBindings.Bit(PadControl.LT);
            if (Input.GetAxisRaw("PcPad.RT") > 0.5f) controls |= PcBindings.Bit(PadControl.RT);
            float dx = Input.GetAxisRaw("PcPad.DX"), dy = Input.GetAxisRaw("PcPad.DY");
            if (dx < -0.5f) controls |= PcBindings.Bit(PadControl.DpadLeft);
            if (dx > 0.5f) controls |= PcBindings.Bit(PadControl.DpadRight);
            if (dy > 0.5f) controls |= PcBindings.Bit(PadControl.DpadUp);
            if (dy < -0.5f) controls |= PcBindings.Bit(PadControl.DpadDown);
            lx += (int)(Mathf.Clamp(Input.GetAxisRaw("PcPad.LX"), -1f, 1f) * StickMax);
            ly += (int)(Mathf.Clamp(Input.GetAxisRaw("PcPad.LY"), -1f, 1f) * StickMax);
            rx += (int)(Mathf.Clamp(Input.GetAxisRaw("PcPad.RX"), -1f, 1f) * StickMax);
            ry += (int)(Mathf.Clamp(Input.GetAxisRaw("PcPad.RY"), -1f, 1f) * StickMax);
        }
#endif

        static bool AxesPresent()
        {
            if (!axesChecked)
            {
                axesChecked = true;
                try
                {
                    Input.GetAxisRaw("PcPad.LX");
                    axesPresent = true;
                }
                catch (ArgumentException)
                {
                    axesPresent = false;
                    PcConfig.Log("hid: the PcPad.* axes are missing from InputManager.asset - pad sticks / triggers / d-pad disabled");
                }
            }
            return axesPresent;
        }
    }
}
#endif
