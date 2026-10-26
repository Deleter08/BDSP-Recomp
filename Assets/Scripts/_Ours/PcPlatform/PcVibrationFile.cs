#if !SWITCH
// PC platform layer: nn::hid vibration files (.bnvib) - pure computation, ported bit-exactly from the SDK module
// (work/sdk_flat.bin). NOT shipped code. The game itself never calls these (no caller of nn.hid.VibrationFile in the binary).
//   Parse         nn::hid::ParseVibrationFile @225560 -> detail @220110
//   RetrieveValue nn::hid::RetrieveVibrationValue @2255f0 -> detail @220240 (frequency table @a6c0cc)
//   Generate      nn::hid::GenerateVibrationFile @2256a0 -> detail @2202d0 (log2f @552ed0, the FreeBSD/musl e_log2f.c)
//   CalculateSize nn::hid::CalculateVibrationFileSize @2257a0 -> detail @2204c0
// File layout: u32 metaSize | u16 formatId (3) | u16 samplingRate (200) | [u32 loopStart, u32 loopEnd [, u32 loopInterval]] |
// u32 dataSize | dataSize bytes = sampleLength samples of 4 bytes (ampLow, freqLow code, ampHigh, freqHigh code).
// The parser context: the SDK keeps a raw pointer to the sample data at +0 and sampleLength at +0xc. Here +0 holds a weak GCHandle
// to the parsed byte[] (non-null like the pointer; Unity's GC does not move arrays either) and +8 the data offset.
using System;
using System.Runtime.InteropServices;
using nn;
using nn.hid;

namespace PcPlatform
{
    public static class PcVibrationFile
    {
        // nn::hid ResultInvalid range (202, 140..150): the detail parser's codes
        static readonly Result ResultHeaderTooSmall = new Result(202, 141); // 0x11aca: metaSize < 4 or the file shorter than metaSize + 8
        static readonly Result ResultBadFormat = new Result(202, 142);      // 0x11cca: formatId != 3
        static readonly Result ResultBadRate = new Result(202, 143);        // 0x11eca: samplingRate != 200
        static readonly Result ResultDataTruncated = new Result(202, 144);  // 0x120ca: metaSize + 8 + dataSize > fileSize
        static readonly Result ResultBadLoop = new Result(202, 145);        // 0x122ca: loopStart > loopEnd, interval < 0, loopEnd > length

        static readonly float[] FrequencyTable =
        {
            1.0f, 1.0218969583511353f, 1.0442739725112915f, 1.067139983177185f, 1.090507984161377f, 1.114387035369873f,
            1.1387890577316284f, 1.1637250185012817f, 1.1892069578170776f, 1.2152470350265503f, 1.2418580055236816f, 1.2690509557724f,
            1.296839952468872f, 1.3252370357513428f, 1.3542560338974f, 1.3839099407196045f, 1.414214015007019f, 1.4451810121536255f,
            1.4768259525299072f, 1.5091639757156372f, 1.5422110557556152f, 1.5759810209274292f, 1.6104899644851685f, 1.6457550525665283f,
            1.681792974472046f, 1.7186189889907837f, 1.7562520503997803f, 1.7947089672088623f, 1.834007978439331f, 1.874168038368225f,
            1.915207028388977f, 1.957144021987915f
        };

        static uint U32(byte[] a, long at) { return (uint)(a[at] | a[at + 1] << 8 | a[at + 2] << 16 | a[at + 3] << 24); }
        static ushort U16(byte[] a, long at) { return (ushort)(a[at] | a[at + 1] << 8); }

