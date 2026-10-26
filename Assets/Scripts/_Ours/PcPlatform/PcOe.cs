#if !SWITCH
// PC platform layer (s33c): nn::oe (the application's view of the console: desired language, active time, crash report, the
// China health-warning event). NOT shipped code. Behaviour reference: Ryujinx Services/Am (IApplicationFunctions /
// ICommonStateGetter) and Services/Settings with one user and default system settings.
using System.Diagnostics;

namespace PcPlatform
{
    public static class PcOe
    {
        // nn::oe::GetDesiredLanguage returns the system language the application supports best (nn::settings::LanguageCode, an
        // 8-byte NUL-padded BCP-47 tag). Ryujinx's default system language is AmericanEnglish = "en-US".
        public static string DesiredLanguage = "en-US";

        // The 8 bytes of nn::settings::LanguageCode, little-endian packed into a ulong (byte i = char i), as the native returns it.
        public static ulong DesiredLanguageCode()
        {
            ulong code = 0;
            string s = DesiredLanguage ?? "";
            for (int i = 0; i < 8 && i < s.Length; i++)
                code |= (ulong)(byte)s[i] << (8 * i);
            return code;
        }

        // nn::oe::GetProgramTotalActiveTime: nanoseconds the program has been in the foreground since launch.
        static readonly Stopwatch activeTime = Stopwatch.StartNew();

        public static long ProgramTotalActiveTimeNanoseconds()
        {
            return (long)(activeTime.ElapsedTicks * (1000000000.0 / Stopwatch.Frequency));
        }

        static bool crashReportEnabled = true;

        public static void SetCrashReport(bool enabled)
        {
            crashReportEnabled = enabled;
            PcConfig.Log("oe: crash report " + (crashReportEnabled ? "enabled" : "disabled"));
        }
    }
}
#endif
