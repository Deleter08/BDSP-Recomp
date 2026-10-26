#if !SWITCH && !UNITY_EDITOR && UNITY_ANDROID
// PC platform layer (Android player only, 2026-10-20): the software-keyboard applet as a native AlertDialog with a text field.
// NOT shipped code. The dialog runs on Android's UI thread; the game's thread waits for OK / Cancel, as the console applet suspends
// the game and the macOS player's NSAlert (PcSwkbdMac) blocks. Back / tapping outside = Cancel. If the dialog never appears (the UI
// thread busy or the call failing) the wait gives up after 5 s and the caller accepts the initial text instead of hanging.
using System;
using System.Text;
using System.Threading;
using nn.swkbd;
using UnityEngine;

namespace PcPlatform
{
    public static class PcSwkbdAndroid
    {
        const int ShowTimeoutMs = 5000;

        sealed class ClickListener : AndroidJavaProxy
        {
            readonly Action done;
            public ClickListener(Action done) : base("android.content.DialogInterface$OnClickListener") { this.done = done; }
            public void onClick(AndroidJavaObject dialog, int which) { done(); }
        }

        sealed class CancelListener : AndroidJavaProxy
        {
            readonly Action done;
            public CancelListener(Action done) : base("android.content.DialogInterface$OnCancelListener") { this.done = done; }
            public void onCancel(AndroidJavaObject dialog) { done(); }
        }

        // 1 = OK (text set), 0 = cancel, -1 = the dialog could not be shown
        static int Show(string title, string message, string initial, string okText, string cancelText, bool edit, out string text)
        {
            string result = null;
            int outcome = -1;
            var shown = new ManualResetEvent(false);
            var closed = new ManualResetEvent(false);
            AndroidJavaObject activity;
            try
            {
                activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity");
            }
            catch (Exception e)
            {
                PcConfig.Log("swkbd: no Android activity (" + e.GetType().Name + ": " + e.Message + ")");
                text = null;
                return -1;
            }
            activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                try
                {
                    AndroidJavaObject field = null;
                    var builder = new AndroidJavaObject("android.app.AlertDialog$Builder", activity);
                    builder.Call<AndroidJavaObject>("setTitle", string.IsNullOrEmpty(title) ? "Software keyboard" : title);
                    if (!string.IsNullOrEmpty(message))
                        builder.Call<AndroidJavaObject>("setMessage", message);
                    if (edit)
                    {
                        field = new AndroidJavaObject("android.widget.EditText", activity);
                        field.Call("setSingleLine", true);
                        field.Call("setText", initial ?? "");
                        field.Call("selectAll");
                        builder.Call<AndroidJavaObject>("setView", field);
                    }
                    builder.Call<AndroidJavaObject>("setPositiveButton", okText, new ClickListener(() =>
                    {
                        if (field != null)
                            result = field.Call<AndroidJavaObject>("getText").Call<string>("toString");
                        outcome = 1;
                        closed.Set();
                    }));
                    builder.Call<AndroidJavaObject>("setNegativeButton", cancelText, new ClickListener(() =>
                    {
                        outcome = 0;
                        closed.Set();
                    }));
                    builder.Call<AndroidJavaObject>("setOnCancelListener", new CancelListener(() =>
                    {
                        outcome = 0;
                        closed.Set();
                    }));
                    var dialog = builder.Call<AndroidJavaObject>("create");
                    // SOFT_INPUT_STATE_ALWAYS_VISIBLE (5): the keyboard opens with the dialog
                    dialog.Call<AndroidJavaObject>("getWindow").Call("setSoftInputMode", 5);
                    dialog.Call("show");
                    if (field != null)
                        field.Call<bool>("requestFocus");
                    shown.Set();
                }
                catch (Exception e)
                {
                    PcConfig.Log("swkbd: the Android dialog failed (" + e.GetType().Name + ": " + e.Message + ")");
                    outcome = -1;
                    shown.Set();
                    closed.Set();
                }
            }));
            if (!shown.WaitOne(ShowTimeoutMs))
            {
                PcConfig.Log("swkbd: the Android dialog did not appear within " + ShowTimeoutMs + " ms");
                text = null;
                return -1;
            }
            closed.WaitOne();
            text = result;
            return outcome;
        }

        public static bool Ask(KeyboardConfig cfg, int maxLength, ref string text, string error)
        {
            var sb = new StringBuilder();
            foreach (var line in new[] { cfg.subText, cfg.guideText })
                if (!string.IsNullOrEmpty(line))
                    sb.Append(line).Append('\n');
            if (!string.IsNullOrEmpty(error))
                sb.Append('\n').Append(error).Append('\n');
            sb.Append("(up to ").Append(maxLength).Append(" characters)");
            string answer;
            int r = Show(cfg.headerText, sb.ToString(), text, string.IsNullOrEmpty(cfg.okText) ? "OK" : cfg.okText, "Cancel", true, out answer);
            if (r < 0)
                return error == null;   // as PcSwkbdMac: a dialog that cannot open accepts the initial text
            if (r == 0)
                return false;
            text = answer ?? "";
            return true;
        }

        public static bool Confirm(string dialog)
        {
            string unused;
            return Show("Software keyboard", dialog, null, "OK", "Back", false, out unused) != 0;
        }
    }
}
#endif
