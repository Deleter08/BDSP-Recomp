#if !SWITCH
// PC platform layer: the small nn::hid services around the one Pro Controller (HANDOFF §11.7 "Input"). NOT shipped code.
//
// Emulates what Ryujinx's HID service (Ryujinx.HLE/HOS/Services/Hid/IHidServer.cs) and its controller applet
// (HOS/Applets/Controller/ControllerApplet.cs) answer on a docked console with one Pro Controller (Npad No1, FullKey):
//   - controller-support applet: success at once, one player, No1 selected (Npads.Validate with the one connected pad);
//   - firmware-update / strap-guide applets (the same applet in its other modes): a success response at once;
//   - SetExplainText: the SDK's own copy (nn::hid::SetExplainText, sdk_flat @244bf0) into the argument's explain-text buffer;
//   - NpadJoy: assignment mode per id (shared-memory default Dual), hold type (default Vertical), handheld activation mode
//     (default Dual), communication mode (default 0) stored and returned; merge / swap / LR-assignment accepted;
//   - gestures: the gesture LIFO is never written, so GetGestureStates reads nothing.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using nn;
using nn.hid;

namespace PcPlatform
{
    public static class PcHidMisc
    {
        // ---- applets ---------------------------------------------------------------------------------------------------------

        const int ExplainTextSize = 1032; // ControllerSupportArg.ExplainTextSize = 8 slots x ExplainTextMaxBufferSize (129)

        static FieldInfo explainTextField;

        static FieldInfo ExplainTextField
        {
            get
            {
                if (explainTextField == null)
                    explainTextField = typeof(ControllerSupportArg).GetField("explainText", BindingFlags.Instance | BindingFlags.NonPublic);
                return explainTextField;
            }
        }

        // The il2cpp marshaller copies explainText as ByValArray[0x408]: a non-null array shorter than that throws before the native call.
        static void CheckMarshal(ControllerSupportArg arg)
        {
            byte[] text = (byte[])ExplainTextField.GetValue(arg);
            if (text != null && text.Length < ExplainTextSize)
                throw new ArgumentException("Type could not be marshaled because the length of an embedded array instance does not match the declared length in the layout.");
        }

        public static Result ShowControllerSupport(ref ControllerSupportResultInfo info, ControllerSupportArg arg)
        {
            CheckMarshal(arg);
            // ControllerApplet.Start -> Npads.Validate: the pad counts when its style is accepted (the applet gets the supported style set).
            bool accepted = (PcHid.GetSupportedStyleSet() & NpadStyle.FullKey) != 0 && PcHid.GetStyleSet(NpadId.No1) != NpadStyle.None;
            int count = accepted ? 1 : 0;
            info.playerCount = (byte)count;
            // GetNpadIdTypeFromIndex(PlayerIndex.Unknown) = NpadIdType.Unknown (0x10) when no pad qualified
            info.selectedId = accepted ? NpadId.No1 : (NpadId)0x10;
            PcConfig.Log("hid: controller-support applet (min " + arg.playerCountMin + " max " + arg.playerCountMax + ") -> " + count + " player(s), " + info.selectedId);
            return new Result();
        }

        public static Result ShowControllerFirmwareUpdate()
        {
            PcConfig.Log("hid: controller firmware-update applet -> success");
            return new Result();
        }

        public static Result ShowControllerStrapGuide()
        {
            PcConfig.Log("hid: controller strap-guide applet -> success");
            return new Result();
        }

        // nn::hid::SetExplainText @244bf0: slot = npadId for No1..No8 (id 0x10 maps to 9 and any other id - Handheld included - to 8,
        // both rejected by the `> 7` test, so nothing is written); copies the UTF-8 bytes up to the first NUL, at most 128, then writes a NUL.
        public static void SetExplainText(ref ControllerSupportArg arg, string text, NpadId npadId)
        {
            CheckMarshal(arg);
            byte[] buffer = (byte[])ExplainTextField.GetValue(arg);
            // the native -> managed copy-back gives the struct a 0x408-byte array whatever it held before
            byte[] result = new byte[ExplainTextSize];
            if (buffer != null)
                Buffer.BlockCopy(buffer, 0, result, 0, ExplainTextSize);
            int id = (int)npadId;
            int slot = (uint)id < 8 ? id : (id == 0x10 ? 9 : 8);
            if (slot <= 7)
            {
                byte[] utf8 = text == null ? new byte[0] : Encoding.UTF8.GetBytes(text);
                int at = slot * ControllerSupport.ExplainTextMaxBufferSize;
                int i = 0;
                while (i < 128 && i < utf8.Length && utf8[i] != 0)
                {
                    result[at + i] = utf8[i];
                    i++;
                }
                result[at + i] = 0;
            }
            object boxed = arg;
            ExplainTextField.SetValue(boxed, result);
            arg = (ControllerSupportArg)boxed;
        }

        // ---- NpadJoy ---------------------------------------------------------------------------------------------------------

        static readonly Dictionary<NpadId, NpadJoyAssignmentMode> assignment = new Dictionary<NpadId, NpadJoyAssignmentMode>();
        static NpadJoyHoldType holdType = NpadJoyHoldType.Vertical;                               // NpadDevices: JoyHold = Vertical
        static NpadHandheldActivationMode handheldActivationMode = NpadHandheldActivationMode.Dual; // IHidServer ctor
        static NpadCommunicationMode communicationMode = (NpadCommunicationMode)0;                  // IHidServer: long, never initialised

        public static NpadJoyAssignmentMode GetAssignment(NpadId npadId)
        {
            NpadJoyAssignmentMode mode;
            return assignment.TryGetValue(npadId, out mode) ? mode : NpadJoyAssignmentMode.Dual;
        }

        // Ryujinx sets the mode only for a valid id (No1..No8, Handheld) and never finds a partner pad to merge with.
        public static void SetAssignment(NpadId npadId, NpadJoyAssignmentMode mode)
        {
            if (IsValidNpadId(npadId))
                assignment[npadId] = mode;
        }

        public static void SetHoldType(NpadJoyHoldType type) { holdType = type; }

        public static NpadJoyHoldType GetHoldType() { return holdType; }

        public static void SetHandheldActivationMode(NpadHandheldActivationMode mode) { handheldActivationMode = mode; }

        public static NpadHandheldActivationMode GetHandheldActivationMode() { return handheldActivationMode; }

        public static void SetCommunicationMode(NpadCommunicationMode mode) { communicationMode = mode; }

        public static NpadCommunicationMode GetCommunicationMode() { return communicationMode; }

        public static bool IsValidNpadId(NpadId npadId)
        {
            return (npadId >= NpadId.No1 && npadId <= NpadId.No8) || npadId == NpadId.Handheld;
        }

        // ---- gestures --------------------------------------------------------------------------------------------------------

        public static void InitializeGesture()
        {
            PcConfig.Log("hid: gestures - none (no touch input on the docked console)");
        }

        // GetGestureStates: the LIFO holds no entry, so nothing is read.
        public static int GetGestureStates(GestureState[] states, int count)
        {
            return 0;
        }
    }
}
#endif
