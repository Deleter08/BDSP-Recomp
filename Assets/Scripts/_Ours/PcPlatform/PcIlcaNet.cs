#if !SWITCH
// PC platform layer: the game's own IlcaNet native library (statically linked in main, natives @01390770 ... @01392ad4) for
// INL1.IlcaNetNative. NOT shipped code. Each member reproduces the native's path on the emulated console of the play test
// (Ryujinx, guest internet OFF, one user, no NSO licence): nifm never grants a request, so nothing past the network gate runs;
// the BCAT delivery-cache storage mounts (Ryujinx/LibHac create the bcat save for a title with a BCAT storage size) and is empty;
// no NEX / Pia session ever exists. The pure computations (float formats, the RSA-2048/SHA-256 signature check) are bit-exact
// translations of the native code.
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PcPlatform
{
    public static class PcIlcaNet
    {
        // ---- error viewer --------------------------------------------------------------------------------------------------
        // @01390778-ish byte 0x501d778: ErrorViewerDispCancelControl's flag; every native that would open an error applet
        // (nn::err::ShowError / ShowApplicationError) skips it while the flag is set.
        static bool errorViewerCancel;

        public static void ErrorViewerDispCancelControl(bool on)
        {
            errorViewerCancel = on; // @013907a0: strb (w0 & 1)
        }

        // nn::err::ShowError(Result): the error applet. The PC has no applet - it is logged and returns at once (never blocks).
        static void ShowError(uint result)
        {
            PcConfig.Log(string.Format("nn::err::ShowError(0x{0:X8}) module {1} description {2}", result, result & 0x1FF, (result >> 9) & 0x1FFF));
        }

        // @013907b0: skipped while the cancel flag is set; else ApplicationErrorArg {code, dialog, full-screen message, language}
        // and nn::err::ShowApplicationError.
        public static void ShowApplicationError(uint code, string dialog, string screen, string lang)
        {
            if (errorViewerCancel)
                return;
            PcConfig.Log(string.Format("nn::err::ShowApplicationError code {0} lang \"{1}\" dialog \"{2}\" screen \"{3}\"", code, lang, dialog, screen));
        }

        // ---- network clock -------------------------------------------------------------------------------------------------
        // @01390850: nn::time::StandardNetworkSystemClock::GetCurrentTime; failure -> ShowError + 0. Ryujinx sets the network
        // clock up from the local clock context at boot (Horizon.cs SetupStandardNetworkSystemClock(localSystemClockContext, ...)),
        // so on the reference console it succeeds and reads the user clock: the current POSIX time.
        public static long InternetCurrentTime()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // ---- licence (nn::account network-service licence) -----------------------------------------------------------------
        // The emulated console has no network: the async licence load is accepted (@013908a0 returns true, the event is
        // signalled) but completes with a failure, so @01390930 shows that error (unless cancelled) and returns -3.
        static bool licenceLoaded;
        // (124, 59) InternetRequestDenied - the account module's "no network" failure (Ryujinx Account ResultCode).
        const uint LicenceLoadFailure = 124u | (59u << 9);

        public static bool LicLoadAsync(nn.account.UserHandle user)
        {
            // TODO(pc): Ryujinx's LoadNetworkServiceLicenseKindAsync stub completes successfully with Subscribed (kind 1) even with
            // internet off; the brief's console has no NSO licence and no network, which is what this follows.
            licenceLoaded = true;
            return true;
        }

        public static bool LicWait()
        {
            return licenceLoaded; // @01390920: nn::os::TryWaitSystemEvent on the async context's event
        }

        public static int LicGetKind()
        {
            // @01390930: AsyncContext::GetResult fails -> (cancel ? skip : ShowError) -> -3
            if (!errorViewerCancel)
                ShowError(LicenceLoadFailure);
            return -3;
        }

        // ---- BCAT delivery cache -------------------------------------------------------------------------------------------
        static uint mountResult;          // 0x501d77c: the last MountDeliveryCacheStorage result (raw nn::Result)
        static bool mounted;
        static bool networkRequestBusy;   // byte behind GOT 0x4c4b838
        const uint BcatModule = 122;
        const uint BcatTargetAlreadyMounted = BcatModule | (4u << 9);

        public static void BcatMount()
        {
            // @01390c90: g_mountResult = nn::bcat::MountDeliveryCacheStorage(). The empty storage mounts (Success).
            // TODO(pc): a second mount without an unmount - the SDK's answer is assumed to be bcat TargetAlreadyMounted (122, 4);
            // the game never does it (MountStorageAsyncCoreSub2 is guarded by isMount).
            mountResult = mounted ? BcatTargetAlreadyMounted : 0u;
            mounted = true;
        }

        public static int BcatMountCheck2()
        {
            // @01390cc0
            uint r = mountResult;
            if (r == 0)
                return 0;
            if ((r & 0x1FF) == BcatModule)
            {
                if ((r & 0x3FFE00) == 0x600)
                {
                    // bcat TargetLocked (122, 3): mount again, -3 = "retry"
                    BcatMount();
                    return -3;
                }
                if (((r >> 9) & 0x1FFF) - 30u <= 9u)
                {
                    if (!errorViewerCancel)
                        ShowError(r);
                    return -2;
                }
            }
            if (!errorViewerCancel)
                ShowError(r);
            return -1;
        }

        public static void BcatMountCheck3()
        {
            // @01390d80: (cancel ? nothing : ShowError(g_mountResult))
            if (!errorViewerCancel)
                ShowError(mountResult);
        }

        public static void BcatUnmount()
        {
            mounted = false; // @01390da0: nn::bcat::UnmountDeliveryCacheStorage
        }

        public static void BcatGetStart2()
        {
            // @01390dd0: busy flag around nn::nifm::SubmitNetworkRequestAndWait (returns at once: the request is rejected)
            if (networkRequestBusy)
                return;
            networkRequestBusy = true;
            networkRequestBusy = false;
        }

        public static bool BcatGetStart2x1()
        {
            // @01390e20: busy ? false : (busy = true, nn::nifm::SubmitNetworkRequest, true)
            if (networkRequestBusy)
                return false;
            networkRequestBusy = true;
            return true;
        }

        public static bool BcatGetStart2x2()
        {
            // @01390e70: nn::nifm::IsNetworkRequestOnHold() - Ryujinx IRequest.GetRequestState answers Error (not OnHold) with
            // internet off, so the request is finished: busy = false, true.
            networkRequestBusy = false;
            return true;
        }

        public static void BcatEnumerateAll()
        {
            // @013913d0: nn::bcat::EnumerateDeliveryCacheDirectory (NN_ABORT_UNLESS success) - the empty storage lists 0
            // directories, so nothing is opened. Unmounted storage fails the enumeration and the native aborts.
            if (!mounted)
                throw new NnAbortException("IlcaNetNative BCATEnumerateAll: EnumerateDeliveryCacheDirectory failed (delivery cache storage not mounted)");
        }

        // The storage is empty: every DeliveryCacheDirectory::Open / DeliveryCacheFile::Open fails (LibHac: the directory's
        // files.meta is not found), so the natives return their open-failure codes after the argument checks.
        public static int BcatFileCountGet(uint dirNum)
        {
            if (dirNum > 99) return -1;  // @01390fb0
            return -3;                   // @01391010: Open failed
        }

        public static int BcatFileCountListGet(uint dirNum, byte[] fileNameArray)
        {
            if (dirNum > 99) return -2;  // @01391050
            return -3;                   // @013910d4
        }

        public static long BcatFileSizeGet(uint dirNum, uint fileNum)
        {
            if (dirNum > 99) return -1;  // @013911c0
            if (fileNum > 99) return -2;
            return -3;                   // @01391258
        }

        public static int BcatFileRead(uint dirnum, uint filenum, byte[] data, int dataMaxSize)
        {
            if (dirnum > 99) return -1;  // @013912a4
            if (filenum > 99) return -2;
            if (data == null) return -3; // the marshalled pointer of a null array
            if (dataMaxSize < 1) return -4;
            return -5;                   // @0139135c: Open failed
        }

        // ---- inline software keyboard ----------------------------------------------------------------------------------------
        // The natives drive nn::swkbd::InlineKeyboard (@013924a0 Initialize/Appear, @01392900 Calc + text copy, @01392860 Finalize,
        // @01392a30 GetImage). No game code calls INL1.IlcaNetSwkInlineService (its four wrappers have no caller in the binary), so
        // the path is unreachable; this is a minimal Unity-text-entry stand-in so it never throws if it is ever reached.
        static readonly StringBuilder swkbdText = new StringBuilder();
        static bool swkbdActive;
        static bool swkbdDecided;
        static uint swkbdMaxLength;

        public static void SwkbdInitialize(uint textMaxLength)
        {
            swkbdText.Length = 0;
            swkbdMaxLength = textMaxLength;
            swkbdActive = true;
            swkbdDecided = false;
        }

        public static int SwkbdUpdate(byte[] text, ref int size)
        {
            // TODO(pc): the console's Calc() state mapping (flags 0x4c4b920 / 0x4c4b928) is approximated: Enter or Escape decides
            // (1), a finalized keyboard reports 2, otherwise 0.
            if (swkbdActive && !swkbdDecided)
            {
                foreach (char c in Input.inputString)
                {
                    if (c == '\b')
                    {
                        if (swkbdText.Length > 0)
                            swkbdText.Length--;
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        swkbdDecided = true;
                    }
                    else if (swkbdText.Length < swkbdMaxLength)
                    {
                        swkbdText.Append(c);
                    }
                }
                if (Input.GetKeyDown(KeyCode.Escape))
                    swkbdDecided = true;
            }
            // strcpy(text, inputText) and size = strlen (@013929a8)
            byte[] utf8 = Encoding.UTF8.GetBytes(swkbdText.ToString());
            int n = Math.Min(utf8.Length, text.Length - 1);
            Array.Copy(utf8, text, n);
            text[n] = 0;
            size = n;
            if (swkbdDecided)
                return 1;
            return swkbdActive ? 0 : 2;
        }

        public static void SwkbdFinalize()
        {
            swkbdActive = false;
            swkbdDecided = false;
        }

        public static bool SwkbdGetImage(out IntPtr buff, out int size)
        {
            // @01392ab8: no image -> *buff = 0, *size = 0, false (the PC draws no keyboard image)
            buff = IntPtr.Zero;
            size = 0;
            return false;
        }

        // ---- float formats (bit-exact, @01391560 ... @013916a0) --------------------------------------------------------------
        [StructLayout(LayoutKind.Explicit)]
        struct Bits32
        {
            [FieldOffset(0)] public float f;
            [FieldOffset(0)] public uint u;
        }

        static uint FloatBits(float f)
        {
            Bits32 b = default(Bits32);
            b.f = f;
            return b.u;
        }

        static float BitsFloat(uint u)
        {
            Bits32 b = default(Bits32);
            b.u = u;
            return b.f;
        }

        public static ushort FP32toFP16(float src)
        {
            // @01391560: round half up at bit 12, normal / denormal / overflow lanes OR-ed under their exponent conditions
            uint w8 = FloatBits(src) + 0x1000u;
            uint e = (w8 >> 23) & 0xFFu;
            uint normal = ((((w8 >> 23) << 10) + 0x4000u) & 0x7C00u) | ((w8 >> 13) & 0x3FFu);
            uint denormal = ((((w8 & 0x7FFFFFu) + 0x7FF000u) >> (int)(0x7Du - e)) + 1u) >> 1;
            uint r = (e > 0x8Fu ? 0x7FFFu : 0u) | ((w8 >> 16) & 0x8000u) | (e > 0x70u ? normal : 0u) | (e - 0x66u < 0xBu ? denormal : 0u);
            return (ushort)r;
        }

        public static float FP16toFP32(ushort src)
        {
            // @013915e0
            uint h = src;
            uint m13 = (h << 13) & 0x7FE000u;
            uint exp = (h >> 10) & 0x1Fu;
            uint fbits = FloatBits((float)m13);                      // ucvtf (exact: m13 < 2^23)
            uint shifted = m13 << (int)(0x96u - (fbits >> 23));
            uint normal = exp == 0 ? 0u : ((m13 | (exp << 23)) + 0x38000000u);
            normal = (normal & 0x7FFFFFFFu) | ((h >> 15) << 31);     // bfi w8, w9, #31, #1
            uint denormal = (m13 != 0 && exp == 0) ? ((shifted & 0x7FE000u) | ((fbits - 0x12800000u) & 0xFF800000u)) : 0u;
            return BitsFloat(normal | denormal);
        }

        public static uint FP32toFP24(float src)
        {
            return FloatBits(src) >> 8; // @01391650
        }

        public static float FP24toFP32(uint src)
        {
            return BitsFloat((src << 8) | 0x80u); // @01391660: bfi of the 24 bits over 0x80
        }

        public static float BToFP32(uint src)
        {
            return BitsFloat(src); // @01391670: fmov s0, w0
        }

        public static uint FP32ToB(float src)
        {
            return FloatBits(src); // @01391680
        }

        public static double BToFP64(ulong src)
        {
            return BitConverter.Int64BitsToDouble((long)src); // @01391690
        }

        public static ulong FP64ToB(double src)
        {
            return (ulong)BitConverter.DoubleToInt64Bits(src); // @013916a0
        }

        // ---- signature (@013909d0): RSA-2048 PKCS#1 v1.5 with SHA-256 over an X.509 SubjectPublicKeyInfo -------------------
        static readonly byte[] Sha256DigestInfo =
        {
            0x30, 0x31, 0x30, 0x0D, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00, 0x04, 0x20,
        };

        public static bool VerifySignature(byte[] pPublicKey, int publicKeysize, byte[] pPokemonDataList, int pokemonListSize, byte[] pSignature)
        {
            // Modulus start: the byte after the third zero byte of the key, scanned in pairs over the first 0x100 bytes (the
            // SPKI's "05 00", "03 82 01 0F 00", "02 82 01 01 00"); key + 0x100 when fewer than three zeros are found.
            int start = 0x100;
            int zeros = 0;
            for (int i = 0; i < 0x100; i++)
            {
                if (KeyByte(pPublicKey, i) == 0 && ++zeros == 3)
                {
                    start = i + 1;
                    break;
                }
            }
            byte[] modulus = new byte[0x100];
            for (int i = 0; i < 0x100; i++)
                modulus[i] = KeyByte(pPublicKey, start + i);
            // exponent: the last 3 bytes of the key (BigNum::Set(key + size - 3, 3))
            byte[] exponent = new byte[3];
            for (int i = 0; i < 3; i++)
                exponent[i] = KeyByte(pPublicKey, publicKeysize - 3 + i);

            byte[] hash;
            using (SHA256 sha = SHA256.Create())
                hash = sha.ComputeHash(pPokemonDataList, 0, pokemonListSize);

            BigInteger n = BigEndian(modulus);
            BigInteger e = BigEndian(exponent);
            if (n.IsZero)
                return false; // BigNum::ModExp with an empty modulus fails
            // TODO(pc): nn::crypto BigNum::ModExp's handling of a signature >= modulus is not visible (SDK code); reduced mod n here.
            byte[] sig = new byte[0x100];
            Array.Copy(pSignature, sig, Math.Min(pSignature.Length, 0x100));
            BigInteger m = BigInteger.ModPow(BigEndian(sig), e, n);
            byte[] em = ToBigEndian(m, 0x100);

            // 00 01 FF*202 00 DigestInfo(19) hash(32)
            if (em[0] != 0 || em[1] != 1)
                return false;
            for (int i = 2; i < 0xCC; i++)
                if (em[i] != 0xFF)
                    return false;
            if (em[0xCC] != 0)
                return false;
            for (int i = 0; i < Sha256DigestInfo.Length; i++)
                if (em[0xCD + i] != Sha256DigestInfo[i])
                    return false;
            for (int i = 0; i < 0x20; i++)
                if (em[0xE0 + i] != hash[i])
                    return false;
            return true;
        }

        // The native reads raw memory; past the managed key's end the PC reads zero.
        static byte KeyByte(byte[] key, int i)
        {
            return (i >= 0 && i < key.Length) ? key[i] : (byte)0;
        }

        static BigInteger BigEndian(byte[] be)
        {
            byte[] le = new byte[be.Length + 1];
            for (int i = 0; i < be.Length; i++)
                le[i] = be[be.Length - 1 - i];
            return new BigInteger(le);
        }

        static byte[] ToBigEndian(BigInteger v, int size)
        {
            byte[] le = v.ToByteArray();
            byte[] be = new byte[size];
            for (int i = 0; i < size && i < le.Length; i++)
                be[size - 1 - i] = le[i];
            return be;
        }

        // ---- NEX trade (@01391f90 ... @01392200) ----------------------------------------------------------------------------
        // The NEX protocol client (GOT 0x4c4b878) only exists after a NEX login, which needs the network: NEXTradeObject resets
        // the result buffers and the point (0x501d840) and returns false; the async status (0x501d85c) is never set.
        static ushort tradePoint;
        static int tradeStatus;

        public static bool NexTradeObject()
        {
            tradePoint = 0;
            return false;
        }

        public static int NexTradeObjectPolling()
        {
            // @01392170: status 2..4 -> {1, -1, -2}, else 0
            switch (tradeStatus)
            {
                case 2: return 1;
                case 3: return -1;
                case 4: return -2;
                default: return 0;
            }
        }

        public static void NexTradeObjectSuccessGet(ref int core_binarysize, ref int signature_binarysize, ref ushort point)
        {
            // @01392200: copies the two (empty) result vectors - nothing is copied for a size < 1 - and the point
            core_binarysize = 0;
            signature_binarysize = 0;
            point = tradePoint;
        }
    }
}
#endif
