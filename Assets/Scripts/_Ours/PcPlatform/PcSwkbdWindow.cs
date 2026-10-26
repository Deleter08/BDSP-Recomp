#if !SWITCH && UNITY_EDITOR
// PC platform layer (editor only): the modal text-entry window that stands in for the Switch software-keyboard applet. NOT shipped
// code. EditorWindow.ShowModal blocks the calling (game) thread until the window closes, as the applet suspends the game.
using nn.swkbd;
using UnityEditor;
using UnityEngine;

namespace PcPlatform
{
    public class PcSwkbdWindow : EditorWindow
    {
        string header, sub, guide, okText, error, text;
        int maxLength;
        bool ok, focused;

        public static bool Ask(KeyboardConfig cfg, int maxLength, ref string text, string error)
        {
            var w = CreateInstance<PcSwkbdWindow>();
            w.titleContent = new GUIContent("Software keyboard");
            w.header = cfg.headerText;
            w.sub = cfg.subText;
            w.guide = cfg.guideText;
            w.okText = string.IsNullOrEmpty(cfg.okText) ? "OK" : cfg.okText;
            w.error = error;
            w.text = text ?? "";
            w.maxLength = maxLength;
            w.minSize = w.maxSize = new Vector2(520, 200);
            w.ShowModal();
            if (!w.ok)
                return false;
            text = w.text;
            return true;
        }

        void OnGUI()
        {
            if (!string.IsNullOrEmpty(header)) EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(sub)) EditorGUILayout.LabelField(sub, EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
            GUI.SetNextControlName("swkbd");
            text = EditorGUILayout.TextField(guide ?? "", text);
            if (text.Length > maxLength)
                text = text.Substring(0, maxLength);
            if (!focused)
            {
                EditorGUI.FocusTextInControl("swkbd");
                focused = true;
            }
            EditorGUILayout.LabelField(text.Length + " / " + maxLength, EditorStyles.miniLabel);
            var e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            bool esc = e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape;
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel") || esc)
            {
                ok = false;
                Close();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button(okText) || enter)
            {
                ok = true;
                Close();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
