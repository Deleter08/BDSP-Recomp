#if !SWITCH
// PC platform layer: the controls and fast-forward rows of the in-game Options (s47, players' request: keybinds for keyboard /
// XInput and a game speed, "in the option menu, same place as where screen/windows type option is"). NOT shipped code.
//
// Five rows follow the display rows (PcDisplayOptions, which calls in here): Fast-Forward Speed (2x-8x), Fast-Forward Key (Hold /
// Toggle) - PcGameSpeed -, Button Layout (the pad's face buttons by Switch position or by Xbox letter), Keyboard Keys and Controller
// Buttons. The rows are clones of the shipped WindowChange row, like the display rows; pseudo ConfigIDs 104-108 never reach CONFIG.
// A on Keyboard Keys / Controller Buttons opens a PAGE: the same list shows one row per action instead (pseudo ConfigIDs 200 + action
// / 300 + action), the shipped Up / Down / L / R move through it, and here: A = wait for the key / button to bind (PcBindings
// capture: 5 s or Esc cancels), X = clear, Y = every action of the page back to its default, B = back to the main list; Left / Right
// on a keyboard row picks key 1 or key 2 (the brackets in "[Z] / Enter" mark it). PcBindings' rules: the menu actions keep a key and
// a button, and a key / button taken from another action swaps with it ("Swapped with ..." in the description).
// Everything is a pending edit applied on the shipped "save these settings?" yes, discarded on no. The shipped Y reset of the main list
// resets the fast-forward rows (at once, like the display rows) and leaves the bindings to their pages' own Y.
using System;
using System.Collections.Generic;
using System.Linq;
using Audio;
using Dpr.Message;
using Dpr.UI;
using UnityEngine;
using UnityEngine.Events;

namespace PcPlatform
{
    public static class PcControlsOptions
    {
        public const ConfigID SpeedRow = (ConfigID)104;
        public const ConfigID KeyModeRow = (ConfigID)105;
        public const ConfigID LayoutRow = (ConfigID)106;
        public const ConfigID KeyboardRow = (ConfigID)107;
        public const ConfigID PadRow = (ConfigID)108;
        const int KeyRowBase = 200, PadRowBase = 300;   // + (int)PcAction: the rows of the two pages
        static readonly string[] MainRowNames = { "PcFastForwardSpeed", "PcFastForwardKey", "PcButtonLayout", "PcKeyboardKeys", "PcControllerButtons" };

        // page order: Fast-Forward first (the new action), the Switch buttons, the d-pad, the sticks, the stick presses
        static readonly PcAction[] KeyPage =
        {
            PcAction.FastForward, PcAction.A, PcAction.B, PcAction.X, PcAction.Y, PcAction.L, PcAction.R, PcAction.ZL, PcAction.ZR,
            PcAction.Plus, PcAction.Minus, PcAction.DpadUp, PcAction.DpadDown, PcAction.DpadLeft, PcAction.DpadRight,
            PcAction.LStickUp, PcAction.LStickDown, PcAction.LStickLeft, PcAction.LStickRight,
            PcAction.RStickUp, PcAction.RStickDown, PcAction.RStickLeft, PcAction.RStickRight,
            PcAction.LStickPress, PcAction.RStickPress,
        };
        static readonly PcAction[] PadPage = KeyPage.Where(PcBindings.OnPad).ToArray();

        enum Page { Main, Keyboard, Pad }

        static PcBindingTable pending;
        static int speedIndex, keyModeIndex;   // keyModeIndex: 0 = Hold, 1 = Toggle
        static SettingMenuItem speedItem, keyModeItem, layoutItem, keyboardItem, padItem;
        static readonly List<SettingMenuItem> keyRows = new List<SettingMenuItem>(), padRows = new List<SettingMenuItem>();
        static List<SettingMenuItem> mainRows;
        static int mainSelect, mainScroll;
        static Page page;
        static bool layoutDirty;
        static string notice1, notice2;        // a one-time description (a swapped or refused binding) for noticeItem
        static SettingMenuItem noticeItem;
        static Transform content, cursor;
        static UnityAction<SettingMenuItem> onChanged;

        public static bool Owns(SettingMenuItem item)
        {
            if (item == null)
                return false;
            int id = (int)item.configId;
            return (id >= (int)SpeedRow && id <= (int)PadRow) || (id >= KeyRowBase && id < KeyRowBase + PcBindingTable.Actions)
                || (id >= PadRowBase && id < PadRowBase + PcBindingTable.Actions);
        }

        static bool IsKeyRow(SettingMenuItem item) { int id = (int)item.configId; return id >= KeyRowBase && id < KeyRowBase + PcBindingTable.Actions; }
        static bool IsPadRow(SettingMenuItem item) { int id = (int)item.configId; return id >= PadRowBase && id < PadRowBase + PcBindingTable.Actions; }
        static PcAction ActionOf(SettingMenuItem item) { return (PcAction)((int)item.configId - (IsPadRow(item) ? PadRowBase : KeyRowBase)); }

