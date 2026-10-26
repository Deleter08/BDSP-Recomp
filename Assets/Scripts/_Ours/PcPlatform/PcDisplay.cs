#if !SWITCH
// PC platform layer: display mode, resolution and frame rate, as the FIRST THREE rows of the in-game Options menu (author requests s39:
// "add Window/Borderless Window/Full screen/Resolution select mode in in-game option-menu", then "Make the window type/resolution/
// framerate settings the first three entries of option menu"), + V-Sync as the fourth row (author: "The screen is tearing clearly
// while watching the opening video"). NOT shipped code - the Switch game has a fixed output and 30 fps.
//
// Stored in <persistentDataPath>/pc_settings.ini (PcSettingsIni): `display=windowed|borderless|fullscreen`, `resolution=<w>x<h>` and
// the frame-rate cap `fps=<n>` (0 = unlimited, PcFrameRate). NOT in the save data (CONFIG keeps the shipped layout). At boot the
// display entries are applied unless the command line sets the screen itself (-screen-fullscreen / -screen-width / -screen-height /
// -window-mode / -popupwindow); without them the player starts as Unity does (ProjectSettings: borderless full screen at the desktop
// resolution, or Unity's last-run memory).
//
// The Options rows (PcDisplayOptions) are clones of the shipped "WindowChange" row (arrow selector), so they look, move and sound like
// the shipped rows; SettingWindow / SettingMenuItem call in under `#if !SWITCH`. The clones are kept at the END of the list's children
// while the shipped loop maps child index -> ConfigID, then moved to the top. Their values are applied when the player saves the
// settings (the shipped "save these settings?" yes), discarded on "no"; the Y reset applies Borderless / desktop resolution /
// unlimited at once, like the shipped reset applies CONFIG at once. Pseudo ConfigIDs 100-102 mark the rows; they never reach CONFIG.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dpr.Message;
using Dpr.UI;
using UnityEngine;
using UnityEngine.Events;

namespace PcPlatform
{
    public static class PcDisplay
    {
        public enum Mode : int
        {
            Windowed = 0,
            Borderless = 1,
            FullScreen = 2,
        }

        public static Mode DefaultMode { get { return Mode.Borderless; } }   // = ProjectSettings fullscreenMode FullScreenWindow
        public static Vector2Int DefaultSize { get { return NativeSize; } }
        public static Vector2Int NativeSize { get { return new Vector2Int(Display.main.systemWidth, Display.main.systemHeight); } }

        static readonly string[] CommandLineScreenArgs = { "-screen-fullscreen", "-screen-width", "-screen-height", "-window-mode", "-popupwindow" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            Mode mode;
            Vector2Int size;
            bool haveMode, haveSize;
            Read(out mode, out haveMode, out size, out haveSize);
            var args = Environment.GetCommandLineArgs();
            string fromCommandLine = CommandLineScreenArgs.FirstOrDefault(a => args.Any(x => string.Equals(x, a, StringComparison.OrdinalIgnoreCase)));
            if (fromCommandLine != null)
            {
                PcConfig.Log("display: the command line sets the screen (" + fromCommandLine + ") - pc_settings.ini display / resolution not applied");
                return;
            }
            if (!haveMode && !haveSize)
            {
                PcConfig.Log("display: " + Describe(Current, CurrentSize) + " (pc_settings.ini has no display / resolution; Options > Display Mode / Resolution sets them)");
                return;
            }
            Apply(haveMode ? mode : Current, haveSize ? size : CurrentSize, false);
        }

        // what the screen is now (a maximized window counts as Windowed)
        public static Mode Current
        {
            get
            {
                switch (Screen.fullScreenMode)
                {
                    case FullScreenMode.ExclusiveFullScreen: return Mode.FullScreen;
                    case FullScreenMode.FullScreenWindow: return Mode.Borderless;
                    default: return Mode.Windowed;
                }
            }
        }

        public static Vector2Int CurrentSize { get { return new Vector2Int(Screen.width, Screen.height); } }

        static FullScreenMode ToUnity(Mode mode)
        {
            switch (mode)
            {
                case Mode.FullScreen: return FullScreenMode.ExclusiveFullScreen;
                case Mode.Borderless: return FullScreenMode.FullScreenWindow;
                default: return FullScreenMode.Windowed;
            }
        }

