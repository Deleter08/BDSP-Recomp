#if !SWITCH && !UNITY_EDITOR && UNITY_STANDALONE_OSX
// PC platform layer (macOS player only): the software-keyboard applet as a native modal dialog (NSAlert + text field,
// Assets/Plugins/macOS/PcMacDialog.bundle, source setup/native/pcmacdialog). NOT shipped code. Blocks the main thread until
// closed, as the applet suspends the game. Keyboard: type, Return = OK, Escape = Cancel / Back.
using System;
using System.Runtime.InteropServices;
using System.Text;
using nn.swkbd;

namespace PcPlatform
{
    public static class PcSwkbdMac
    {
        const int OutBytes = 4096;   // UTF-8 of Swkbd.TextMaxLength UTF-16 units, with room to spare

        [DllImport("PcMacDialog")]
        static extern int PcMacAsk(byte[] title, byte[] message, byte[] initial, byte[] okText, int maxChars, byte[] output, int outBytes);

        [DllImport("PcMacDialog")]
        static extern int PcMacConfirm(byte[] title, byte[] message);

        static byte[] Utf8(string s) { return Encoding.UTF8.GetBytes((s ?? "") + "\0"); }

        public static bool Ask(KeyboardConfig cfg, int maxLength, ref string text, string error)
        {
            var sb = new StringBuilder();
            foreach (var line in new[] { cfg.subText, cfg.guideText })
                if (!string.IsNullOrEmpty(line))
                    sb.Append(line).Append('\n');
            if (!string.IsNullOrEmpty(error))
                sb.Append('\n').Append(error).Append('\n');
            sb.Append("\nReturn = OK, Escape = Cancel");
            var output = new byte[OutBytes];
            int r;
            try
            {
                r = PcMacAsk(Utf8(cfg.headerText), Utf8(sb.ToString()), Utf8(text), Utf8(cfg.okText), maxLength, output, output.Length);
            }
            catch (Exception e)
            {
                PcConfig.Log("swkbd: the native dialog failed (" + e.GetType().Name + ": " + e.Message + ") - accepting the initial text");
                return error == null;
            }
            if (r != 1)
                return false;
            int n = Array.IndexOf(output, (byte)0);
            text = Encoding.UTF8.GetString(output, 0, n < 0 ? output.Length : n);
            return true;
        }

        public static bool Confirm(string dialog)
        {
            try
            {
                return PcMacConfirm(Utf8("Software keyboard"), Utf8(dialog)) == 1;
            }
            catch (Exception e)
            {
                PcConfig.Log("swkbd: the native confirm dialog failed (" + e.GetType().Name + ": " + e.Message + ") - confirming");
                return true;
            }
        }
    }
}
#endif
