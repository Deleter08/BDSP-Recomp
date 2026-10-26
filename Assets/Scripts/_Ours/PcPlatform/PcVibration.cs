#if !SWITCH
// PC platform layer: nn::hid vibration (HANDOFF §11.7 "Input"). NOT shipped code.
//
// The emulated Pro Controller accepts vibration and drops it (a keyboard has no motor), but every query answers as the console:
//   GetDeviceHandles - the SDK's own computation (nn::hid::GetVibrationDeviceHandles @225280 -> utility @22a6d0): the handle is
//                      (npad style index) | (npad id << 8) | (device index << 16); FullKey = style index 3 with two devices
//                      (0x00000003 | id << 8, 0x00010003 | id << 8), Handheld 4 / JoyDual 5 two each, JoyLeft 6 (device 0) /
//                      JoyRight 7 (device 1) one, Gc 8 / style bit 10 (0xd) one, System 0x21 / SystemExt 0x20 two, bits 6 / 7 / 9
//                      none, bit 8 as Handheld; the wrapper aborts unless count > 0 and exactly one style bit is set;
//   GetDeviceInfo    - Ryujinx IHidServer.GetVibrationDeviceInfo: a defined style index -> LinearResonantActuator, device index
//                      0 = Left, 1 = Right; an invalid npad id (InvalidNpadIdType) / device index > 1 (InvalidDeviceIndex) fails
//                      and the SDK aborts on it;
//   InitializeDevice - success, nothing to do;
//   SendValue        - Ryujinx SendVibrationValue -> Npads.UpdateRumbleQueue: the player's last value pair becomes (sent, neutral)
//                      or (neutral, sent) by device index, then the queue is drained by the input driver (here: dropped);
//   GetActualValue   - the last value of that player / device, the neutral value (0.01, 160, 0, 320) before any send;
//   IsPermitted      - true (IHidServer: _vibrationPermitted = true).
using System.Collections.Generic;
using nn.hid;

namespace PcPlatform
{
    public static class PcVibration
    {
        static readonly VibrationValue Neutral = new VibrationValue(0.01f, 160f, 0f, 320f);

        // PlayerId -> (Left, Right)
        static readonly Dictionary<int, VibrationValue[]> lastValues = new Dictionary<int, VibrationValue[]>();

        static int StyleIndex(VibrationDeviceHandle h) { return (int)(h._storage & 0xFF); }
        static int PlayerId(VibrationDeviceHandle h) { return (int)((h._storage >> 8) & 0xFF); }
        static int DeviceIdx(VibrationDeviceHandle h) { return (int)((h._storage >> 16) & 0xFF); }

        static uint Make(int styleIndex, int npadId, int deviceIdx)
        {
            return (uint)(styleIndex & 0xFF) | (uint)(npadId & 0xFF) << 8 | (uint)(deviceIdx & 0xFF) << 16;
        }

        public static int GetDeviceHandles(VibrationDeviceHandle[] pOutValues, int count, NpadId npadId, NpadStyle npadStyle)
        {
            uint style = (uint)npadStyle;
            if (count <= 0 || style == 0 || (style & (style - 1)) != 0)
                throw new NnAbortException("nn::hid::GetVibrationDeviceHandles: count > 0 && one npad style");
            int id = (int)npadId;
            if ((uint)id >= 8 && id != 0x20 && id != 0x10)
                throw new NnAbortException("nn::hid::GetVibrationDeviceHandles: invalid NpadIdType " + id);
            // the utility tests the bits in this order; the first set bit decides
            int styleIndex, devices, firstIdx = 0;
            if ((style & 0x1) != 0) { styleIndex = 3; devices = 2; }                       // FullKey
            else if ((style & 0x2) != 0) { styleIndex = 4; devices = 2; }                  // Handheld
            else if ((style & 0x4) != 0) { styleIndex = 5; devices = 2; }                  // JoyDual
            else if ((style & 0x8) != 0) { styleIndex = 6; devices = 1; }                  // JoyLeft
            else if ((style & 0x10) != 0) { styleIndex = 7; devices = 1; firstIdx = 1; }   // JoyRight: its one device is index 1
            else if ((style & 0x20) != 0) { styleIndex = 8; devices = 1; }
            else if ((style & 0x400) != 0) { styleIndex = 0xd; devices = 1; }
            else if ((style & 0x280) != 0) return 0;
            else if ((style & 0x100) != 0) { styleIndex = 4; devices = 2; }
            else if ((style & 0x40000000) != 0) { styleIndex = 0x21; devices = 2; }
            else if ((style & 0x20000000) != 0) { styleIndex = 0x20; devices = 2; }
            else if ((style & 0x40) != 0) return 0;
            else
                throw new NnAbortException("nn::hid::GetVibrationDeviceHandles: unsupported npad style 0x" + style.ToString("X"));

            pOutValues[0] = new VibrationDeviceHandle { _storage = Make(styleIndex, id, firstIdx) };
            if (devices < 2 || count < 2)
                return 1;
            pOutValues[1] = new VibrationDeviceHandle { _storage = Make(styleIndex, id, 1) };
            return 2;
        }


        public static void GetDeviceInfo(ref VibrationDeviceInfo pOutValue, VibrationDeviceHandle handle)
        {
            int player = PlayerId(handle);
            if (!((player >= 0 && player <= 7) || player == 0x20 || player == 0x10)) // HidUtils.IsValidNpadIdType
                throw new NnAbortException("nn::hid::GetVibrationDeviceInfo: InvalidNpadIdType (202-123)");
            int idx = DeviceIdx(handle);
            if (idx > 1)
                throw new NnAbortException("nn::hid::GetVibrationDeviceInfo: InvalidDeviceIndex (202-124)");
            int styleIndex = StyleIndex(handle);
            bool lra = (styleIndex >= 3 && styleIndex <= 7) || styleIndex == 32 || styleIndex == 33; // Enum.IsDefined(NpadStyleIndex)
            // style index 8 (Gc) = GcErm in Ryujinx, which this SDK's VibrationDeviceType does not name (value 2)
            pOutValue.deviceType = lra ? VibrationDeviceType.LinearResonantActuator : (styleIndex == 8 ? (VibrationDeviceType)2 : VibrationDeviceType.Unknown);
            pOutValue.position = lra ? (idx == 0 ? VibrationDevicePosition.Left : VibrationDevicePosition.Right) : VibrationDevicePosition.None;
        }

        public static void InitializeDevice(VibrationDeviceHandle handle) { }

        public static void SendValue(VibrationDeviceHandle handle, VibrationValue value)
        {
            VibrationValue[] pair = new VibrationValue[] { Neutral, Neutral };
            int idx = DeviceIdx(handle);
            if (idx <= 1)
                pair[idx] = value;
            lastValues[PlayerId(handle)] = pair;
        }

        public static void GetActualValue(ref VibrationValue pOutValue, VibrationDeviceHandle handle)
        {
            VibrationValue[] pair;
            if (!lastValues.TryGetValue(PlayerId(handle), out pair))
            {
                pOutValue = Neutral;
                return;
            }
            pOutValue = DeviceIdx(handle) == 0 ? pair[0] : pair[1];
        }

        public static bool IsPermitted() { return true; }
    }
}
#endif
