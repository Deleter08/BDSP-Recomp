#if !SWITCH
// PC platform layer: nn::swkbd (the system software keyboard applet) on the PC (HANDOFF §11.7 "Software keyboard"). NOT shipped code.
//
// On the console ShowKeyboard suspends the game while the applet runs: the user types, the applet calls the game's text-check
// callback (Dpr.UI.SoftwareKeyboard.TextCheckCallback), shows the failure / confirm text the callback writes, and returns the text,
// or ResultCanceled (159-1). The PC does the same on the calling (main) thread:
//   - an answer queued by the play script (PcInput `swkbd <text>` / `swkbdcancel`) is used first;
//   - else, in the editor, a modal window (PcSwkbdWindow, EditorWindow.ShowModal) blocks like the applet; in a Windows player a
//     native modal dialog box (PcSwkbdNative, s38) does the same, in a macOS player an NSAlert (PcSwkbdMac), in an Android player
//     an AlertDialog with a text field (PcSwkbdAndroid);
//   - else (a script is running but queued nothing) the initial text is accepted, as if OK was pressed.
// Every answer goes through the game's text-check callback with the console's buffers (UTF-16 string + NUL, a 500-character
// dialog buffer); a failure is shown (editor) / logged (script) and the input is asked again.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using nn.swkbd;
using UnityEngine;

namespace PcPlatform
{
    public static class PcSwkbd
    {
        public static readonly nn.Result ResultCanceled = new nn.Result(159, 1);

        static string initialText = "";
        static TextCheckCallback textCheck;
        static readonly Queue<string> answers = new Queue<string>(); // null entry = cancel

        // ---- the play script's answers ------------------------------------------------------------------------------
        public static void Enqueue(string text) { answers.Enqueue(text); }

        // ---- config / arg (the console keeps these in the applet's native structs) ----------------------------------
        public static void InitializeKeyboardConfig(ref KeyboardConfig cfg)
        {
            cfg = new KeyboardConfig();
            cfg.keyboardMode = KeyboardMode.Full;
            cfg.isPredictionEnabled = true;
            cfg.initialCursorPos = InitialCursorPos.Last;
            cfg.passwordMode = PasswordMode.Show;
            cfg.inputFormMode = InputFormMode.OneLine;
            cfg.isUseBlurBackground = true;
            cfg.PcInitArrays();
        }

        public static void MakePreset(ref KeyboardConfig cfg, Preset preset)
        {
            switch (preset)
            {
                case Preset.Password:
                    cfg.passwordMode = PasswordMode.Hide;
                    cfg.isPredictionEnabled = false;
                    break;
                case Preset.UserName:
                    cfg.isPredictionEnabled = false;
                    cfg.invalidCharFlag = InvalidChar.AtMark;
                    break;
                case Preset.DownloadCode:
                    cfg.keyboardMode = KeyboardMode.ASCII;
                    cfg.isPredictionEnabled = false;
                    cfg.invalidCharFlag = InvalidChar.OutsideOfDownloadCode;
                    break;
            }
        }

        public static string Clip(string s, int max)
        {
            if (s == null)
                return null;
            return s.Length > max ? s.Substring(0, max) : s;
        }

        public static void SetInitialText(string text) { initialText = text ?? ""; }

        public static void SetTextCheckCallback(TextCheckCallback cb) { textCheck = cb; }

        public static void Initialize(ref ShowKeyboardArg arg)
        {
            InitializeKeyboardConfig(ref arg.keyboardConfig);
            initialText = "";
            textCheck = null;
        }

        public static void Destroy(ref ShowKeyboardArg arg)
        {
            textCheck = null;
        }