        public static string Describe(Mode mode, Vector2Int size)
        {
            return mode + " " + size.x + "x" + size.y;
        }

        public static void Apply(Mode mode, Vector2Int size, bool save)
        {
            if (size.x <= 0 || size.y <= 0)
                size = NativeSize;
            Screen.SetResolution(size.x, size.y, ToUnity(mode));
            PcConfig.Log("display: " + Describe(mode, size) + (save ? " (saved to pc_settings.ini)" : " (pc_settings.ini)"));
            if (save)
                PcSettingsIni.Set("# display: windowed | borderless | fullscreen; resolution: <width>x<height> (Options > Display Mode / Resolution)",
                    "display=" + (mode == Mode.Windowed ? "windowed" : mode == Mode.Borderless ? "borderless" : "fullscreen"),
                    "resolution=" + size.x + "x" + size.y);
        }

        // every selectable size: the monitor's modes from 1280x720 up + the common 16:9 sizes that fit + the desktop size, ascending
        public static List<Vector2Int> Sizes(Vector2Int keep)
        {
            var native = NativeSize;
            var set = new HashSet<Vector2Int>();
            foreach (var r in Screen.resolutions)
                if (r.width >= 1280 && r.height >= 720 && r.width <= native.x && r.height <= native.y)
                    set.Add(new Vector2Int(r.width, r.height));
            foreach (var s in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440),
                                      new Vector2Int(3200, 1800), new Vector2Int(3840, 2160) })
                if (s.x <= native.x && s.y <= native.y)
                    set.Add(s);
            set.Add(native);
            if (keep.x > 0 && keep.y > 0)
                set.Add(keep);
            return set.OrderBy(v => v.x).ThenBy(v => v.y).ToList();
        }

        static void Read(out Mode mode, out bool haveMode, out Vector2Int size, out bool haveSize)
        {
            mode = DefaultMode;
            size = Vector2Int.zero;
            haveMode = haveSize = false;
            string v = PcSettingsIni.Get("display");
            if (v != null)
            {
                haveMode = true;
                v = v.ToLowerInvariant();
                if (v == "windowed") mode = Mode.Windowed;
                else if (v == "borderless") mode = Mode.Borderless;
                else if (v == "fullscreen") mode = Mode.FullScreen;
                else haveMode = false;
            }
            v = PcSettingsIni.Get("resolution");
            if (v != null)
            {
                var p = v.ToLowerInvariant().Split('x');
                int w, h;
                if (p.Length == 2 && int.TryParse(p[0].Trim(), out w) && int.TryParse(p[1].Trim(), out h) && w >= 640 && h >= 360)
                {
                    size = new Vector2Int(w, h);
                    haveSize = true;
                }
            }
        }
    }

    // <persistentDataPath>/pc_settings.ini: `key=value` lines, '#' comments; a write keeps every other line as it is.
    public static class PcSettingsIni
    {
        public static string Get(string key)
        {
            try
            {
                if (!File.Exists(PcFrameRate.SettingsPath))
                    return null;
                foreach (var raw in File.ReadAllLines(PcFrameRate.SettingsPath))
                {
                    var line = raw.Trim();
                    if (line.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
                        return line.Substring(key.Length + 1).Trim();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PcPlatform] pc_settings.ini: " + e.Message);
            }
            return null;
        }

        // every key=value line (keys case-insensitive; a repeated key: the first line wins, as Get)
        public static Dictionary<string, string> ReadAll()
        {
            var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(PcFrameRate.SettingsPath))
                    return all;
                foreach (var raw in File.ReadAllLines(PcFrameRate.SettingsPath))
                {
                    var line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (line.StartsWith("#") || eq <= 0)
                        continue;
                    string key = line.Substring(0, eq).Trim();
                    if (!all.ContainsKey(key))
                        all[key] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PcPlatform] pc_settings.ini: " + e.Message);
            }
            return all;
        }

        // each entry "key=value" replaces that key's line or is appended (after `comment`, when one is given and something is appended)
        public static void Set(string comment, params string[] entries)
        {
            try
            {
                var lines = File.Exists(PcFrameRate.SettingsPath) ? File.ReadAllLines(PcFrameRate.SettingsPath).ToList() : new List<string>();
                var append = new List<string>();
                foreach (var entry in entries)
                {
                    string key = entry.Substring(0, entry.IndexOf('=') + 1);
                    int at = lines.FindIndex(l => l.Trim().StartsWith(key, StringComparison.OrdinalIgnoreCase));
                    if (at >= 0)
                        lines[at] = entry;
                    else
                        append.Add(entry);
                }
                if (append.Count > 0 && comment != null)
                    lines.Add(comment);
                lines.AddRange(append);
                File.WriteAllText(PcFrameRate.SettingsPath, string.Join("\n", lines.ToArray()) + "\n");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[PcPlatform] pc_settings.ini: " + e.Message);
            }
        }
    }

    // The display Options rows (called by Dpr.UI.SettingWindow under #if !SWITCH); since s47 also the entry point of the controls /
    // fast-forward rows and pages (PcControlsOptions), which follow the display rows.
    public static class PcDisplayOptions
    {
        public const ConfigID ModeRow = (ConfigID)100;
        public const ConfigID SizeRow = (ConfigID)101;
        public const ConfigID FpsRow = (ConfigID)102;
        public const ConfigID VSyncRow = (ConfigID)103;
        const string TemplateRow = "WindowChange";   // the shipped arrow-selector row (ConfigID.WindowType)
        static readonly string[] RowNames = { "PcDisplayMode", "PcResolution", "PcFrameRate", "PcVSync" };

        static List<Vector2Int> sizes = new List<Vector2Int>();
        static List<int> rates = new List<int>();     // frame-rate caps, 0 = unlimited (last)
        static int modeIndex, sizeIndex, rateIndex, vsyncIndex;   // vsyncIndex 0 = On, 1 = Off
        static SettingMenuItem modeItem, sizeItem, rateItem, vsyncItem;

        public static bool IsPcRow(SettingMenuItem item)
        {
            return item != null && (item.configId == ModeRow || item.configId == SizeRow || item.configId == FpsRow || item.configId == VSyncRow
                || PcControlsOptions.Owns(item));
        }

        static int CurrentCap
        {
            get
            {
#if PC_UNLOCKED_FPS
                return PcFrameRate.Cap;
#else
                return PcFrameClock.LogicRate;   // the shipped 30 fps: the row shows it and changes nothing
#endif
            }
        }

        static bool CurrentVSync
        {
            get
            {
#if PC_UNLOCKED_FPS
                return PcFrameRate.VSync;
#else
                return true;   // the shipped pace runs on vSync (vSyncCount 2): the row shows On and changes nothing
#endif
            }
        }

        // something chosen that is not in effect yet
        public static bool Pending
        {
            get
            {
                if (PcControlsOptions.Pending)
                    return true;
                if (sizes.Count == 0 || rates.Count == 0)
                    return false;
                return (PcDisplay.Mode)modeIndex != PcDisplay.Current || sizes[sizeIndex] != PcDisplay.CurrentSize || rates[rateIndex] != CurrentCap
                    || (vsyncIndex == 0) != CurrentVSync;
            }
        }

        // Before the shipped loop (child index = ConfigID): the PC rows of an earlier opening go back to the end of the list.
        public static void MoveRowsToEnd(Transform content)
        {
            foreach (var name in RowNames)
            {
                var t = content.Find(name);
                if (t != null)
                    t.SetAsLastSibling();
            }
            PcControlsOptions.MoveRowsToEnd(content);
        }

        // After the shipped loop: creates (once per window instance) and sets up the four rows, puts them at the top of the list and
        // at the front of `activeItems`.
        public static void Open(Transform content, List<SettingMenuItem> activeItems, UnityAction<SettingMenuItem> onValueChanged, Transform cursor)
        {
            var current = PcDisplay.CurrentSize;
            sizes = PcDisplay.Sizes(current);
            modeIndex = (int)PcDisplay.Current;
            sizeIndex = Math.Max(0, sizes.IndexOf(current));
            rates = Rates();
            rateIndex = Math.Max(0, rates.IndexOf(CurrentCap));
            vsyncIndex = CurrentVSync ? 0 : 1;

            modeItem = Row(content, RowNames[0], cursor);
            sizeItem = Row(content, RowNames[1], cursor);
            rateItem = Row(content, RowNames[2], cursor);
            vsyncItem = Row(content, RowNames[3], cursor);
            if (modeItem == null || sizeItem == null || rateItem == null || vsyncItem == null)
            {
                UnityEngine.Debug.LogWarning("[PcPlatform] display options: no '" + TemplateRow + "' row to clone");
                return;
            }
            var lang = Strings.Lang();
            modeItem.SetupPc(ModeRow, Strings.Get(lang, Strings.Key.TitleMode),
                new[] { Strings.Get(lang, Strings.Key.Windowed), Strings.Get(lang, Strings.Key.Borderless), Strings.Get(lang, Strings.Key.FullScreen) },
                modeIndex, onValueChanged);
            sizeItem.SetupPc(SizeRow, Strings.Get(lang, Strings.Key.TitleResolution), sizes.Select(s => s.x + " x " + s.y).ToArray(), sizeIndex, onValueChanged);
            string unlimited = Strings.Get(lang, Strings.Key.Unlimited);
            rateItem.SetupPc(FpsRow, Strings.Get(lang, Strings.Key.TitleFps), rates.Select(r => r > 0 ? r + " FPS" : unlimited).ToArray(), rateIndex, onValueChanged);
            vsyncItem.SetupPc(VSyncRow, Strings.Get(lang, Strings.Key.TitleVSync), new[] { Strings.Get(lang, Strings.Key.On), Strings.Get(lang, Strings.Key.Off) },
                vsyncIndex, onValueChanged);

            modeItem.transform.SetSiblingIndex(0);
            sizeItem.transform.SetSiblingIndex(1);
            rateItem.transform.SetSiblingIndex(2);
            vsyncItem.transform.SetSiblingIndex(3);
            activeItems.InsertRange(0, new[] { modeItem, sizeItem, rateItem, vsyncItem });

            // s47: Fast-Forward Speed / Key, Button Layout, Keyboard Keys, Controller Buttons follow
            PcControlsOptions.Open(content, activeItems, onValueChanged, cursor, 4);
        }

        // 30 (the shipped pace), the common display rates, the monitor's own rate, the current cap, then Unlimited
        static List<int> Rates()
        {
#if PC_UNLOCKED_FPS
            var set = new HashSet<int> { 30, 60, 120, 144, 165, 240 };
            int refresh = Screen.currentResolution.refreshRate;
            if (refresh > 30)
                set.Add(refresh);
            if (PcFrameRate.Cap > 0)
                set.Add(PcFrameRate.Cap);
            var list = set.OrderBy(x => x).ToList();
            list.Add(0);
            return list;
#else
            return new List<int> { PcFrameClock.LogicRate };
#endif
        }

        // the row `name` under `content`: found, or cloned from the shipped WindowChange row (PcControlsOptions makes its rows here too)
        internal static SettingMenuItem Row(Transform content, string name, Transform cursor)
        {
            var existing = content.Find(name);
            if (existing != null)
                return existing.GetComponent<SettingMenuItem>();
            var template = content.Find(TemplateRow);
            if (template == null)
                return null;
            // the menu cursor is parented to the selected row: never clone it along
            Transform cursorParent = null;
            if (cursor != null && cursor.IsChildOf(template))
            {
                cursorParent = cursor.parent;
                cursor.SetParent(content.parent, false);
            }
            var go = UnityEngine.Object.Instantiate(template.gameObject, content, false);
            if (cursorParent != null)
                cursor.SetParent(cursorParent, false);
            go.name = name;
            go.transform.SetAsLastSibling();
            go.SetActive(true);
            return go.GetComponent<SettingMenuItem>();
        }

        // SettingWindow.OnMenuItemValueChaged: true = a PC row (CONFIG untouched)
        public static bool OnValueChanged(SettingMenuItem item)
        {
            if (PcControlsOptions.OnValueChanged(item))
                return true;
            if (item.configId == ModeRow)
                modeIndex = item.selectIndex;
            else if (item.configId == SizeRow)
                sizeIndex = item.selectIndex;
            else if (item.configId == FpsRow)
                rateIndex = item.selectIndex;
            else if (item.configId == VSyncRow)
                vsyncIndex = item.selectIndex;
            else
                return false;
            return true;
        }

        // the shipped "save these settings?" yes
        public static void Accept()
        {
            PcControlsOptions.Accept();
            AcceptDisplay();
        }

        static void AcceptDisplay()
        {
            if (sizes.Count == 0 || rates.Count == 0)
                return;
            if ((PcDisplay.Mode)modeIndex != PcDisplay.Current || sizes[sizeIndex] != PcDisplay.CurrentSize)
                PcDisplay.Apply((PcDisplay.Mode)modeIndex, sizes[sizeIndex], true);
#if PC_UNLOCKED_FPS
            if (rates[rateIndex] != PcFrameRate.Cap)
                PcFrameRate.SetCap(rates[rateIndex], true);
            if ((vsyncIndex == 0) != PcFrameRate.VSync)
                PcFrameRate.SetVSync(vsyncIndex == 0, true);
#endif
        }

        // the shipped Y reset: Borderless, the desktop resolution, Unlimited, V-Sync on - applied at once (s47: + the fast-forward rows;
        // the key / button bindings stay - their pages have their own reset)
        public static void ResetToDefault()
        {
            PcControlsOptions.ResetToDefault();
            if (modeItem == null || sizeItem == null || rateItem == null || vsyncItem == null || sizes.Count == 0 || rates.Count == 0)
                return;
            modeIndex = (int)PcDisplay.DefaultMode;
            int i = sizes.IndexOf(PcDisplay.DefaultSize);
            sizeIndex = i >= 0 ? i : sizes.Count - 1;
            rateIndex = rates.Count - 1;   // Unlimited (the author's default, s37); the shipped-pace build has the one entry 30
            modeItem.SetSelectIndex(modeIndex, true, false);
            sizeItem.SetSelectIndex(sizeIndex, true, false);
            rateItem.SetSelectIndex(rateIndex, true, false);
            vsyncIndex = 0;
            vsyncItem.SetSelectIndex(vsyncIndex, true, false);
            AcceptDisplay();
        }

        // the description under the list for a PC row (null = a shipped row): a text model built like a shipped two-line option
        // description (style + attributes of SS_option_005; word widths -1 = measured at runtime with the current font)
        public static MessageTextParseDataModel Description(SettingMenuItem item, MessageMsgFile optionFile)
        {
            if (!IsPcRow(item) || optionFile == null)
                return null;
            if (PcControlsOptions.Owns(item))
                return PcControlsOptions.Description(item, optionFile);
            var lang = Strings.Lang();
            Strings.Key k1, k2;
            if (item.configId == ModeRow) { k1 = Strings.Key.DescMode1; k2 = Strings.Key.DescMode2; }
            else if (item.configId == SizeRow) { k1 = Strings.Key.DescRes1; k2 = Strings.Key.DescRes2; }
            else if (item.configId == FpsRow) { k1 = Strings.Key.DescFps1; k2 = Strings.Key.DescFps2; }
            else { k1 = Strings.Key.DescVSync1; k2 = Strings.Key.DescVSync2; }
            return TextModel(item, optionFile, Strings.Get(lang, k1), Strings.Get(lang, k2));
        }

        // a two-line description text model for a PC row
        internal static MessageTextParseDataModel TextModel(SettingMenuItem item, MessageMsgFile optionFile, string line1, string line2)
        {
            var src = optionFile.GetAllLabelDataArray().FirstOrDefault(x => x != null && x.labelName == "SS_option_005");
            var label = new LabelData
            {
                labelIndex = src != null ? src.labelIndex : 0,
                arrayIndex = src != null ? src.arrayIndex : 0,
                labelName = "PC_" + item.configId,
                styleInfo = src != null ? src.styleInfo : new StyleInfo { fontSize = 54, maxWidth = 1080, colorIndex = -1 },
                attributeValueArray = src != null ? src.attributeValueArray : new int[0],
                tagDataArray = new TagData[0],
                wordDataArray = new[]
                {
                    new WordData { patternID = MessageEnumData.WordDataPatternID.Event, eventID = MessageEnumData.MsgEventID.NewLine, str = line1, strWidth = -1f },
                    new WordData { patternID = MessageEnumData.WordDataPatternID.Str, eventID = MessageEnumData.MsgEventID.End, str = line2, strWidth = -1f },
                },
            };
            var model = new MessageTextParseDataModel();
            model.SetLabelData(label, optionFile.LanguageID);
            return model;
        }

        // the table column of the current language: en, jp_kanji, jp_kana, fr, it, de, es, ko, sch, tch (PcControlsOptions too)
        internal static string LangCode()
        {
            return Strings.Code(Strings.Lang());
        }

        // `s` when the current font draws it, else `english`
        internal static string Drawable(string s, string english)
        {
            return Strings.Drawable(Strings.Lang(), s) ? s : english;
        }

        // The row texts in the game's nine languages (+ Japanese kana mode). A string the current font cannot draw falls back to English.
        static class Strings
        {
            public enum Key
            {
                TitleMode, TitleResolution, Windowed, Borderless, FullScreen, DescMode1, DescMode2, DescRes1, DescRes2,
                TitleFps, Unlimited, DescFps1, DescFps2,
                TitleVSync, On, Off, DescVSync1, DescVSync2,
            }

            static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>
            {
                { "en", new[] { "Display Mode", "Resolution", "Windowed", "Borderless Window", "Full Screen",
                                "Choose whether the game is shown in a window,", "a borderless window, or full screen.",
                                "Choose the resolution the game is shown at.", "Display changes apply when you save settings.",
                                "Frame Rate", "Unlimited",
                                "Choose the frame rate limit. 30 FPS matches", "the original game; higher rates look smoother.",
                                "V-Sync", "On", "Off",
                                "Matches frames to the monitor to stop screen", "tearing. Off lets the frame rate run freely." } },
                { "jp_kanji", new[] { "画面モード", "解像度", "ウィンドウ", "ボーダーレスウィンドウ", "フルスクリーン",
                                "ゲーム画面を　ウィンドウ　ボーダーレスウィンドウ", "フルスクリーンの　どれで　表示するか　選べます。",
                                "ゲーム画面の　解像度を　選べます。", "設定を　保存すると　画面に　反映されます。",
                                "フレームレート", "無制限",
                                "フレームレートの　上限を　選べます。", "30 FPSが　もとの　ゲームと　同じです。",
                                "垂直同期", "オン", "オフ",
                                "モニターに　合わせて　表示し　画面の", "ずれ（ティアリング）を　防ぎます。" } },
                { "jp_kana", new[] { "がめんモード", "かいぞうど", "ウィンドウ", "ボーダーレスウィンドウ", "フルスクリーン",
                                "ゲームがめんを　ウィンドウ　ボーダーレスウィンドウ", "フルスクリーンの　どれで　ひょうじするか　えらべます。",
                                "ゲームがめんの　かいぞうどを　えらべます。", "せっていを　ほぞんすると　がめんに　はんえいされます。",
                                "フレームレート", "むせいげん",
                                "フレームレートの　じょうげんを　えらべます。", "30 FPSが　もとの　ゲームと　おなじです。",
                                "すいちょくどうき", "オン", "オフ",
                                "モニターに　あわせて　ひょうじし　がめんの", "ずれ（ティアリング）を　ふせぎます。" } },
                { "fr", new[] { "Mode d'affichage", "Résolution", "Fenêtré", "Fenêtre sans bordure", "Plein écran",
                                "Choisissez d'afficher le jeu dans une fenêtre,", "une fenêtre sans bordure ou en plein écran.",
                                "Choisissez la résolution d'affichage du jeu.", "Appliquée à l'enregistrement des réglages.",
                                "Fréquence d'images", "Illimitée",
                                "Choisissez la fréquence d'images maximale.", "30 FPS correspond au jeu d'origine.",
                                "Synchro verticale", "Activée", "Désactivée",
                                "Synchronise l'image avec l'écran pour", "éviter les déchirures d'image." } },
                { "it", new[] { "Modalità schermo", "Risoluzione", "In finestra", "Finestra senza bordi", "Schermo intero",
                                "Scegli se mostrare il gioco in una finestra,", "in una finestra senza bordi o a schermo intero.",
                                "Scegli la risoluzione del gioco.", "Le modifiche si applicano al salvataggio.",
                                "Frequenza fotogrammi", "Illimitata",
                                "Scegli la frequenza massima dei fotogrammi.", "30 FPS corrisponde al gioco originale.",
                                "V-Sync", "Attivo", "Disattivo",
                                "Sincronizza le immagini con lo schermo", "per evitare lo screen tearing." } },
                { "de", new[] { "Anzeigemodus", "Auflösung", "Fenster", "Randloses Fenster", "Vollbild",
                                "Wähle, ob das Spiel in einem Fenster, in einem", "randlosen Fenster oder im Vollbild läuft.",
                                "Wähle die Auflösung, in der das Spiel läuft.", "Änderungen gelten nach dem Speichern.",
                                "Bildrate", "Unbegrenzt",
                                "Wähle die maximale Bildrate.", "30 FPS entspricht dem Originalspiel.",
                                "V-Sync", "An", "Aus",
                                "Gleicht die Bilder an den Monitor an,", "damit kein Screen-Tearing entsteht." } },
                { "es", new[] { "Modo de pantalla", "Resolución", "Ventana", "Ventana sin bordes", "Pantalla completa",
                                "Elige si quieres ver el juego en una ventana,", "en una ventana sin bordes o a pantalla completa.",
                                "Elige la resolución con la que se ve el juego.", "Los cambios se aplican al guardar los ajustes.",
                                "Tasa de fotogramas", "Ilimitada",
                                "Elige la tasa máxima de fotogramas.", "30 FPS es la del juego original.",
                                "V-Sync", "Activada", "Desactivada",
                                "Sincroniza la imagen con el monitor", "para evitar cortes en la pantalla." } },
                { "ko", new[] { "화면 모드", "해상도", "창 모드", "테두리 없는 창", "전체 화면",
                                "게임을 창 모드, 테두리 없는 창, 전체 화면 중", "어떤 방식으로 표시할지 선택할 수 있습니다.",
                                "게임 화면의 해상도를 선택할 수 있습니다.", "설정을 저장하면 변경 사항이 적용됩니다.",
                                "프레임 레이트", "제한 없음",
                                "프레임 레이트의 상한을 선택할 수 있습니다.", "30 FPS가 원래 게임과 같습니다.",
                                "수직 동기화", "켜기", "끄기",
                                "모니터와 화면을 동기화하여", "화면 찢어짐을 방지합니다." } },
                { "sch", new[] { "显示模式", "分辨率", "窗口", "无边框窗口", "全屏",
                                "可以选择以窗口、无边框窗口或全屏", "的方式显示游戏。",
                                "可以选择游戏画面的分辨率。", "保存设置后即会生效。",
                                "帧率", "无限制",
                                "可以选择帧率上限。", "30 FPS与原版游戏相同。",
                                "垂直同步", "开", "关",
                                "使画面与显示器同步，", "防止画面撕裂。" } },
                { "tch", new[] { "顯示模式", "解析度", "視窗", "無邊框視窗", "全螢幕",
                                "可以選擇以視窗、無邊框視窗或全螢幕", "的方式顯示遊戲。",
                                "可以選擇遊戲畫面的解析度。", "儲存設定後即會生效。",
                                "幀率", "無限制",
                                "可以選擇幀率上限。", "30 FPS與原版遊戲相同。",
                                "垂直同步", "開", "關",
                                "使畫面與螢幕同步，", "防止畫面撕裂。" } },
            };

            public static MessageEnumData.MsgLangId Lang()
            {
                return UIManager.GetCurrentLangId();
            }

            public static string Code(MessageEnumData.MsgLangId lang)
            {
                switch (lang)
                {
                    case MessageEnumData.MsgLangId.JPN:
                        return MessageManager.Instance != null && !MessageManager.Instance.IsKanji ? "jp_kana" : "jp_kanji";
                    case MessageEnumData.MsgLangId.FRA: return "fr";
                    case MessageEnumData.MsgLangId.ITA: return "it";
                    case MessageEnumData.MsgLangId.DEU: return "de";
                    case MessageEnumData.MsgLangId.ESP: return "es";
                    case MessageEnumData.MsgLangId.KOR: return "ko";
                    case MessageEnumData.MsgLangId.SCH: return "sch";
                    case MessageEnumData.MsgLangId.TCH: return "tch";
                    default: return "en";
                }
            }

            public static bool Drawable(MessageEnumData.MsgLangId lang, string s)
            {
                try
                {
                    return FontManager.Instance == null || !MessageHelper.ExistMissingChara(lang, s);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public static string Get(MessageEnumData.MsgLangId lang, Key key)
            {
                string code = Code(lang);
                string s = table[code][(int)key];
                if (code != "en" && !Drawable(lang, s))
                    s = table["en"][(int)key];
                return s;
            }
        }
    }
}
#endif