        static IEnumerable<string> PageRowNames()
        {
            return KeyPage.Select(a => "PcKey" + a).Concat(PadPage.Select(a => "PcPad" + a));
        }

        // with PcDisplayOptions.MoveRowsToEnd, before the shipped loop (child index = ConfigID)
        public static void MoveRowsToEnd(Transform content)
        {
            foreach (var name in MainRowNames.Concat(PageRowNames()))
            {
                var t = content.Find(name);
                if (t != null)
                    t.SetAsLastSibling();
            }
        }

        // After the display rows: creates (once per window instance) and sets up the five rows at sibling / list index `at`.
        public static void Open(Transform content, List<SettingMenuItem> activeItems, UnityAction<SettingMenuItem> onValueChanged, Transform cursor, int at)
        {
            PcControlsOptions.content = content;
            PcControlsOptions.cursor = cursor;
            onChanged = onValueChanged;
            pending = PcBindings.Live.Clone();
            page = Page.Main;
            mainRows = null;
            layoutDirty = false;
            ClearNotice();
            keyRows.Clear();
            padRows.Clear();
            foreach (var name in PageRowNames())
            {
                var t = content.Find(name);
                if (t != null)
                    t.gameObject.SetActive(false);   // an earlier opening's page rows stay hidden at the end
            }

            speedIndex = Math.Max(0, Array.IndexOf(PcGameSpeed.Speeds, PcGameSpeed.Speed));
            keyModeIndex = PcGameSpeed.ToggleMode ? 1 : 0;
            speedItem = PcDisplayOptions.Row(content, MainRowNames[0], cursor);
            keyModeItem = PcDisplayOptions.Row(content, MainRowNames[1], cursor);
            layoutItem = PcDisplayOptions.Row(content, MainRowNames[2], cursor);
            keyboardItem = PcDisplayOptions.Row(content, MainRowNames[3], cursor);
            padItem = PcDisplayOptions.Row(content, MainRowNames[4], cursor);
            var rows = new[] { speedItem, keyModeItem, layoutItem, keyboardItem, padItem };
            if (rows.Any(r => r == null))
            {
                UnityEngine.Debug.LogWarning("[PcPlatform] controls options: no row to clone");
                return;
            }
            speedItem.SetupPc(SpeedRow, S(K.TitleSpeed), PcGameSpeed.Speeds.Select(s => s + "x").ToArray(), speedIndex, onValueChanged);
            keyModeItem.SetupPc(KeyModeRow, S(K.TitleKey), new[] { S(K.Hold), S(K.Toggle) }, keyModeIndex, onValueChanged);
            layoutItem.SetupPc(LayoutRow, S(K.TitleLayout), LayoutValues(), (int)PcBindings.Layout(pending), onValueChanged);
            keyboardItem.SetupPc(KeyboardRow, S(K.TitleKeyboard), new[] { KeysStatus() }, 0, onValueChanged);
            padItem.SetupPc(PadRow, S(K.TitleController), new[] { PadStatus() }, 0, onValueChanged);
            for (int i = 0; i < rows.Length; i++)
                rows[i].transform.SetSiblingIndex(at + i);
            activeItems.InsertRange(at, rows);
        }

        static string[] LayoutValues()
        {
            var v = new List<string> { S(K.Positions), S(K.Labels) };
            if (PcBindings.Layout(pending) == PcBindings.PadLayout.Custom)
                v.Add(S(K.Custom));
            return v.ToArray();
        }

        static string KeysStatus() { return pending.SameKeys(PcBindings.Defaults()) ? S(K.Default) : S(K.Custom); }
        static string PadStatus() { return pending.SamePad(PcBindings.Defaults()) ? S(K.Default) : S(K.Custom); }

        static void RefreshMainRows()
        {
            if (layoutItem == null)
                return;
            layoutItem.SetPcValues(LayoutValues(), (int)PcBindings.Layout(pending));
            keyboardItem.SetPcValues(new[] { KeysStatus() }, 0);
            padItem.SetPcValues(new[] { PadStatus() }, 0);
        }

        static string[] RowValues(PcAction a, bool pad)
        {
            if (pad)
                return new[] { PcBindings.PadName(pending.pad[(int)a]) };
            string k0 = PcBindings.KeyName(pending.keys[(int)a, 0]), k1 = PcBindings.KeyName(pending.keys[(int)a, 1]);
            return new[] { "[" + k0 + "] / " + k1, k0 + " / [" + k1 + "]" };
        }

        static void RefreshPageRows()
        {
            foreach (var r in page == Page.Pad ? padRows : keyRows)
                r.SetPcValues(RowValues(ActionOf(r), page == Page.Pad), r.selectIndex);
        }

