#if !SWITCH
// PC platform layer: the Pia middleware's Unity plugin layer (PiaPlugin* natives @0156bf60 ... @0156da00, glue @0156da84 ...
// @01578e70, statically linked in main). NOT shipped code. It emulates the console of the play test: no network, no
// local-wireless radio. What the native glue establishes and this file reproduces:
//   - every glue call first reads the framework instance (GOT 0x4c4e008) and, for transport / session / sync / clone /
//     reckoning, the protocol or session object behind it; while that is null it returns its "no instance" value:
//     Result 0x10C08 (ResultInvalidState, ShouldNotUse, ProgrammingError), false / 0 / -1.0f, no out writes.
//   - the instance exists from a successful InitializeFramework (pia::framework::Initialize @01687d30) until FinalizeAll
//     (@0156edf0); a second InitializeFramework fails with 0x10C02 (ResultAlreadyInitialized) and the glue then runs
//     FinalizeAll (@0156eaf0).
//   - the protocols and the session only come into being as the join process climbs past NetworkStartedUp, which needs a
//     network: on the emulated console ChangeStateAsync(NetworkStartedUp) starts and its async completes with a failure, so
//     everything above stays at the "no instance" values for the whole run.
// pia Result -> PiaPlugin.Result (@0156da84): resultValue = the raw value, viewerType = (value >> 10) & 7,
// handlingType = (value >> 13) & 0x1F, innerErrorCode = the ErrorCode carried by the result (none for these values).
using System;
using System.Reflection;

namespace PcPlatform
{
    public static class PcPia
    {
        public const uint ResultSuccess = 0;
        public const uint ResultAlreadyInitialized = 0x10C02;       // 68610
        public const uint ResultInvalidArgument = 0x10C07;          // 68615
        public const uint ResultInvalidState = 0x10C08;             // 68616
        public const uint ResultNotInitialized = 0x10C0C;           // 68620
        public const uint ResultNetworkConnectionIsLost = 0xC406;   // 50182: ShouldUse, ShutdownNetwork
        public const uint ResultWifiOff = 0xC420;                   // 50208: ShouldUse, ShutdownNetwork

        public static PiaPlugin.Result Make(uint value)
        {
            return new PiaPlugin.Result((PiaPlugin.ResultValue)value, 0, (PiaPlugin.ViewerType)((value >> 10) & 7), (PiaPlugin.HandlingType)((value >> 13) & 0x1F));
        }

        public static PiaPlugin.Result Success { get { return Make(ResultSuccess); } }
        public static PiaPlugin.Result InvalidState { get { return Make(ResultInvalidState); } }
        public static PiaPlugin.Result InvalidArgument { get { return Make(ResultInvalidArgument); } }

        // ---- framework state ------------------------------------------------------------------------------------------------
        static bool initialized;                 // framework instance (GOT 0x4c4e008) != null
        static PiaPlugin.NetworkType networkType; // 0x4c4e060, stored by InitializeFramework
        static PiaPlugin.State joinState;         // instance + 0x4c
        static PiaPlugin.AsyncProcessId asyncId;  // instance + 0x78
        static uint asyncResult;
        static bool joinRandomRegistered;         // instance + 0x7c (RegisterJoinRandom(Joint)SessionSetting)

        public static bool Initialized { get { return initialized; } }

        public static PiaPlugin.Result InitializeFramework(PiaPlugin.InitializeFrameworkSettingNative setting)
        {
            // @0156e818
            if (initialized)
            {
                // pia::framework::Initialize: heap already set up -> 0x10C02; the glue's failure path runs FinalizeAll
                FinalizeAll();
                return Make(ResultAlreadyInitialized);
            }
            initialized = true;
            networkType = (PiaPlugin.NetworkType)setting.networkType;
            joinState = PiaPlugin.State.NotInitialized;
            asyncId = PiaPlugin.AsyncProcessId.Nothing;
            asyncResult = ResultSuccess;
            joinRandomRegistered = false;
            PcConfig.Log("Pia: InitializeFramework (network type " + networkType + ")");
            return Success;
        }

        public static void FinalizeAll()
        {
            // @0156edf0: framework finalize, event buffer / heaps freed
            initialized = false;
            joinState = PiaPlugin.State.NotInitialized;
            asyncId = PiaPlugin.AsyncProcessId.Nothing;
            asyncResult = ResultSuccess;
            joinRandomRegistered = false;
        }

        public static void FinalizeNetwork()
        {
            // @0156c230: unwinds the join process from its state (1..5) to 0; nothing without an instance
            if (initialized)
                joinState = PiaPlugin.State.NotInitialized;
        }

        // Register*/Unregister* (@0156f030 ...): null setting -> 0x10C07, no instance -> 0x10C08, else stored and Success.
        public static PiaPlugin.Result Register(object setting)
        {
            if (setting == null)
                return InvalidArgument;
            return initialized ? Success : InvalidState;
        }

        public static PiaPlugin.Result RegisterJoinRandom(object setting, object criteria)
        {
            // @0156fb90: both pointers checked before the instance
            if (setting == null || criteria == null)
                return InvalidArgument;
            if (!initialized)
                return InvalidState;
            joinRandomRegistered = true;
            return Success;
        }

