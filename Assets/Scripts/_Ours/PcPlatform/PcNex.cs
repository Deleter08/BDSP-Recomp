#if !SWITCH
// PC platform layer (play-test campaign Phase 5, package s33b): NOT shipped code. Emulates the NEX plugin native library that the
// shipped game links statically into main (plugin entry points 0x15adf90..0x15b2e00, the plugin singleton returned by 0x15d2b44,
// NintendoSDK-NEX OnlineCore underneath) as it behaves on a console WITHOUT a network connection (the Ryujinx reference: guest
// internet off). NexPlugin\*.cs declare the natives as `extern`; their `#else` twins call in here.
//
// Plugin singleton fields reproduced: +0x48 plugin initialised, +0x49 network connected (nn::nifm::IsNetworkAvailable cached),
// +0x4b NEX initialised, +0x4c thread mode, +0x50 thread core no., +0x18 / +0x20 the last result / notification arrays handed to
// managed code, and the two async-result lists (0x15ade10 = ready, 0x15ade20 = handed out, waiting for InitAsyncResultByAsyncId).
//
// Offline facts (from the native code):
// - InitializeNexPlugin needs no network: allocator + list setup, sets +0x48, returns true.
// - InitializeNex / the InitializeNexAsync thread run the core init 0x15c2500 -> 0x15c2460: nn::nifm::Initialize, then
//   IsNetworkAvailable == false -> SubmitNetworkRequestAndWait -> still unavailable -> HandleNetworkRequestErrorResult (the console
//   shows the network error applet here) -> the AsyncResult gets nnResult = 0 (success!), errorCode = 0x80000000, and the init
//   returns false. +0x4b (NEX initialised) therefore never becomes true offline, so every NEX service call (DataStore, Ranking,
//   Ranking2, Subscriber, Utility, SmartDeviceVoiceChat, NGS login/logout/test) is rejected by its gate and returns false.
// - InitializeNexAsync returns true (the init thread starts) and queues one COMMON_INITIALIZENEX (90) result carrying that failure;
//   UpdateAsyncResult hands it to managed code, whose AsyncResult.IsFailure() is true (errorCode bit 31).
// - NgsFacade.IsConnected(null facade) = nn::Result 0xCD32 (module 306, description 102: qResult(false) = 0x80010001 mapped by
//   0x14ffea4/0x150307c); GetPrincipalID(null) = 0.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PcPlatform
{
    public static class PcNex
    {
        // AsyncResultNum values the plugin native itself produces.
        const int AsyncNumInitializeNex = 90;      // 0x5a (0x15c2b60 / 0x15c2770: AsyncResultInt ctor 0x15d292c(.., asyncId, 0x5a))

        // Native AsyncResultInt (vtable + 0x28 bytes of fields, element stride 0x30), the layout Detail.AsyncResultInt mirrors.
        const int AsyncResultSize = 0x30;

        struct NativeAsyncResult
        {
            public uint asyncId;      // +0x08
            public int asyncNum;      // +0x0c
            public uint nnResult;     // +0x10
            public uint netErrCode;   // +0x14
            public int returnCode;    // +0x18
            public uint errorCode;    // +0x1c
        }

        static readonly object s_lock = new object();     // the plugin mutex (singleton +0x28)
        static readonly List<NativeAsyncResult> s_ready = new List<NativeAsyncResult>();       // 0x15ade10
        static readonly List<NativeAsyncResult> s_handedOut = new List<NativeAsyncResult>();   // 0x15ade20

        static bool s_pluginInitialized;   // +0x48
        static bool s_networkConnected;    // +0x49
        static bool s_nexInitialized;      // +0x4b
        static int s_threadMode;           // +0x4c
        static int s_coreNo;               // +0x50
        static bool s_terminateImmediately;
        static IntPtr s_resultArray;       // +0x18 (CppArray header) - freed on the next UpdateAsyncResultInt
        static IntPtr s_notificationArray; // +0x20

        // ---- Common ------------------------------------------------------------------------------------------------------------

        // 0x15adfe0
        public static bool IsInitializedNex() { return s_nexInitialized; }

        // 0x15ae000
        public static bool IsNetworkConnected() { return s_networkConnected; }

        // 0x15adf90 -> 0x15c2950
        public static bool InitializeNexPlugin(uint pluginMemSize)
        {
            lock (s_lock)
            {
                if (s_pluginInitialized)
                    return true;
                s_ready.Clear();
                s_handedOut.Clear();
                s_pluginInitialized = true;
                PcConfig.Log("NEX plugin initialised (" + pluginMemSize + " bytes)");
                return true;
            }
        }

        // 0x15adfd0 -> 0x15c2b00 (the allocator teardown 0x15c2f24 always returns 0, so this succeeds)
        public static bool FinalizeNexPlugin()
        {
            lock (s_lock)
            {
                if (!s_pluginInitialized)
                    return true;
                FreeResultArray();
                FreeNotificationArray();
                s_ready.Clear();
                s_handedOut.Clear();
                s_pluginInitialized = false;
                return true;
            }
        }

        // 0x15ae070 -> 0x15c2b60: the synchronous core init; its AsyncResult is discarded.
        public static bool InitializeNex(int threadMode)
        {
            if (!s_pluginInitialized)
                return false;
            s_threadMode = threadMode;
            NativeAsyncResult res = NewAsyncResult(0, AsyncNumInitializeNex);
            return InitializeNexCore(ref res);
        }

        // 0x15ae020 -> 0x15c2bb0: start the init thread (0x15c2770), which runs the core init and queues its result. The console
        // thread finishes as soon as nifm reports no network; the PC runs it inline.
        public static bool InitializeNexAsync(uint asyncId, int threadMode)
        {
            if (!s_pluginInitialized)
                return false;
            s_threadMode = threadMode;
            if (s_nexInitialized)
                return false;
            NativeAsyncResult res = NewAsyncResult(asyncId, AsyncNumInitializeNex);
            InitializeNexCore(ref res);
            lock (s_lock)
                s_ready.Add(res);
            return true;
        }

        // 0x15ae0f0 -> 0x15c28d0 (needs NEX initialised)
        public static bool FinalizeNex()
        {
            if (!s_pluginInitialized)
                return false;
            if (!s_nexInitialized)
                return false;
            throw Unsupported.Api("NexPlugin FinalizeNex with NEX initialised");
        }

        // 0x15ae0b0 -> 0x15c2c80 (needs NEX initialised)
        public static bool FinalizeNexAsync(uint asyncId)
        {
            if (!s_pluginInitialized)
                return false;
            if (!s_nexInitialized)
                return false;
            throw Unsupported.Api("NexPlugin FinalizeNexAsync with NEX initialised");
        }

        // 0x15ae140
        public static bool SetThreadCoreNo(int coreNo)
        {
            if (!s_pluginInitialized)
                return false;
            s_coreNo = coreNo;
            return true;
        }

        // 0x15c14d0 / 0x15c1580: the flag of the NEX Core object (+0x50) behind the OnlineCore instance table.
        // TODO(pc): before InitializeNex the console's instance-table slot may be empty (the getter dereferences it unchecked);
        // the game only calls the setter path through an emptied helper, so a plain flag is kept.
        public static void SetTerminateImmediately(uint terminate) { s_terminateImmediately = terminate == 1; }

        public static bool GetTerminateImmediately() { return s_terminateImmediately; }

        // 0x15c1640 / 0x15c1790 / 0x15c18e0: the NEX scheduler only exists once NEX is initialised.
        public static uint Dispatch(uint dispatchTimeout, uint dispatchFlags)
        {
            if (!s_nexInitialized)
                return 0;
            throw Unsupported.Api("NexPlugin Dispatch with NEX initialised");
        }

        public static void DispatchAll()
        {
            if (!s_nexInitialized)
                return;
            throw Unsupported.Api("NexPlugin DispatchAll with NEX initialised");
        }

        public static uint GetReadyJobsSize()
        {
            if (!s_nexInitialized)
                return 0;
            throw Unsupported.Api("NexPlugin GetReadyJobsSize with NEX initialised");
        }

        // 0x15aea10: allocator dump (debug output only).
        public static void DumpMemory() { }

        // 0x15ae1c0: hand the ready results to managed code as a CppArray of AsyncResultInt; they move to the handed-out list.
        public static bool UpdateAsyncResult(ref IntPtr pAsyncResult)
        {
            if (!s_pluginInitialized)
                return false;
            lock (s_lock)
            {
                RefreshNetworkConnected();
                FreeResultArray();
                int count = s_ready.Count;
                if (count == 0)
                    return true;
                IntPtr header = Marshal.AllocHGlobal(0x10);
                IntPtr items = Marshal.AllocHGlobal(count * AsyncResultSize);
                Marshal.WriteInt32(header, 0, count);
                Marshal.WriteInt32(header, 4, 0);
                Marshal.WriteIntPtr(header, 8, items);
                for (int i = 0; i < count; i++)
                {
                    NativeAsyncResult r = s_ready[i];
                    IntPtr e = new IntPtr(items.ToInt64() + i * AsyncResultSize);
                    Marshal.WriteInt64(e, 0x00, 0);
                    Marshal.WriteInt32(e, 0x08, (int)r.asyncId);
                    Marshal.WriteInt32(e, 0x0c, r.asyncNum);
                    Marshal.WriteInt32(e, 0x10, (int)r.nnResult);
                    Marshal.WriteInt32(e, 0x14, (int)r.netErrCode);
                    Marshal.WriteInt32(e, 0x18, r.returnCode);
                    Marshal.WriteInt32(e, 0x1c, (int)r.errorCode);
                    Marshal.WriteIntPtr(e, 0x20, IntPtr.Zero);
                    Marshal.WriteIntPtr(e, 0x28, IntPtr.Zero);
                    s_handedOut.Add(r);
                }
                s_ready.Clear();
                s_resultArray = header;
                pAsyncResult = header;
                return true;
            }
        }

        // 0x15ae6f0: drop a result once managed code has consumed it (handed-out list first, then the ready list).
        public static void InitAsyncResultByAsyncId(uint asyncId)
        {
            lock (s_lock)
            {
                int i = s_handedOut.FindIndex(r => r.asyncId == asyncId);
                if (i >= 0)
                {
                    s_handedOut.RemoveAt(i);
                    return;
                }
                i = s_ready.FindIndex(r => r.asyncId == asyncId);
                if (i >= 0)
                    s_ready.RemoveAt(i);
            }
        }

        // 0x15ae440: notification events come from the NEX server only - offline the list is always empty (true, out untouched).
        public static bool GetNotificationEvent(ref IntPtr pNotificationEvent)
        {
            if (!s_pluginInitialized)
                return false;
            lock (s_lock)
            {
                RefreshNetworkConnected();
                FreeNotificationArray();
                return true;
            }
        }

        // ---- NgsFacade -----------------------------------------------------------------------------------------------------------

        // 0x15b0620: the result of qResult(false) = 0x80010001 (Core::Unknown) converted to nn::Result by 0x150307c.
        public static readonly nn.Result ResultNotConnected = new nn.Result(306, 102);   // innerValue 0xCD32

        public static nn.Result IsConnected(IntPtr pNgsFacade)
        {
            // A facade only comes from a successful NGS login, which needs NEX initialised - never the case offline.
            return ResultNotConnected;
        }

        // 0x15b0680: facade == null -> 0 (else facade->[0xc0]->[0x10]).
        public static ulong GetPrincipalID(IntPtr pNgsFacade) { return 0; }

        // ---- NEX service calls --------------------------------------------------------------------------------------------------

        // The gate every NEX service native starts with: plugin initialised (+0x48), facade non-null where it takes one, NEX
        // initialised (+0x4b) (Ngs_LoginAsync additionally +0x49). Offline the last test always fails, so the native returns false
        // without touching its out parameters. The pass branch cannot be reached on PC (NEX init needs the network).
        public static bool ServiceCall(string api, IntPtr pNgsFacade)
        {
            if (!s_pluginInitialized || pNgsFacade == IntPtr.Zero || !s_nexInitialized)
                return false;
            throw Unsupported.Api(api);
        }

        public static bool ServiceCall(string api)
        {
            if (!s_pluginInitialized || !s_nexInitialized)
                return false;
            throw Unsupported.Api(api);
        }

        // 0x15b1250 Ranking_LocalUpdate: plugin not initialised -> 0 (UPDATE_SUCCESS); a null argument -> 2 (UPDATE_ERROR); else the
        // local re-ranking of a cached server result 0x15c6a50 (no cached result can exist without a server).
        public static int RankingLocalUpdate(string api)
        {
            if (!s_pluginInitialized)
                return 0;
            throw Unsupported.Api(api);
        }

        // Natives that free a buffer a NEX service result allocated (none exists offline); a null pointer is a no-op there too.
        public static void FreeServiceResult(string api, IntPtr p)
        {
            if (p == IntPtr.Zero)
                return;
            PcConfig.Log("NEX " + api + ": no such buffer on PC (" + p + ")");
        }

        // ---- Utility ------------------------------------------------------------------------------------------------------------

        // 0x15b2e00 -> 0x15d24c0 -> 0x1675ba8: a NEX unique id is valid when its top 16 bits equal CRC-16/CCITT-FALSE (poly 0x1021,
        // init 0xFFFF, no reflection, no final xor; table 0x3e69aee) of its low 48 bits taken as 6 little-endian bytes. The CRC is
        // stored little-endian and compared with memcmp against the top 16 bits stored the same way, i.e. a plain ushort compare.
        public static bool IsValidNexUniqueId(ulong nexUniqueId)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < 6; i++)
            {
                byte b = (byte)(nexUniqueId >> (8 * i));
                crc = (ushort)(CrcTable[((crc >> 8) ^ b) & 0xFF] ^ (crc << 8));
            }
            return (ushort)(nexUniqueId >> 48) == crc;
        }

        static readonly ushort[] CrcTable = BuildCrcTable();

        static ushort[] BuildCrcTable()
        {
            ushort[] t = new ushort[256];
            for (int i = 0; i < 256; i++)
            {
                int c = i << 8;
                for (int k = 0; k < 8; k++)
                    c = (c & 0x8000) != 0 ? ((c << 1) ^ 0x1021) & 0xFFFF : (c << 1) & 0xFFFF;
                t[i] = (ushort)c;
            }
            return t;
        }

        // ---- internals ----------------------------------------------------------------------------------------------------------

        // 0x15d292c: vtable, asyncId, asyncNum, nnResult/netErrCode = 0, returnCode = 0, errorCode = 0x80000000, params = 0.
        static NativeAsyncResult NewAsyncResult(uint asyncId, int asyncNum)
        {
            NativeAsyncResult r = new NativeAsyncResult();
            r.asyncId = asyncId;
            r.asyncNum = asyncNum;
            r.errorCode = 0x80000000;
            return r;
        }

        // 0x15c2500 / 0x15c2460 on a console without network (see the header).
        static bool InitializeNexCore(ref NativeAsyncResult res)
        {
            if (s_nexInitialized)
                return false;
            res.errorCode = 0;
            // nn::nifm::Initialize() succeeds; IsNetworkAvailable() is false; SubmitNetworkRequestAndWait() fails;
            // HandleNetworkRequestErrorResult() shows the console's network error applet (logged here) and asks for no retry.
            PcConfig.Log("NEX init: nifm reports no network (console: network connection error applet) - NEX stays uninitialised");
            res.nnResult = 0;
            res.errorCode = 0x80000000;
            return false;
        }

        // 0x15c2720
        static void RefreshNetworkConnected()
        {
            // s_nexInitialized ? nn::nifm::IsNetworkAvailable() : false  - false either way offline.
            s_networkConnected = false;
        }

        static void FreeResultArray()
        {
            if (s_resultArray == IntPtr.Zero)
                return;
            IntPtr items = Marshal.ReadIntPtr(s_resultArray, 8);
            if (items != IntPtr.Zero)
                Marshal.FreeHGlobal(items);
            Marshal.FreeHGlobal(s_resultArray);
            s_resultArray = IntPtr.Zero;
        }

        static void FreeNotificationArray()
        {
            if (s_notificationArray == IntPtr.Zero)
                return;
            Marshal.FreeHGlobal(s_notificationArray);
            s_notificationArray = IntPtr.Zero;
        }
    }
}
#endif
