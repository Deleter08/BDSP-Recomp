#if !SWITCH && !UNITY_EDITOR && UNITY_STANDALONE_WIN
// PC platform layer (Windows PLAYER only): the software keyboard applet as a native modal dialog box (HANDOFF "s38"). NOT shipped code.
//
// On the console ShowKeyboard suspends the game while the applet runs. In the editor PcSwkbdWindow (EditorWindow.ShowModal) does that;
// a player has no editor, so a Win32 modal dialog owned by the Unity window takes its place: DialogBoxIndirectParamW runs its own
// message loop on the calling (main) thread, the game's frame does not advance until OK / Cancel - the applet's behaviour. The dialog
// template is built in memory (no resource file): header / sub / guide / failure text, one edit line limited to maxLength UTF-16
// units, OK and Cancel. The game's text-check confirm dialog is the same dialog without the edit line (OK / Back).
// s43 (player report: "When nicknaming Pokemon window pops up can't exit the window if you're using a controller. Must use mouse."):
// the dialog's own message loop runs instead of the game's frame, so nothing read the pad. A 30 ms timer now polls the XInput pads
// (PcXInput.ReadRawControls) through the player's pad bindings (PcBindings, s47): Switch A (default pad B, the game's confirm) or +
// (default Start) = OK, Switch B (default pad A) = Cancel / Back; the hint line names the bound pad buttons. A button acts on
// RELEASE, after the dialog first saw every pad control up - the press that opened the dialog does not close it, and the closing
// press is not held into the game's next frame.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using nn.swkbd;

namespace PcPlatform
{
    public static class PcSwkbdNative
    {
        const int IdInfo = 100, IdEdit = 101, IDOK = 1, IDCANCEL = 2;
        const uint WM_INITDIALOG = 0x0110, WM_COMMAND = 0x0111, WM_TIMER = 0x0113, EM_SETSEL = 0x00B1, EM_LIMITTEXT = 0x00C5;
        const int PadTimer = 1, PadPollMs = 30;
        // the Switch buttons the dialog answers to (bits of padDown)
        const int SwA = 1, SwB = 2, SwPlus = 4;

        // "Controller: B or Start = OK, A = " with the default bindings (the dialog text is English, as its OK / Cancel buttons)
        static string PadHint
        {
            get
            {
                var pad = PcBindings.Live.pad;
                return "Controller: " + PcBindings.PadName(pad[(int)PcAction.A]) + " or " + PcBindings.PadName(pad[(int)PcAction.Plus])
                    + " = OK, " + PcBindings.PadName(pad[(int)PcAction.B]) + " = ";
            }
        }