        public static Result Parse(ref VibrationFileInfo info, ref VibrationFileParserContext ctx, byte[] address, long fileSize)
        {
            if (address == null || fileSize == 0)
                throw new NnAbortException("nn::hid::ParseVibrationFile: address != nullptr && fileSize > 0");
            uint meta = U32(address, 0);
            info.metaDataSize = meta;
            info.formatId = U16(address, 4);
            info.samplingRate = U16(address, 6);
            if (meta < 4)
                return ResultHeaderTooSmall;
            if (info.formatId != 3)
                return ResultBadFormat;
            if (info.samplingRate != 200)
                return ResultBadRate;
            bool loop;
            if (meta < 0xc)
            {
                loop = false;
                info.isLoop = 0;
            }
            else
            {
                info.isLoop = 1;
                info.loopInterval = 0;
                info.loopStartPosition = U32(address, 8);
                info.loopEndPosition = U32(address, 0xc);
                if (info.loopStartPosition > info.loopEndPosition)
                    return ResultBadLoop;
                if (meta >= 0x10)
                {
                    info.loopInterval = U32(address, 0x10);
                    if ((int)info.loopInterval < 0)
                        return ResultBadLoop;
                }
                loop = true;
            }
            int dataAt = (int)meta + 8;
            if (dataAt > (int)fileSize)
                return ResultHeaderTooSmall;
            uint dataSize = U32(address, (int)meta + 4);
            uint sampleLength = dataSize >> 2;
            info.dataSize = dataSize;
            info.sampleLength = (int)sampleLength;
            if (loop)
            {
                if (info.loopEndPosition > sampleLength)
                    return ResultBadLoop;
            }
            else
            {
                info.loopStartPosition = 0;
                info.loopEndPosition = sampleLength;
                info.loopInterval = 0;
            }
            GCHandle handle = GCHandle.Alloc(address, GCHandleType.Weak);
            ctx._storage[0] = GCHandle.ToIntPtr(handle);
            ctx._storage[1] = (IntPtr)((long)sampleLength << 32 | (uint)dataAt);
            if ((int)(dataSize + (uint)dataAt) > (int)fileSize)
                return ResultDataTruncated;
            return new Result();
        }

        public static void RetrieveValue(ref VibrationValue value, int position, ref VibrationFileParserContext ctx)
        {
            long packed = (long)ctx._storage[1];
            int sampleLength = (int)(packed >> 32);
            if (ctx._storage[0] == IntPtr.Zero || position < 0 || sampleLength <= position)
                throw new NnAbortException("nn::hid::RetrieveVibrationValue: 0 <= position < sampleLength");
            byte[] data = (byte[])GCHandle.FromIntPtr(ctx._storage[0]).Target;
            int at = (int)(uint)packed + position * 4;
            value.amplitudeLow = (float)data[at] / 255f;
            value.frequencyLow = FrequencyTable[data[at + 1] & 0x1f] * (float)(10 << (data[at + 1] >> 5));
            value.amplitudeHigh = (float)data[at + 2] / 255f;
            value.frequencyHigh = FrequencyTable[data[at + 3] & 0x1f] * (float)(10 << (data[at + 3] >> 5));
        }

        public static long CalculateSize(VibrationValueArrayInfo info)
        {
            return (uint)Size(info);
        }

        static int Size(VibrationValueArrayInfo info)
        {
            int header = info.isLoop ? (info.loopInterval == 0 ? 0x14 : 0x18) : 0xc;
            return header + (info.sampleLength << 2);
        }

        public static void Generate(ref long pOutSize, byte[] outBuffer, long bufferSize, VibrationValueArrayInfo info, VibrationValue[] pValues)
        {
            if (outBuffer == null || bufferSize == 0 || (ulong)CalculateSize(info) > (ulong)bufferSize || info.sampleLength <= 0
                || info.loopStartPosition > info.loopEndPosition || info.loopEndPosition > (uint)info.sampleLength
                || (int)info.loopInterval < 0 || pValues == null)
                throw new NnAbortException("nn::hid::GenerateVibrationFile: precondition");
            int size = Size(info);
            pOutSize = 0;
            int at;
            if (info.isLoop)
            {
                Put32(outBuffer, 0, info.loopInterval == 0 ? 0xcu : 0x10u);
                Put32(outBuffer, 4, 0x00c80003u);
                Put32(outBuffer, 8, info.loopStartPosition);
                Put32(outBuffer, 0xc, info.loopEndPosition);
                at = 0x10;
                if (info.loopInterval != 0)
                {
                    Put32(outBuffer, 0x10, info.loopInterval);
                    at = 0x14;
                }
            }
            else
            {
                Put32(outBuffer, 0, 4u);
                Put32(outBuffer, 4, 0x00c80003u);
                at = 8;
            }
            Put32(outBuffer, at, (uint)info.sampleLength << 2);
            at += 4;
            for (int i = 0; i < info.sampleLength; i++)
            {
                VibrationValue v = pValues[i];
                outBuffer[at++] = Quantize((float)(v.amplitudeLow * 255f));
                outBuffer[at++] = Quantize((float)(Log2f((float)(v.frequencyLow / 10f)) * 32f));
                outBuffer[at++] = Quantize((float)(v.amplitudeHigh * 255f));
                outBuffer[at++] = Quantize((float)(Log2f((float)(v.frequencyHigh / 10f)) * 32f));
            }
            pOutSize = size;
        }