        public static PiaPlugin.Result Unregister()
        {
            return initialized ? Success : InvalidState;
        }

        public static PiaPlugin.Result UnregisterJoinRandom()
        {
            if (!initialized)
                return InvalidState;
            joinRandomRegistered = false;
            return Success;
        }

        // ---- join process ----------------------------------------------------------------------------------------------------
        public static PiaPlugin.Result ChangeStateAsync(PiaPlugin.State state)
        {
            // @01570fd8 -> framework @0168a3c8
            if (!initialized)
                return InvalidState;
            if (state == PiaPlugin.State.SessionJoined && !joinRandomRegistered)
                return InvalidArgument;
            asyncId = PiaPlugin.AsyncProcessId.ChangeState;
            if (state <= joinState)
            {
                // cleanup / leave transitions only tear down
                joinState = state;
                asyncResult = ResultSuccess;
            }
            else
            {
                // climbing needs the network: the network start-up fails (nifm never grants the request, no local radio)
                asyncResult = NetworkStartupFailure();
            }
            return Success;
        }

        static uint NetworkStartupFailure()
        {
            // TODO(pc): the exact pia result of a failed network start-up is decided deep in pia (pia::inet nifm start-up
            // @015c2460 / the ldn start-up); a local session on a console without its radio reports WifiOff, an internet / LAN
            // one a lost connection - both ShouldUse + ShutdownNetwork, which drive the game's error path the same way.
            return networkType == PiaPlugin.NetworkType.Local ? ResultWifiOff : ResultNetworkConnectionIsLost;
        }

        public static PiaPlugin.Result HandleErrorAsync(PiaPlugin.Result result)
        {
            // @01571040 -> framework @0168a4dc: the error's handling type picks the state to fall back to
            if (!initialized)
                return InvalidState;
            PiaPlugin.State target = ConvertHandlingTypeToState(result.handlingType);
            if (joinState > target)
                joinState = target;
            asyncId = PiaPlugin.AsyncProcessId.HandleError;
            asyncResult = ResultSuccess;
            return Success;
        }

        public static PiaPlugin.State ConvertHandlingTypeToState(PiaPlugin.HandlingType handlingType)
        {
            // @015710d8 -> @0168a790: Cleanup / CleanupWithLeave -> 3, LogoutWithLeave -> 1, else 0 (table @03e713b0);
            // without an instance w0 is the null pointer: 0
            if (!initialized)
                return PiaPlugin.State.NotInitialized;
            switch (handlingType)
            {
                case PiaPlugin.HandlingType.Cleanup:
                case PiaPlugin.HandlingType.CleanupWithLeave:
                    return PiaPlugin.State.SessionCleanedUp;
                case PiaPlugin.HandlingType.LogoutWithLeave:
                    return PiaPlugin.State.LoggedOut;
                default:
                    return PiaPlugin.State.NotInitialized;
            }
        }

        public static PiaPlugin.State GetJoinProcessState()
        {
            return initialized ? joinState : PiaPlugin.State.NotInitialized; // @015710f4
        }

        public static PiaPlugin.AsyncProcessId GetAsyncProcessId()
        {
            return initialized ? asyncId : PiaPlugin.AsyncProcessId.Nothing; // @01571320
        }

        public static PiaPlugin.AsyncState GetAsyncProcessState(PiaPlugin.AsyncProcessId id)
        {
            // @01571230: no instance -> {false, 0x10C08}; another process id -> {false, 0x10C07}; else {completed, result}.
            // The emulated start-ups finish at once (nothing to wait for without a network).
            if (!initialized)
                return MakeAsyncState(false, ResultInvalidState);
            if (asyncId != id)
                return MakeAsyncState(false, ResultInvalidArgument);
            return MakeAsyncState(true, asyncResult);
        }

        public static PiaPlugin.Result CheckDispatchError()
        {
            return Success; // @0156ef78: Success without an instance; the framework records no dispatch error offline
        }

        // AsyncState.result has a private setter (the console's native writes the struct); set it through reflection.
        static readonly PropertyInfo asyncStateResult = typeof(PiaPlugin.AsyncState).GetProperty("result");

        static PiaPlugin.AsyncState MakeAsyncState(bool completed, uint value)
        {
            object boxed = new PiaPlugin.AsyncState { isCompleted = completed };
            asyncStateResult.SetValue(boxed, Make(value), null);
            return (PiaPlugin.AsyncState)boxed;
        }

        // Calls whose glue reaches the session / transport objects: those never exist on the emulated console.
        public static PiaPlugin.Result NoSession()
        {
            return InvalidState;
        }

        // GetSessionStatus (@01574384) and UpdateTransportAnalyzer (@01578398 -> @0168d08c) report a missing session /
        // transport analyzer as 0x10C0C; UpdateTransportAnalyzer without an instance as 0x10C08.
        public static PiaPlugin.Result GetSessionStatus()
        {
            return Make(ResultNotInitialized);
        }

        public static PiaPlugin.Result UpdateTransportAnalyzer()
        {
            return initialized ? Make(ResultNotInitialized) : InvalidState;
        }
    }
}
#endif
