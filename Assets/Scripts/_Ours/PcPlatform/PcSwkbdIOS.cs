#if !SWITCH && !UNITY_EDITOR && UNITY_IOS
// PC platform layer (iOS player only): the software-keyboard applet as a native modal UIAlertController with a text field
// (Assets/Plugins/iOS/PcSwkbdIOS.m). NOT shipped code. Blocks the main thread via run-loop pumping until OK / Cancel, as the
// console applet suspends the game and the macOS player's NSAlert (PcSwkbdMac) blocks. Done = OK, Cancel / Back = Cancel.
// The keyboard mode (numeric / ASCII) and password mode of the game's config are passed through to the text field.
using System;
using System.Runtime.InteropServices;
using System.Text;
using nn.swkbd;

namespace PcPlatform
{
    public static class PcSwkbdIOS
    {
        const int OutBytes = 4096;   // UTF-8 of Swkbd.TextMaxLength UTF-16 units, with room to spare

        [DllImport("__Internal")]
        static extern int PcIosAsk(byte[] title, byte[] message, byte[] initial, byte[] okText, int maxChars, int keyboardMode, int passwordMode, byte[] output, int outBytes);

        [DllImport("__Internal")]
        static extern int PcIosConfirm(byte[] title, byte[] message);

        static byte[] Utf8(string s) { return Encoding.UTF8.GetBytes((s ?? "") + "\0"); }

        public static bool Ask(KeyboardConfig cfg, int maxLength, ref string text, string error)
        {
            var sb = new StringBuilder();
            foreach (var line in new[] { cfg.subText, cfg.guideText })
                if (!string.IsNullOrEmpty(line))
                    sb.Append(line).Append('\n');
            if (!string.IsNullOrEmpty(error))
                sb.Append('\n').Append(error).Append('\n');
            var output = new byte[OutBytes];
            int r;
            try
            {
                r = PcIosAsk(Utf8(cfg.headerText), Utf8(sb.ToString()), Utf8(text), Utf8(cfg.okText), maxLength,
                    (int)cfg.keyboardMode, (int)cfg.passwordMode, output, output.Length);
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
                return PcIosConfirm(Utf8("Software keyboard"), Utf8(dialog)) == 1;
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