        // ---- the applet --------------------------------------------------------------------------------------------
        public static nn.Result ShowKeyboard(out string result, ShowKeyboardArg arg)
        {
            var cfg = arg.keyboardConfig;
            int maxLength = cfg.textMaxLength > 0 ? Math.Min(cfg.textMaxLength, Swkbd.TextMaxLength) : Swkbd.TextMaxLength;
            string text = initialText ?? "";
            string error = null;
            while (true)
            {
                if (!NextAnswer(cfg, maxLength, ref text, error))
                {
                    PcConfig.Log("swkbd: canceled");
                    result = null;
                    return ResultCanceled;
                }
                if (text.Length > maxLength)
                    text = text.Substring(0, maxLength);
                if (text.Length < cfg.textMinLength)
                {
                    error = "(min " + cfg.textMinLength + " characters)";
                    continue;
                }
                if (textCheck == null)
                    break;
                TextCheckResult check = RunTextCheck(text, out string dialog);
                if (check == TextCheckResult.Success)
                    break;
                if (check == TextCheckResult.ShowConfirmDialog && Confirm(dialog))
                    break;
                PcConfig.Log("swkbd: text check " + check + " for '" + text + "': " + dialog);
                error = dialog;
            }
            PcConfig.Log("swkbd: '" + text + "'");
            result = text;
            return new nn.Result();
        }

        static bool NextAnswer(KeyboardConfig cfg, int maxLength, ref string text, string error)
        {
            if (answers.Count > 0)
            {
                string a = answers.Dequeue();
                if (a == null)
                    return false;
                text = a;
                return true;
            }
#if UNITY_EDITOR
            if (!PcInput.Active)
                return PcSwkbdWindow.Ask(cfg, maxLength, ref text, error);
#elif UNITY_STANDALONE_WIN
            if (!PcInput.Active)
                return PcSwkbdNative.Ask(cfg, maxLength, ref text, error);
#elif UNITY_STANDALONE_OSX
            if (!PcInput.Active)
                return PcSwkbdMac.Ask(cfg, maxLength, ref text, error);
#elif UNITY_ANDROID
            if (!PcInput.Active)
                return PcSwkbdAndroid.Ask(cfg, maxLength, ref text, error);
#elif UNITY_IOS
            if (!PcInput.Active)
                return PcSwkbdIOS.Ask(cfg, maxLength, ref text, error);
#endif
            if (error != null)
                return false; // a scripted / unattended run cannot answer a failed check: cancel, as the user backing out would
            PcConfig.Log("swkbd: no queued answer - accepting the initial text '" + text + "'");
            return true;
        }

        // The applet's call into the game: UTF-16 text + NUL in a native buffer (nn::swkbd::String), and a dialog buffer the
        // callback fills with a NUL-terminated message.
        static TextCheckResult RunTextCheck(string text, out string dialog)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(text + "\0");
            IntPtr strBuf = Marshal.AllocHGlobal(bytes.Length);
            int dialogBytes = (Swkbd.DialogTextMaxLength + 1) * 2;
            IntPtr dialogBuf = Marshal.AllocHGlobal(dialogBytes);
            try
            {
                Marshal.Copy(bytes, 0, strBuf, bytes.Length);
                var zero = new byte[dialogBytes];
                Marshal.Copy(zero, 0, dialogBuf, dialogBytes);
                var str = new nn.swkbd.String { ptr = strBuf, bufSize = bytes.Length };
                long length = 0;
                TextCheckResult r = textCheck(dialogBuf, ref length, ref str);
                dialog = Marshal.PtrToStringUni(dialogBuf) ?? "";
                return r;
            }
            finally
            {
                Marshal.FreeHGlobal(strBuf);
                Marshal.FreeHGlobal(dialogBuf);
            }
        }

        static bool Confirm(string dialog)
        {
#if UNITY_EDITOR
            if (!PcInput.Active)
                return UnityEditor.EditorUtility.DisplayDialog("Software keyboard", dialog, "OK", "Back");
#elif UNITY_STANDALONE_WIN
            if (!PcInput.Active)
                return PcSwkbdNative.Confirm(dialog);
#elif UNITY_STANDALONE_OSX
            if (!PcInput.Active)
                return PcSwkbdMac.Confirm(dialog);
#elif UNITY_ANDROID
            if (!PcInput.Active)
                return PcSwkbdAndroid.Confirm(dialog);
#elif UNITY_IOS
            if (!PcInput.Active)
                return PcSwkbdIOS.Confirm(dialog);
#endif
            return true;
        }
    }
}
#endif