        delegate IntPtr DialogProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr DialogBoxIndirectParamW(IntPtr hInstance, IntPtr template, IntPtr owner, DialogProc proc, IntPtr param);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetDlgItemTextW(IntPtr dlg, int id, string text);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetDlgItemTextW(IntPtr dlg, int id, StringBuilder text, int max);
        [DllImport("user32.dll")]
        static extern IntPtr SendDlgItemMessageW(IntPtr dlg, int id, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern IntPtr GetDlgItem(IntPtr dlg, int id);
        [DllImport("user32.dll")]
        static extern IntPtr SetFocus(IntPtr hwnd);
        [DllImport("user32.dll")]
        static extern bool EndDialog(IntPtr dlg, IntPtr result);
        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll")]
        static extern IntPtr SendMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern UIntPtr SetTimer(IntPtr hwnd, UIntPtr id, uint ms, IntPtr proc);
        [DllImport("user32.dll")]
        static extern bool KillTimer(IntPtr hwnd, UIntPtr id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr GetModuleHandleW(string name);

        // the dialog's state lives here while DialogBoxIndirectParamW runs (one dialog at a time, main thread)
        static readonly DialogProc proc = Proc;
        static string info, initial, answer;
        static int limit;
        static bool hasEdit;
        static bool padArmed;   // every pad button seen up since the dialog opened
        static int padDown;     // the OK / Cancel buttons down at the last poll

        public static bool Ask(KeyboardConfig cfg, int maxLength, ref string text, string error)
        {
            var sb = new StringBuilder();
            foreach (var line in new[] { cfg.headerText, cfg.subText, cfg.guideText })
                if (!string.IsNullOrEmpty(line))
                    sb.Append(line).Append("\r\n");
            if (!string.IsNullOrEmpty(error))
                sb.Append("\r\n").Append(error).Append("\r\n");
            sb.Append(PadHint).Append("Cancel");
            initial = text ?? "";
            limit = maxLength;
            long r = Show(sb.ToString(), true, string.IsNullOrEmpty(cfg.okText) ? "OK" : cfg.okText, "Cancel");
            if (r == -1)
            {
                PcConfig.Log("swkbd: the native dialog failed (error " + Marshal.GetLastWin32Error() + ") - accepting the initial text");
                return error == null;
            }
            if (r != 1 || answer == null)
                return false;
            text = answer;
            return true;
        }

        // The game's text-check confirm text (TextCheckResult.ShowConfirmDialog): OK keeps the text, Back returns to the input.
        public static bool Confirm(string dialog)
        {
            initial = "";
            limit = 0;
            long r = Show((dialog ?? "") + "\r\n\r\n" + PadHint + "Back", false, "OK", "Back");
            if (r == -1)
            {
                PcConfig.Log("swkbd: the native confirm dialog failed (error " + Marshal.GetLastWin32Error() + ") - accepting");
                return true;
            }
            return r == 1;
        }

        static long Show(string text, bool edit, string okText, string cancelText)
        {
            info = text.TrimEnd();
            hasEdit = edit;
            answer = null;
            padArmed = false;
            padDown = 0;
            int infoLines = Math.Max(2, info.Split('\n').Length + 1);
            byte[] template = Template("Software keyboard", infoLines, edit, okText, cancelText);
            IntPtr mem = Marshal.AllocHGlobal(template.Length);
            try
            {
                Marshal.Copy(template, 0, mem, template.Length);
                return (long)DialogBoxIndirectParamW(GetModuleHandleW(null), mem, GetActiveWindow(), proc, IntPtr.Zero);
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
        }

        // WM_TIMER: OK / Cancel from the pad, on release (see the header).
        static void PollPad(IntPtr hwnd)
        {
            int k = PcXInput.ReadRawControls();
            if (!padArmed)
            {
                padArmed = k == 0;
                return;
            }
            nn.hid.NpadButton b = PcBindings.PadButtons(k);
            int now = ((b & nn.hid.NpadButton.A) != 0 ? SwA : 0) | ((b & nn.hid.NpadButton.B) != 0 ? SwB : 0)
                | ((b & nn.hid.NpadButton.Plus) != 0 ? SwPlus : 0);
            int released = padDown & ~now;
            padDown = now;
            int id = (released & SwB) != 0 ? IDCANCEL : (released & (SwA | SwPlus)) != 0 ? IDOK : 0;
            if (id == 0)
                return;
            KillTimer(hwnd, (UIntPtr)PadTimer);
            PcConfig.Log("swkbd: " + (id == IDOK ? "OK" : "Cancel") + " from the game pad");
            SendMessageW(hwnd, WM_COMMAND, (IntPtr)id, IntPtr.Zero);
        }

        [AOT.MonoPInvokeCallback(typeof(DialogProc))]
        static IntPtr Proc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_INITDIALOG:
                    SetDlgItemTextW(hwnd, IdInfo, info);
                    SetTimer(hwnd, (UIntPtr)PadTimer, PadPollMs, IntPtr.Zero);
                    if (!hasEdit)
                        return (IntPtr)1; // TRUE: the default focus (the OK button)
                    SendDlgItemMessageW(hwnd, IdEdit, EM_LIMITTEXT, (IntPtr)limit, IntPtr.Zero);
                    SetDlgItemTextW(hwnd, IdEdit, initial);
                    SendDlgItemMessageW(hwnd, IdEdit, EM_SETSEL, IntPtr.Zero, (IntPtr)(-1));
                    SetFocus(GetDlgItem(hwnd, IdEdit));
                    return IntPtr.Zero; // FALSE: the focus was set here
                case WM_TIMER:
                    if ((long)wParam == PadTimer)
                    {
                        PollPad(hwnd);
                        return (IntPtr)1;
                    }
                    break;
                case WM_COMMAND:
                    int id = (int)((long)wParam & 0xFFFF);
                    if (id == IDOK)
                    {
                        if (hasEdit)
                        {
                            var sb = new StringBuilder(limit + 2);
                            GetDlgItemTextW(hwnd, IdEdit, sb, limit + 1);
                            answer = sb.ToString();
                        }
                        KillTimer(hwnd, (UIntPtr)PadTimer);
                        EndDialog(hwnd, (IntPtr)1);
                        return (IntPtr)1;
                    }
                    if (id == IDCANCEL)
                    {
                        KillTimer(hwnd, (UIntPtr)PadTimer);
                        EndDialog(hwnd, IntPtr.Zero);
                        return (IntPtr)1;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        // ---- in-memory DLGTEMPLATE (dialog units; DS_SETFONT 9 pt Segoe UI) ----------------------------------------------------
        static byte[] Template(string title, int infoLines, bool edit, string okText, string cancelText)
        {
            const uint DS_SETFONT = 0x40, DS_MODALFRAME = 0x80, DS_CENTER = 0x800;
            const uint WS_POPUP = 0x80000000, WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000;
            const uint CHILD = 0x50000000;                       // WS_CHILD | WS_VISIBLE
            const uint SS_LEFT = 0x0, ES_AUTOHSCROLL = 0x80, WS_BORDER = 0x00800000, WS_TABSTOP = 0x00010000;
            const uint BS_PUSHBUTTON = 0x0, BS_DEFPUSHBUTTON = 0x1;
            const ushort BUTTON = 0x0080, EDIT = 0x0081, STATIC = 0x0082;
            int width = 300, infoH = 10 * infoLines, editH = edit ? 20 : 0, height = infoH + editH + 30;

            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(DS_SETFONT | DS_MODALFRAME | DS_CENTER | WS_POPUP | WS_CAPTION | WS_SYSMENU);
            w.Write(0u);                 // extended style
            w.Write((ushort)(edit ? 4 : 3));   // items
            w.Write((short)0); w.Write((short)0); w.Write((short)width); w.Write((short)height);
            w.Write((ushort)0);          // no menu
            w.Write((ushort)0);          // default dialog class
            Sz(w, title);
            w.Write((ushort)9);
            Sz(w, "Segoe UI");
            Item(w, CHILD | SS_LEFT, 7, 6, width - 14, infoH, IdInfo, STATIC, "");
            if (edit)
                Item(w, CHILD | WS_BORDER | WS_TABSTOP | ES_AUTOHSCROLL, 7, infoH + 10, width - 14, 14, IdEdit, EDIT, "");
            Item(w, CHILD | WS_TABSTOP | BS_DEFPUSHBUTTON, width - 124, infoH + editH + 10, 56, 14, IDOK, BUTTON, okText);
            Item(w, CHILD | WS_TABSTOP | BS_PUSHBUTTON, width - 63, infoH + editH + 10, 56, 14, IDCANCEL, BUTTON, cancelText);
            w.Flush();
            return ms.ToArray();
        }

        static void Item(BinaryWriter w, uint style, int x, int y, int cx, int cy, int id, ushort cls, string text)
        {
            while (w.BaseStream.Position % 4 != 0)
                w.Write((byte)0);        // every DLGITEMTEMPLATE starts on a DWORD boundary
            w.Write(style);
            w.Write(0u);
            w.Write((short)x); w.Write((short)y); w.Write((short)cx); w.Write((short)cy);
            w.Write((ushort)id);
            w.Write((ushort)0xFFFF); w.Write(cls);   // predefined class by atom
            Sz(w, text);
            w.Write((ushort)0);          // no creation data
        }

        static void Sz(BinaryWriter w, string s)
        {
            foreach (char c in s ?? "")
                w.Write((ushort)c);
            w.Write((ushort)0);
        }
    }
}
#endif