        // SettingWindow.OnUpdate, before the shipped buttons: true = handled here (the shipped A / B / Y paths do not run)
        public static bool Update(SettingWindow window, SettingMenuItem item, bool a, bool b, bool x, bool y)
        {
            if (pending == null)
                return false;
            if (layoutDirty)
            {
                layoutDirty = false;
                RefreshMainRows();
            }
            if (PcBindings.Capturing)
            {
                KeyCode key;
                PadControl control;
                if (PcBindings.TakeResult(out key, out control))
                    Captured(window, item, key, control);
                return true;
            }
            if (item == null)
                return false;
            if (page == Page.Main)
            {
                if (a && (item == keyboardItem || item == padItem))
                {
                    OpenPage(window, item == keyboardItem ? Page.Keyboard : Page.Pad);
                    return true;
                }
                return false;
            }
            if (!IsKeyRow(item) && !IsPadRow(item))
                return false;
            PcAction action = ActionOf(item);
            bool pad = page == Page.Pad;
            if (b)
            {
                ClosePage(window);
                return true;
            }
            if (a)
            {
                ClearNotice();
                PcBindings.BeginCapture(pad ? PcBindings.CaptureDevice.Pad : PcBindings.CaptureDevice.Keyboard);
                Se(AK.EVENTS.UI_COMMON_DECIDE);
                window.PcRefreshDescription();
                return true;
            }
            if (x)
            {
                bool ok = pad ? PcBindings.ClearPad(pending, action) : PcBindings.ClearKey(pending, action, item.selectIndex);
                if (ok)
                {
                    ClearNotice();
                    Se(AK.EVENTS.UI_COMMON_SELECT);
                }
                else
                {
                    SetNotice(item, F(pad ? K.NeedPad : K.NeedKey, action), S(K.Need2));
                    Se(AK.EVENTS.UI_COMMON_BEEP);
                }
                RefreshPageRows();
                window.PcRefreshDescription();
                return true;
            }
            if (y)
            {
                if (pad)
                    pending.DefaultPad();
                else
                    pending.DefaultKeys();
                ClearNotice();
                Se(AK.EVENTS.UI_COMMON_DONE);
                RefreshPageRows();
                window.PcRefreshDescription();
                return true;
            }
            return false;
        }

        static void Captured(SettingWindow window, SettingMenuItem item, KeyCode key, PadControl control)
        {
            if (item == null || (!IsKeyRow(item) && !IsPadRow(item)))
                return;
            PcAction action = ActionOf(item), owner;
            bool pad = IsPadRow(item);
            if (key == KeyCode.None && control == PadControl.None)
            {
                Se(AK.EVENTS.UI_COMMON_CANCEL);   // Esc or 5 seconds
                window.PcRefreshDescription();
                return;
            }
            PcAction? before = pad ? PadOwner(control) : KeyOwner(key);
            bool ok = pad ? PcBindings.AssignPad(pending, action, control, true, out owner)
                          : PcBindings.AssignKey(pending, action, item.selectIndex, key, out owner);
            if (!ok)
            {
                SetNotice(item, F(pad ? K.NeedPad : K.NeedKey, owner), S(K.Need2));
                Se(AK.EVENTS.UI_COMMON_BEEP);
            }
            else
            {
                if (before.HasValue && before.Value != action)
                    SetNotice(item, F(K.Swapped, before.Value), null);
                Se(AK.EVENTS.UI_COMMON_DONE);
            }
            RefreshPageRows();
            window.PcRefreshDescription();
        }

        static PcAction? KeyOwner(KeyCode key)
        {
            for (int a = 0; a < PcBindingTable.Actions; a++)
                if (pending.keys[a, 0] == key || pending.keys[a, 1] == key)
                    return (PcAction)a;
            return null;
        }

        static PcAction? PadOwner(PadControl control)
        {
            for (int a = 0; a < PcBindingTable.Actions; a++)
                if (pending.pad[a] == control)
                    return (PcAction)a;
            return null;
        }

        static void OpenPage(SettingWindow window, Page p)
        {
            var rows = p == Page.Pad ? padRows : keyRows;
            if (rows.Count == 0)
            {
                foreach (var action in p == Page.Pad ? PadPage : KeyPage)
                {
                    var r = PcDisplayOptions.Row(content, (p == Page.Pad ? "PcPad" : "PcKey") + action, cursor);
                    if (r == null)
                    {
                        UnityEngine.Debug.LogWarning("[PcPlatform] controls options: no row to clone for the page");
                        rows.Clear();
                        return;
                    }
                    r.SetupPc((ConfigID)((p == Page.Pad ? PadRowBase : KeyRowBase) + (int)action), ActionName(action),
                        RowValues(action, p == Page.Pad), 0, onChanged);
                    r.gameObject.SetActive(false);
                    rows.Add(r);
                }
            }
            mainRows = window.PcActiveItems;
            mainSelect = window.PcSelectIndex;
            mainScroll = window.PcScrollIndex;
            page = p;
            ClearNotice();
            RefreshPageRows();
            Se(AK.EVENTS.UI_COMMON_DECIDE);
            window.PcShowRows(rows, 0, 0);
        }

        static void ClosePage(SettingWindow window)
        {
            page = Page.Main;
            ClearNotice();
            RefreshMainRows();
            Se(AK.EVENTS.UI_COMMON_CANCEL);
            window.PcShowRows(mainRows, mainSelect, mainScroll);
        }