        static void Put32(byte[] a, int at, uint v)
        {
            a[at] = (byte)v;
            a[at + 1] = (byte)(v >> 8);
            a[at + 2] = (byte)(v >> 16);
            a[at + 3] = (byte)(v >> 24);
        }

        // frinta (nearest, ties away), fmaxnm 0 (NaN -> 0), fminnm 255, fcvtzs
        static byte Quantize(float v)
        {
            if (float.IsNaN(v))
                return 0;
            double r = Math.Round((double)v, MidpointRounding.AwayFromZero);
            if (r < 0.0) r = 0.0;
            if (r > 255.0) r = 255.0;
            return (byte)(int)r;
        }

        static float BitsToFloat(uint i) { return BitConverter.ToSingle(BitConverter.GetBytes(i), 0); }
        static uint FloatToBits(float f) { return BitConverter.ToUInt32(BitConverter.GetBytes(f), 0); }

        static readonly float Ivln2hi = BitsToFloat(0x3fb8b000);
        static readonly float Ivln2lo = BitsToFloat(0xb9389ad4);
        static readonly float Lg1 = BitsToFloat(0x3f2aaaaa);
        static readonly float Lg2 = BitsToFloat(0x3ecccE13);
        static readonly float Lg3 = BitsToFloat(0x3e91e9ee);
        static readonly float Lg4 = BitsToFloat(0x3e789e26);

        // log2f @552ed0 (e_log2f.c), every operation rounded to single precision as the AArch64 code does (no fused multiply-add).
        static float Log2f(float x)
        {
            uint ix = FloatToBits(x);
            int k = 0;
            if (ix < 0x00800000 || (ix >> 31) != 0)
            {
                if ((ix << 1) == 0)
                    return -1f / (float)(x * x);
                // (x - x) / 0: a NaN input propagates (quietened); otherwise AArch64 yields the default NaN 0x7fc00000 (x86 would
                // give the negative 0xffc00000, so it is written out)
                if ((ix >> 31) != 0)
                    return float.IsNaN(x) ? (float)(x - x) / 0f : BitsToFloat(0x7fc00000);
                k -= 25;
                x = (float)(x * 33554432f); // 0x1p25f
                ix = FloatToBits(x);
            }
            else if (ix >= 0x7f800000)
            {
                return x;
            }
            else if (ix == 0x3f800000)
            {
                return 0f;
            }
            ix += 0x3f800000 - 0x3f3504f3;
            k += (int)(ix >> 23) - 0x7f;
            ix = (ix & 0x007fffff) + 0x3f3504f3;
            x = BitsToFloat(ix);

            float f = (float)(x - 1f);
            float s = (float)(f / (float)(2f + f));
            float z = (float)(s * s);
            float w = (float)(z * z);
            float t1 = (float)(w * (float)(Lg2 + (float)(w * Lg4)));
            float t2 = (float)(z * (float)(Lg1 + (float)(w * Lg3)));
            float R = (float)(t2 + t1);
            float hfsq = (float)((float)(0.5f * f) * f);
            float hi = (float)(f - hfsq);
            hi = BitsToFloat(FloatToBits(hi) & 0xfffff000);
            float lo = (float)((float)((float)(f - hi) - hfsq) + (float)(s * (float)(hfsq + R)));
            float sum = (float)((float)((float)(lo + hi) * Ivln2lo) + (float)(lo * Ivln2hi));
            sum = (float)((float)(hi * Ivln2hi) + sum);
            return (float)(sum + (float)k);
        }
    }
}
#endif
