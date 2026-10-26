#if !SWITCH
// PC platform layer: nn::hid six-axis sensor of the PC Pro Controller. NOT shipped code.
// The play-test reference (Ryujinx, keyboard-driven Pro Controller, no motion source) reports every sample with zero acceleration,
// angular velocity, angle AND a zero direction matrix, attribute IsConnected, and the sampling number counting up
// (NpadController.GetHLEMotionState / NpadDevices.SetSixAxisState). Fusion is off with parameters 0 / 0, the zero-drift mode is
// Standard, the sensor is at rest (IHidServer defaults). GetQuaternion is the SDK's own algorithm (Quaternion below).
using nn.hid;
using nn.util;

namespace PcPlatform
{
    public static class PcSixAxis
    {
        static bool fusionEnabled;
        static float revisePower, reviseRange;
        static GyroscopeZeroDriftMode driftMode = GyroscopeZeroDriftMode.Standard;
        static long samplingNumber;
        static int lastFrame = -1;

        // nn::hid::GetSixAxisSensorHandles: a fixed number of sensors per style (FullKey / JoyLeft / JoyRight 1, Handheld / JoyDual 2).
        // Handle = style index | npad id << 8 | device index << 16 (the console's TypeValue layout).
        public static int GetHandles(SixAxisSensorHandle[] handles, int count, NpadId id, NpadStyle style)
        {
            int styleIndex, n;
            switch (style)
            {
                case NpadStyle.FullKey: styleIndex = 3; n = 1; break;
                case NpadStyle.Handheld: styleIndex = 4; n = 2; break;
                case NpadStyle.JoyDual: styleIndex = 5; n = 2; break;
                case NpadStyle.JoyLeft: styleIndex = 6; n = 1; break;
                case NpadStyle.JoyRight: styleIndex = 7; n = 1; break;
                default: return 0;
            }
            n = System.Math.Min(n, System.Math.Min(count, handles.Length));
            for (int i = 0; i < n; i++)
                handles[i]._storage = styleIndex | ((int)id & 0xFF) << 8 | i << 16;
            return n;
        }

        static bool IsPcController(SixAxisSensorHandle handle)
        {
            return (handle._storage & 0xFF) == 3 && ((handle._storage >> 8) & 0xFF) == (int)NpadId.No1 && (handle._storage >> 16) == 0;
        }

        public static void GetState(ref SixAxisSensorState state, SixAxisSensorHandle handle)
        {
            int frame = UnityEngine.Time.frameCount;
            if (frame != lastFrame)
            {
                lastFrame = frame;
                samplingNumber++;
            }
            state = new SixAxisSensorState();
            state.samplingNumber = samplingNumber;
            if (IsPcController(handle))
                state.attributes = SixAxisSensorAttribute.IsConnected;
        }

        public static int GetStates(SixAxisSensorState[] states, int count, SixAxisSensorHandle handle)
        {
            int n = System.Math.Min(count, states.Length) > 0 ? 1 : 0;
            if (n == 1)
                GetState(ref states[0], handle);
            return n;
        }

        public static bool IsRest(SixAxisSensorHandle handle) { return true; }
        public static bool IsFusionEnabled() { return fusionEnabled; }
        public static void EnableFusion(bool enable) { fusionEnabled = enable; }
        public static void SetFusionParameters(float power, float range) { revisePower = power; reviseRange = range; }
        public static void GetFusionParameters(ref float power, ref float range) { power = revisePower; range = reviseRange; }
        public static void ResetFusionParameters() { revisePower = 0f; reviseRange = 0f; }
        public static void SetGyroscopeZeroDriftMode(GyroscopeZeroDriftMode mode) { driftMode = mode; }
        public static GyroscopeZeroDriftMode GetGyroscopeZeroDriftMode() { return driftMode; }

        // nn::hid::SixAxisSensorState::GetQuaternion (sdk @00223ae0, NEON, transcribed): from the direction basis x, y, z
        //   d = (1 + xx - yy - zz, 1 - xx + yy - zz, 1 - xx - yy + zz, 1 + xx + yy + zz)
        //   S = (x.y + y.x, z.x + x.z, y.z + z.y), D = (x.y - y.x, z.x - x.z, y.z - z.y)
        //   candidates (x, y, z, w): c0 = (d0, S0, S1, D2), c1 = (S0, d1, S2, D1), c2 = (S1, S2, d2, D0), c3 = (D2, D1, D0, d3)
        //   the candidate of the largest d wins (ties go to the later one: fcmge), normalised by rsqrt(|c|^2); |c| = 0 gives 0.
        public static void Quaternion(ref SixAxisSensorState s, out float qx, out float qy, out float qz, out float qw)
        {
            Float3 x = s.direction.x, y = s.direction.y, z = s.direction.z;
            float d0 = 1f + x.x - y.y - z.z, d1 = 1f - x.x + y.y - z.z, d2 = 1f - x.x - y.y + z.z, d3 = 1f + x.x + y.y + z.z;
            float s0 = x.y + y.x, s1 = z.x + x.z, s2 = y.z + z.y;
            float t0 = x.y - y.x, t1 = z.x - x.z, t2 = y.z - z.y;
            float cx = d0, cy = s0, cz = s1, cw = t2, m = d0;
            if (d1 >= m) { cx = s0; cy = d1; cz = s2; cw = t1; m = d1; }
            if (d2 >= m) { cx = s1; cy = s2; cz = d2; cw = t0; m = d2; }
            if (d3 >= m) { cx = t2; cy = t1; cz = t0; cw = d3; }
            float len2 = cx * cx + cy * cy + cz * cz + cw * cw;
            if (len2 == 0f)
            {
                qx = qy = qz = qw = 0f;
                return;
            }
            float inv = 1f / (float)System.Math.Sqrt(len2);
            qx = cx * inv;
            qy = cy * inv;
            qz = cz * inv;
            qw = cw * inv;
        }
    }
}
#endif