        static void SetNotice(SettingMenuItem item, string line1, string line2)
        {
            noticeItem = item;
            notice1 = line1;
            notice2 = line2;
        }

        static void ClearNotice()
        {
            noticeItem = null;
            notice1 = notice2 = null;
        }

        static void Se(uint eventId)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySe(eventId, null);
        }

        // SettingWindow.OnMenuItemValueChaged (through PcDisplayOptions): true = one of these rows
        public static bool OnValueChanged(SettingMenuItem item)
        {
            if (!Owns(item))
                return false;
            if (item.configId == SpeedRow)
                speedIndex = item.selectIndex;
            else if (item.configId == KeyModeRow)
                keyModeIndex = item.selectIndex;
            else if (item.configId == LayoutRow && pending != null)
            {
                if (item.selectIndex <= 1 && PcBindings.Layout(pending) != (PcBindings.PadLayout)item.selectIndex)
                    PcBindings.SetLayout(pending, (PcBindings.PadLayout)item.selectIndex);
                layoutDirty = true;   // the row's values are rebuilt on the next update, not inside its own SetSelectIndex
            }
            // a keyboard row's value = the key slot that A / X act on; the other rows have one value
            if (item == noticeItem)
                ClearNotice();
            return true;
        }

        // something chosen here that is not in effect yet
        public static bool Pending
        {
            get
            {
                if (pending == null || speedItem == null)
                    return false;
                return PcGameSpeed.Speeds[speedIndex] != PcGameSpeed.Speed || (keyModeIndex == 1) != PcGameSpeed.ToggleMode
                    || !pending.SameKeys(PcBindings.Live) || !pending.SamePad(PcBindings.Live);
            }
        }

        // the shipped "save these settings?" yes
        public static void Accept()
        {
            if (pending == null || speedItem == null)
                return;
            if (PcGameSpeed.Speeds[speedIndex] != PcGameSpeed.Speed)
                PcGameSpeed.SetSpeed(PcGameSpeed.Speeds[speedIndex], true);
            if ((keyModeIndex == 1) != PcGameSpeed.ToggleMode)
                PcGameSpeed.SetToggleMode(keyModeIndex == 1, true);
            PcBindings.Apply(pending, true);
        }

        // the shipped Y reset of the main list: the fast-forward rows back to their defaults, applied at once; bindings untouched
        public static void ResetToDefault()
        {
            if (speedItem == null || keyModeItem == null)
                return;
            speedIndex = Math.Max(0, Array.IndexOf(PcGameSpeed.Speeds, PcGameSpeed.DefaultSpeed));
            keyModeIndex = PcGameSpeed.DefaultToggle ? 1 : 0;
            speedItem.SetSelectIndex(speedIndex, true, false);
            keyModeItem.SetSelectIndex(keyModeIndex, true, false);
            if (PcGameSpeed.Speeds[speedIndex] != PcGameSpeed.Speed)
                PcGameSpeed.SetSpeed(PcGameSpeed.Speeds[speedIndex], true);
            if ((keyModeIndex == 1) != PcGameSpeed.ToggleMode)
                PcGameSpeed.SetToggleMode(keyModeIndex == 1, true);
        }

        // the description under the list
        public static MessageTextParseDataModel Description(SettingMenuItem item, MessageMsgFile optionFile)
        {
            string l1, l2;
            if (item.configId == SpeedRow) { l1 = S(K.DescSpeed1); l2 = S(K.DescSpeed2); }
            else if (item.configId == KeyModeRow) { l1 = S(K.DescKey1); l2 = S(K.DescKey2); }
            else if (item.configId == LayoutRow) { l1 = S(K.DescLayout1); l2 = S(K.DescLayout2); }
            else if (item.configId == KeyboardRow) { l1 = S(K.DescKeyboard1); l2 = S(K.DescKeyboard2); }
            else if (item.configId == PadRow) { l1 = S(K.DescController1); l2 = S(K.DescKeyboard2); }
            else
            {
                bool pad = IsPadRow(item);
                if (PcBindings.Listening)
                {
                    l1 = F(pad ? K.ListenPad1 : K.ListenKey1, ActionOf(item));
                    l2 = S(pad ? K.ListenPad2 : K.ListenKey2);
                }
                else
                {
                    bool own = notice1 != null && noticeItem == item;
                    l1 = own ? notice1 : S(pad ? K.PagePad1 : K.PageKey1);
                    l2 = own && notice2 != null ? notice2 : S(pad ? K.PagePad2 : K.PageKey2);
                }
            }
            return PcDisplayOptions.TextModel(item, optionFile, l1, l2);
        }

        // ---- texts: the game's nine languages (+ Japanese kana mode); a text the current font cannot draw falls back to English ----

        enum K
        {
            TitleSpeed, TitleKey, TitleLayout, TitleKeyboard, TitleController,
            Hold, Toggle, Positions, Labels, Custom, Default,
            DescSpeed1, DescSpeed2, DescKey1, DescKey2, DescLayout1, DescLayout2, DescKeyboard1, DescKeyboard2, DescController1,
            PageKey1, PageKey2, PagePad1, PagePad2,
            ListenKey1, ListenKey2, ListenPad1, ListenPad2,
            NeedKey, NeedPad, Need2, Swapped,
            Action0,   // + (int)PcAction: the action names
        }

