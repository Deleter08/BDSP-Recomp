#if !SWITCH
// PC platform layer (s33c): nn::irsensor (the Joy-Con (R) IR camera). NOT shipped code.
// The emulated console has no IR camera: every camera reports Unconnected, no image processor ever runs, and every processor-state
// read fails with ResultIrsensorUnconnected (205-110) leaving its outputs untouched. The game itself never calls nn::irsensor
// (extern census: 0 call sites). Default configurations are the SDK's constants as libnx (irs.c) documents them.
// Work buffers are real unmanaged allocations, sized as the native glue sizes them, so the managed wrappers keep working.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using nn.irsensor;

namespace PcPlatform
{
    public static class PcIrsensor
    {
        const int Module = 205;
        public static readonly nn.Result Unconnected = new nn.Result(Module, 110);

        // nn::irsensor::GetIrCameraHandle(NpadId): the player index (No1..No8 = 0..7, Handheld = 8; Ryujinx GetNpadIrCameraHandle),
        // device type 0 in the second byte.
        public static IrCameraHandle GetHandle(nn.hid.NpadId npadId)
        {
            uint index;
            if (npadId >= nn.hid.NpadId.No1 && npadId <= nn.hid.NpadId.No8)
                index = (uint)npadId;
            else if (npadId == nn.hid.NpadId.Handheld)
                index = 8;
            else
                index = 9; // TODO(pc): the SDK aborts on an invalid NpadId (Ryujinx: NpadIdOutOfRange); an unknown index is returned
            return new IrCameraHandle { _handle = index };
        }

        public static IrCameraStatus GetStatus(IrCameraHandle handle)
        {
            return IrCameraStatus.Unconnected;
        }

        public static nn.Result CheckFirmwareUpdateNecessity(ref bool isUpdateNeeded)
        {
            isUpdateNeeded = false;
            return Unconnected;
        }

        // Nothing runs: the processor status stays Stopped.
        public static ImageProcessorStatus GetProcessorStatus(IrCameraHandle handle)
        {
            return ImageProcessorStatus.Stopped;
        }

        // ---- default configurations (TODO(pc): values per libnx irs.c, not read from the SDK, which is not in our image) ----

        static IrCameraConfig CameraConfig(long exposureNs, int gain)
        {
            return new IrCameraConfig { exposureTimeNanoSeconds = exposureNs, lightTarget = IrCameraLightTarget.AllObjects, gain = gain, isNegativeImageUsed = false };
        }

        public static void GetDefaultConfig(ref ImageTransferProcessorConfig config)
        {
            config.irCameraConfig = CameraConfig(300000, 8);
            config.format = ImageTransferProcessorFormat.Format320x240;
        }

        public static void GetDefaultConfig(ref ImageTransferProcessorExConfig config)
        {
            config.irCameraConfig = CameraConfig(300000, 8);
            config.origFormat = ImageTransferProcessorFormat.Format320x240;
            config.trimmingFormat = ImageTransferProcessorFormat.Format320x240;
            config.trimmingStartX = 0;
            config.trimmingStartY = 0;
            config.isExternalLightFilterEnabled = true;
        }

        public static void GetDefaultConfig(ref MomentProcessorConfig config)
        {
            config.irCameraConfig = CameraConfig(300000, 8);
            config.windowOfInterest = new Rect(0, 0, 320, 240);
            config.preprocess = MomentProcessorPreprocess.Cutoff;
            config.preprocessIntensityThreshold = 80;
        }

        public static void GetDefaultConfig(ref ClusteringProcessorConfig config)
        {
            config.irCameraConfig = CameraConfig(200000, 2);
            config.windowOfInterest = new Rect(0, 0, 320, 240);
            config.objectPixelCountMin = 3;
            config.objectPixelCountMax = 76800;
            config.objectIntensityMin = 150;
            config.isExternalLightFilterEnabled = true;
        }

        // ---- image-transfer work buffers (the native glue: aligned_alloc(0x1000, size by format), size written out) ----

        static readonly Dictionary<long, bool> buffers = new Dictionary<long, bool>();

        // 320x240 = 0x26000, then 160x120 / 80x60 / 40x30 / 20x15 from the glue's table @03d6d1f8.
        public static long WorkBufferSize(ImageTransferProcessorFormat format)
        {
            switch ((int)format)
            {
                case 1: return 40960;
                case 2: return 12288;
                case 3: return 4096;
                case 4: return 4096;
                default: return 155648;
            }
        }

        public static void InitializeWorkBuffer(ref IntPtr workBuffer, ref long workBufferSize, ImageTransferProcessorFormat format)
        {
            workBufferSize = WorkBufferSize(format);
            workBuffer = Marshal.AllocHGlobal(new IntPtr(workBufferSize));
            lock (buffers)
                buffers[workBuffer.ToInt64()] = true;
        }

        // free(): a null pointer is a no-op; only buffers this layer allocated are released.
        public static void DestroyWorkBuffer(IntPtr workBuffer)
        {
            if (workBuffer == IntPtr.Zero)
                return;
            bool known;
            lock (buffers)
                known = buffers.Remove(workBuffer.ToInt64());
            if (known)
                Marshal.FreeHGlobal(workBuffer);
        }

        // nn::irsensor::CalculateMomentRegionStatistic: the area-weighted mean intensity of the selected 8x6 blocks and the
        // intensity-weighted centroid. TODO(pc): SDK arithmetic not in our image; with no camera the state is never filled.
        public static MomentStatistic CalculateMomentRegionStatistic(ref MomentProcessorState state, Rect windowOfInterest, int startRow, int startColumn, int rowCount, int columnCount)
        {
            float blockArea = (windowOfInterest.width / 8f) * (windowOfInterest.height / 6f);
            float intensitySum = 0f, cx = 0f, cy = 0f, area = 0f;
            for (int row = startRow; row < startRow + rowCount && row < 6; row++)
            {
                for (int column = startColumn; column < startColumn + columnCount && column < 8; column++)
                {
                    MomentStatistic block = state.blocks[row * 8 + column];
                    float weight = block.averageIntensity * blockArea;
                    intensitySum += weight;
                    cx += block.centroid.x * weight;
                    cy += block.centroid.y * weight;
                    area += blockArea;
                }
            }
            MomentStatistic result = new MomentStatistic();
            if (area > 0f)
                result.averageIntensity = intensitySum / area;
            if (intensitySum > 0f)
            {
                result.centroid.x = cx / intensitySum;
                result.centroid.y = cy / intensitySum;
            }
            return result;
        }
    }
}
#endif
