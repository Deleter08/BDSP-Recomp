#if !SWITCH
// PC platform layer: nn::hid touch screen (HANDOFF §11.7 "Input"). NOT shipped code.
//
// The emulated console is docked with a Pro Controller, so nothing touches the screen. Ryujinx's TouchDevice.Update still writes
// one TouchScreenState into the shared-memory LIFO every HID update with SamplingNumber = previous + 1 and no touches, so the
// SDK readers (nn::hid::GetTouchScreenState<N> / GetTouchScreenStates<N>, sdk_flat @21b900 / @21c810) return:
//   GetState  - samplingNumber of the newest entry, count = min(entry count, N) = 0, the N touch slots of the entry (zero);
//   GetStates - min(count, entries in the LIFO (at most 16)) states, newest first, sampling numbers counting down by one.
// The SDK aborts (NN_ABORT_UNLESS on the detail Result) on a null output or a negative count. The LIFO is sampled once per
// Unity frame, as PcHid samples the pad.
using System;
using UnityEngine;

namespace PcPlatform
{
    public static class PcTouch
    {
        const int LifoCountMax = 16; // RingLifo<TouchScreenState<16>, 16>

        static int lastFrame = -1;
        static long samplingNumber;
        static int lifoCount;

        public static void Initialize()
        {
            PcConfig.Log("hid: touch screen - docked console, no touches (sampling number counts per frame)");
        }

        static void Sample()
        {
            int frame = Time.frameCount;
            if (frame == lastFrame)
                return;
            lastFrame = frame;
            samplingNumber++;
            if (lifoCount < LifoCountMax)
                lifoCount++;
        }

        // The newest entry's sampling number (its count is 0 and its touch slots are zero).
        public static long GetState()
        {
            Sample();
            return samplingNumber;
        }

        public delegate T StateMaker<T>(long sampling);

        // The [Out] marshaller hands the native a zeroed buffer of pOutValues.Length states and copies every one of them back, so
        // the slots past the states read are default.
        public static int GetStates<T>(T[] pOutValues, int count, StateMaker<T> make) where T : struct
        {
            if (pOutValues == null)
                throw new NnAbortException("nn::hid::GetTouchScreenStates: pOutValues == nullptr");
            if (count < 0)
                throw new NnAbortException("nn::hid::GetTouchScreenStates: count >= 0");
            Sample();
            int n = Math.Min(count, lifoCount);
            for (int i = 0; i < pOutValues.Length; i++)
                pOutValues[i] = i < n ? make(samplingNumber - i) : default(T);
            return n;
        }
    }
}
#endif