        const int Count = (int)K.Action0 + PcBindingTable.Actions;

        static string Raw(string code, int index) { return table[code][index]; }

        static string S(K key)
        {
            string code = PcDisplayOptions.LangCode();
            return code == "en" ? Raw("en", (int)key) : PcDisplayOptions.Drawable(Raw(code, (int)key), Raw("en", (int)key));
        }

        static string ActionName(PcAction a)
        {
            return S((K)((int)K.Action0 + (int)a));
        }

        // a text with an action name in it
        static string F(K key, PcAction a)
        {
            string code = PcDisplayOptions.LangCode();
            string en = string.Format(Raw("en", (int)key), Raw("en", (int)K.Action0 + (int)a));
            if (code == "en")
                return en;
            return PcDisplayOptions.Drawable(string.Format(Raw(code, (int)key), Raw(code, (int)K.Action0 + (int)a)), en);
        }

        static readonly string[] ActionsEn =
        {
            "A Button", "B Button", "X Button", "Y Button", "L Button", "R Button", "ZL Button", "ZR Button", "+ Button", "- Button",
            "L Stick Press", "R Stick Press", "D-Pad Up", "D-Pad Down", "D-Pad Left", "D-Pad Right",
            "L Stick Up", "L Stick Down", "L Stick Left", "L Stick Right", "R Stick Up", "R Stick Down", "R Stick Left", "R Stick Right",
            "Fast-Forward",
        };

