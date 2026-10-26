#if !SWITCH
// PC platform layer (s33c): nn::err, the error-viewer applet. NOT shipped code.
// On the console nn::err::ShowError* launches the error applet, which blocks the caller until the player closes it, then returns.
// Ryujinx's ErrorApplet (HOS/Applets/Error) shows a dialog "Error Code: 2xxx-yyyy" and returns. On PC the dialog is the log line:
// it is recorded and the call returns at once (never blocks the game thread that called it).
namespace PcPlatform
{
    public static class PcErr
    {
        // Ryujinx ErrorApplet.HandleResultErrorCommonArg: module + 2000, description (the "2xxx-yyyy" shown to the player).
        public static string Format(nn.Result result)
        {
            uint value = result.innerValue;
            return string.Format("{0:D4}-{1:D4} (result 0x{2:X8})", (value & 0x1FF) + 2000, (value >> 9) & 0x3FFF, value);
        }

        public static void ShowError(nn.Result result)
        {
            PcConfig.Log("err: ShowError " + Format(result));
        }

        public static void ShowError(nn.err.ErrorCode errorCode)
        {
            PcConfig.Log(string.Format("err: ShowError {0:D4}-{1:D4}", errorCode.category, errorCode.number));
        }

        public static void ShowUnacceptableAddOnContentVersion()
        {
            PcConfig.Log("err: ShowUnacceptableAddOnContentVersionError");
        }

        public static void ShowUnacceptableApplicationVersion()
        {
            PcConfig.Log("err: ShowUnacceptableApplicationVersionError");
        }

        // nn::err::ErrorCode::GetInvalidErrorCode() = { category 0, number 0 }; IsValid() = category != 0.
        public static nn.err.ErrorCode InvalidErrorCode
        {
            get { return new nn.err.ErrorCode { category = 0, number = 0 }; }
        }

        public static bool IsValid(nn.err.ErrorCode errorCode)
        {
            return errorCode.category != 0; // TODO(pc): SDK-internal predicate (not in our image); category 0 = the invalid code
        }
    }
}
#endif