        static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
        {
            { "en", new[] {
                "Fast-Forward Speed", "Fast-Forward Key", "Button Layout", "Keyboard Keys", "Controller Buttons",
                "Hold", "Toggle", "Switch Positions", "Xbox Labels", "Custom", "Default",
                "How fast the game runs while fast-forwarding.", "Contests, Poffins and movies stay at normal speed.",
                "Hold: fast-forward while the key is held down.", "Toggle: press once to start, again to stop.",
                "Switch Positions: the button in the Switch's spot.", "Xbox Labels: the button with the same letter.",
                "Press A to change the keyboard keys.", "Changes apply when you save settings.",
                "Press A to change the controller buttons.",
                "A: set key  X: clear  Left/Right: key 1 or 2", "Y: reset all keys  B: back",
                "A: set button  X: clear", "Y: reset all buttons  B: back",
                "Press a key for {0}.", "Esc or wait 5 seconds to cancel.",
                "Press a button for {0}.", "Wait 5 seconds to cancel.",
                "{0} needs at least one key.", "{0} needs a button.", "It is used to move through the menus.", "Swapped with {0}.",
            }.Concat(ActionsEn).ToArray() },
            { "jp_kanji", new[] {
                "早送りの速さ", "早送りキー", "ボタン配置", "キーボード設定", "コントローラー設定",
                "押している間", "切り替え", "Switchの位置", "Xboxの表記", "カスタム", "標準",
                "早送り中の　ゲームの速さを　選べます。", "コンテスト　ポフィン　ムービーは　等速です。",
                "押している間：キーを　押している間だけ　早送り。", "切り替え：押すたびに　早送りの　オンとオフ。",
                "Switchの位置：Switchと　同じ位置の　ボタン。", "Xboxの表記：同じ文字の　ボタン。",
                "Aで　キーボードの　キーを　変更できます。", "設定を　保存すると　反映されます。",
                "Aで　コントローラーの　ボタンを　変更できます。",
                "A：キー設定　X：解除　左右：キー1／キー2", "Y：すべて標準に戻す　B：戻る",
                "A：ボタン設定　X：解除", "Y：すべて標準に戻す　B：戻る",
                "{0}に　使う　キーを　押してください。", "Escか　5秒で　キャンセル。",
                "{0}に　使う　ボタンを　押してください。", "5秒で　キャンセル。",
                "{0}には　キーが　必要です。", "{0}には　ボタンが　必要です。", "メニューの　操作に　使います。", "{0}と　入れ替えました。",
                "Aボタン", "Bボタン", "Xボタン", "Yボタン", "Lボタン", "Rボタン", "ZLボタン", "ZRボタン", "+ボタン", "-ボタン",
                "Lスティック押し込み", "Rスティック押し込み", "十字ボタン上", "十字ボタン下", "十字ボタン左", "十字ボタン右",
                "Lスティック上", "Lスティック下", "Lスティック左", "Lスティック右", "Rスティック上", "Rスティック下", "Rスティック左", "Rスティック右",
                "早送り",
            } },
            { "jp_kana", new[] {
                "はやおくりの　はやさ", "はやおくりキー", "ボタンはいち", "キーボードせってい", "コントローラーせってい",
                "おしているあいだ", "きりかえ", "Switchのいち", "Xboxのひょうき", "カスタム", "ひょうじゅん",
                "はやおくりちゅうの　ゲームの　はやさを　えらべます。", "コンテスト　ポフィン　ムービーは　ふつうの　はやさです。",
                "おしているあいだ：キーを　おしている　あいだだけ　はやおくり。", "きりかえ：おすたびに　はやおくりの　オンと　オフ。",
                "Switchのいち：Switchと　おなじ　いちの　ボタン。", "Xboxのひょうき：おなじ　もじの　ボタン。",
                "Aで　キーボードの　キーを　へんこうできます。", "せっていを　ほぞんすると　はんえいされます。",
                "Aで　コントローラーの　ボタンを　へんこうできます。",
                "A：キーせってい　X：かいじょ　さゆう：キー1／キー2", "Y：すべて　ひょうじゅんに　もどす　B：もどる",
                "A：ボタンせってい　X：かいじょ", "Y：すべて　ひょうじゅんに　もどす　B：もどる",
                "{0}に　つかう　キーを　おしてください。", "Escか　5びょうで　キャンセル。",
                "{0}に　つかう　ボタンを　おしてください。", "5びょうで　キャンセル。",
                "{0}には　キーが　ひつようです。", "{0}には　ボタンが　ひつようです。", "メニューの　そうさに　つかいます。", "{0}と　いれかえました。",
                "Aボタン", "Bボタン", "Xボタン", "Yボタン", "Lボタン", "Rボタン", "ZLボタン", "ZRボタン", "+ボタン", "-ボタン",
                "Lスティックおしこみ", "Rスティックおしこみ", "じゅうじボタンうえ", "じゅうじボタンした", "じゅうじボタンひだり", "じゅうじボタンみぎ",
                "Lスティックうえ", "Lスティックした", "Lスティックひだり", "Lスティックみぎ", "Rスティックうえ", "Rスティックした", "Rスティックひだり", "Rスティックみぎ",
                "はやおくり",
            } },
            { "fr", new[] {
                "Vitesse d'avance rapide", "Touche d'avance rapide", "Disposition des boutons", "Touches du clavier", "Boutons de la manette",
                "Maintenir", "Basculer", "Positions Switch", "Lettres Xbox", "Personnalisé", "Par défaut",
                "Vitesse du jeu pendant l'avance rapide.", "Concours, Poffins et vidéos : vitesse normale.",
                "Maintenir : actif tant que la touche est enfoncée.", "Basculer : appuyez une fois pour lancer ou arrêter.",
                "Positions Switch : le bouton placé comme sur Switch.", "Lettres Xbox : le bouton portant la même lettre.",
                "A : changer les touches du clavier.", "Appliqué à l'enregistrement des réglages.",
                "A : changer les boutons de la manette.",
                "A : définir  X : effacer  G/D : touche 1 ou 2", "Y : tout réinitialiser  B : retour",
                "A : définir  X : effacer", "Y : tout réinitialiser  B : retour",
                "Appuyez sur une touche pour {0}.", "Échap ou 5 secondes pour annuler.",
                "Appuyez sur un bouton pour {0}.", "Attendez 5 secondes pour annuler.",
                "{0} doit garder au moins une touche.", "{0} doit garder un bouton.", "Il sert à naviguer dans les menus.", "Échangé avec {0}.",
                "Bouton A", "Bouton B", "Bouton X", "Bouton Y", "Bouton L", "Bouton R", "Bouton ZL", "Bouton ZR", "Bouton +", "Bouton -",
                "Stick L (appui)", "Stick R (appui)", "Croix haut", "Croix bas", "Croix gauche", "Croix droite",
                "Stick L haut", "Stick L bas", "Stick L gauche", "Stick L droite", "Stick R haut", "Stick R bas", "Stick R gauche", "Stick R droite",
                "Avance rapide",
            } },
            { "it", new[] {
                "Velocità avanti veloce", "Tasto avanti veloce", "Disposizione pulsanti", "Tasti della tastiera", "Pulsanti del controller",
                "Tieni premuto", "Attiva/disattiva", "Posizioni Switch", "Lettere Xbox", "Personalizzata", "Predefinita",
                "La velocità del gioco con l'avanti veloce.", "Gare, Poffin e filmati restano a velocità normale.",
                "Tieni premuto: attivo finché tieni premuto il tasto.", "Attiva/disattiva: premi per avviare o fermare.",
                "Posizioni Switch: il pulsante nella posizione Switch.", "Lettere Xbox: il pulsante con la stessa lettera.",
                "A: cambia i tasti della tastiera.", "Le modifiche si applicano al salvataggio.",
                "A: cambia i pulsanti del controller.",
                "A: imposta  X: cancella  Sin./Des.: tasto 1 o 2", "Y: ripristina tutto  B: indietro",
                "A: imposta  X: cancella", "Y: ripristina tutto  B: indietro",
                "Premi un tasto per {0}.", "Esc o attendi 5 secondi per annullare.",
                "Premi un pulsante per {0}.", "Attendi 5 secondi per annullare.",
                "{0} deve avere almeno un tasto.", "{0} deve avere un pulsante.", "Serve per muoversi nei menu.", "Scambiato con {0}.",
                "Pulsante A", "Pulsante B", "Pulsante X", "Pulsante Y", "Pulsante L", "Pulsante R", "Pulsante ZL", "Pulsante ZR", "Pulsante +", "Pulsante -",
                "Pressione stick L", "Pressione stick R", "Croce su", "Croce giù", "Croce sinistra", "Croce destra",
                "Stick L su", "Stick L giù", "Stick L sinistra", "Stick L destra", "Stick R su", "Stick R giù", "Stick R sinistra", "Stick R destra",
                "Avanti veloce",
            } },
            { "de", new[] {
                "Schnellvorlauf-Tempo", "Schnellvorlauf-Taste", "Tastenbelegung", "Tastatur-Tasten", "Controller-Knöpfe",
                "Halten", "Umschalten", "Switch-Positionen", "Xbox-Beschriftung", "Eigene", "Standard",
                "Wie schnell das Spiel im Schnellvorlauf läuft.", "Wettbewerbe, Knurspe und Videos: normales Tempo.",
                "Halten: aktiv, solange die Taste gedrückt ist.", "Umschalten: einmal drücken zum Starten und Stoppen.",
                "Switch-Positionen: der Knopf an der Switch-Stelle.", "Xbox-Beschriftung: Knopf mit gleichem Buchstaben.",
                "A: Tastatur-Tasten ändern.", "Änderungen gelten nach dem Speichern.",
                "A: Controller-Knöpfe ändern.",
                "A: belegen  X: löschen  Links/Rechts: Taste 1 o. 2", "Y: alles zurücksetzen  B: zurück",
                "A: belegen  X: löschen", "Y: alles zurücksetzen  B: zurück",
                "Drücke eine Taste für {0}.", "Esc oder 5 Sekunden warten zum Abbrechen.",
                "Drücke einen Knopf für {0}.", "5 Sekunden warten zum Abbrechen.",
                "{0} braucht mindestens eine Taste.", "{0} braucht einen Knopf.", "Damit bewegst du dich durch die Menüs.", "Mit {0} getauscht.",
                "A-Knopf", "B-Knopf", "X-Knopf", "Y-Knopf", "L-Knopf", "R-Knopf", "ZL-Knopf", "ZR-Knopf", "Plus-Knopf", "Minus-Knopf",
                "L-Stick drücken", "R-Stick drücken", "Steuerkreuz oben", "Steuerkreuz unten", "Steuerkreuz links", "Steuerkreuz rechts",
                "L-Stick oben", "L-Stick unten", "L-Stick links", "L-Stick rechts", "R-Stick oben", "R-Stick unten", "R-Stick links", "R-Stick rechts",
                "Schnellvorlauf",
            } },
            { "es", new[] {
                "Velocidad de avance rápido", "Tecla de avance rápido", "Disposición de botones", "Teclas del teclado", "Botones del mando",
                "Mantener", "Alternar", "Posiciones Switch", "Letras Xbox", "Personalizada", "Predeterminada",
                "La velocidad del juego en avance rápido.", "Concursos, Pokochos y vídeos: velocidad normal.",
                "Mantener: activo mientras mantienes la tecla.", "Alternar: pulsa una vez para activar o desactivar.",
                "Posiciones Switch: el botón en el lugar de Switch.", "Letras Xbox: el botón con la misma letra.",
                "A: cambiar las teclas del teclado.", "Los cambios se aplican al guardar los ajustes.",
                "A: cambiar los botones del mando.",
                "A: asignar  X: borrar  Izq./Der.: tecla 1 o 2", "Y: restablecer todo  B: volver",
                "A: asignar  X: borrar", "Y: restablecer todo  B: volver",
                "Pulsa una tecla para {0}.", "Esc o espera 5 segundos para cancelar.",
                "Pulsa un botón para {0}.", "Espera 5 segundos para cancelar.",
                "{0} necesita al menos una tecla.", "{0} necesita un botón.", "Se usa para moverse por los menús.", "Intercambiado con {0}.",
                "Botón A", "Botón B", "Botón X", "Botón Y", "Botón L", "Botón R", "Botón ZL", "Botón ZR", "Botón +", "Botón -",
                "Pulsar stick L", "Pulsar stick R", "Cruceta arriba", "Cruceta abajo", "Cruceta izquierda", "Cruceta derecha",
                "Stick L arriba", "Stick L abajo", "Stick L izquierda", "Stick L derecha", "Stick R arriba", "Stick R abajo", "Stick R izquierda", "Stick R derecha",
                "Avance rápido",
            } },
            { "ko", new[] {
                "빨리 감기 속도", "빨리 감기 키", "버튼 배치", "키보드 키", "컨트롤러 버튼",
                "누르는 동안", "전환", "Switch 위치", "Xbox 표기", "사용자 설정", "기본",
                "빨리 감기 중의 게임 속도를 선택할 수 있습니다.", "콘테스트, 포핀, 동영상은 보통 속도입니다.",
                "누르는 동안: 키를 누르고 있는 동안 빨리 감기.", "전환: 누를 때마다 빨리 감기를 켜고 끕니다.",
                "Switch 위치: Switch와 같은 위치의 버튼.", "Xbox 표기: 같은 글자가 적힌 버튼.",
                "A로 키보드 키를 변경할 수 있습니다.", "설정을 저장하면 변경 사항이 적용됩니다.",
                "A로 컨트롤러 버튼을 변경할 수 있습니다.",
                "A: 키 설정  X: 해제  좌우: 키 1 / 키 2", "Y: 모두 기본으로  B: 돌아가기",
                "A: 버튼 설정  X: 해제", "Y: 모두 기본으로  B: 돌아가기",
                "{0}에 사용할 키를 누르세요.", "Esc 또는 5초 후 취소됩니다.",
                "{0}에 사용할 버튼을 누르세요.", "5초 후 취소됩니다.",
                "{0}에는 키가 필요합니다.", "{0}에는 버튼이 필요합니다.", "메뉴 조작에 사용됩니다.", "{0}와(과) 교체했습니다.",
                "A 버튼", "B 버튼", "X 버튼", "Y 버튼", "L 버튼", "R 버튼", "ZL 버튼", "ZR 버튼", "+ 버튼", "- 버튼",
                "L 스틱 누르기", "R 스틱 누르기", "십자 버튼 위", "십자 버튼 아래", "십자 버튼 왼쪽", "십자 버튼 오른쪽",
                "L 스틱 위", "L 스틱 아래", "L 스틱 왼쪽", "L 스틱 오른쪽", "R 스틱 위", "R 스틱 아래", "R 스틱 왼쪽", "R 스틱 오른쪽",
                "빨리 감기",
            } },
            { "sch", new[] {
                "快进速度", "快进键", "按键布局", "键盘按键", "手柄按键",
                "按住", "切换", "Switch位置", "Xbox标识", "自定义", "默认",
                "可以选择快进时的游戏速度。", "华丽大赛、宝芬和影片保持正常速度。",
                "按住：按住按键时快进。", "切换：每按一次开启或关闭快进。",
                "Switch位置：与Switch位置相同的按键。", "Xbox标识：字母相同的按键。",
                "按A可以更改键盘按键。", "保存设置后即会生效。",
                "按A可以更改手柄按键。",
                "A：设置　X：清除　左右：按键1／按键2", "Y：全部恢复默认　B：返回",
                "A：设置　X：清除", "Y：全部恢复默认　B：返回",
                "请按下用于{0}的键。", "按Esc或等待5秒取消。",
                "请按下用于{0}的按键。", "等待5秒取消。",
                "{0}至少需要一个键。", "{0}需要一个按键。", "用于操作菜单。", "已与{0}互换。",
                "A键", "B键", "X键", "Y键", "L键", "R键", "ZL键", "ZR键", "+键", "-键",
                "按下L摇杆", "按下R摇杆", "十字键上", "十字键下", "十字键左", "十字键右",
                "L摇杆上", "L摇杆下", "L摇杆左", "L摇杆右", "R摇杆上", "R摇杆下", "R摇杆左", "R摇杆右",
                "快进",
            } },
            { "tch", new[] {
                "快轉速度", "快轉鍵", "按鍵配置", "鍵盤按鍵", "控制器按鍵",
                "按住", "切換", "Switch位置", "Xbox標示", "自訂", "預設",
                "可以選擇快轉時的遊戲速度。", "華麗大賽、寶芬和影片維持正常速度。",
                "按住：按住按鍵時快轉。", "切換：每按一次開啟或關閉快轉。",
                "Switch位置：與Switch位置相同的按鍵。", "Xbox標示：字母相同的按鍵。",
                "按A可以變更鍵盤按鍵。", "儲存設定後即會生效。",
                "按A可以變更控制器按鍵。",
                "A：設定　X：清除　左右：按鍵1／按鍵2", "Y：全部恢復預設　B：返回",
                "A：設定　X：清除", "Y：全部恢復預設　B：返回",
                "請按下用於{0}的鍵。", "按Esc或等待5秒取消。",
                "請按下用於{0}的按鍵。", "等待5秒取消。",
                "{0}至少需要一個鍵。", "{0}需要一個按鍵。", "用於操作選單。", "已與{0}互換。",
                "A鍵", "B鍵", "X鍵", "Y鍵", "L鍵", "R鍵", "ZL鍵", "ZR鍵", "+鍵", "-鍵",
                "按下L搖桿", "按下R搖桿", "十字鍵上", "十字鍵下", "十字鍵左", "十字鍵右",
                "L搖桿上", "L搖桿下", "L搖桿左", "L搖桿右", "R搖桿上", "R搖桿下", "R搖桿左", "R搖桿右",
                "快轉",
            } },
        };

        // a short table is a programming error: say so once, loudly, instead of an IndexOutOfRange in the options
        static PcControlsOptions()
        {
            foreach (var kv in table)
                if (kv.Value.Length != Count)
                    UnityEngine.Debug.LogError("[PcPlatform] controls options: the '" + kv.Key + "' text table has " + kv.Value.Length + " entries, not " + Count);
        }
    }
}
#endif
