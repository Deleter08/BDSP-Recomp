// BDSP play-test campaign, Phase 4 (HANDOFF §11.13): the Play-mode boot driver, editor-only PC tooling - not game code.
//   BdspPlay.Run (-executeMethod): opens Assets/Scenes/level0.unity (the boot scene), enters Play mode, records every log message
//       (type, text, stack for errors / exceptions / asserts) to ../work/s32/play_<time>_log.txt, samples the loaded scenes every 5 s,
//       saves Game-view screenshots, leaves Play mode after ../work/s32/play_seconds.txt seconds (default 120) and quits.
//       Summary -> ../work/s32/play_<time>.txt (counts, the first unique errors with their first stack, scene timeline,
//       AssetManager / UnityEditorProxy "not found" lines).
//   BdspPlay.Play / PlayPadDiag (-executeMethod or the BDSP/Play menu): a human plays - level0 in Play mode with the physical keyboard
//       and pads, no input script, no Time.captureFramerate, no time limit, nothing quits; the Game view maximized at 16:9.
//       PlayPadDiag also starts PcPlatform.PcPadDiag: every pad input -> <work>/pad_diag.txt (setup/play_mac.sh runs both).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BdspPlay
{
    const string Phase = "bdsp_play_phase", Stamp = "bdsp_play_stamp", T0 = "bdsp_play_t0";
    static readonly object gate = new object();
    static double nextSample, nextShot;
    static int shots;

    // Output folder: ../work/<session>/, the session read from ../work/play_session.txt (s33+), default s32 (the folder Phase 4 used).
    static string Work()
    {
        // the automated setup passes its work folder in BDSP_SETUP_WORK (output then goes to <work>/play)
        var setup = Environment.GetEnvironmentVariable("BDSP_SETUP_WORK");
        if (!string.IsNullOrEmpty(setup)) { var sd = Path.Combine(setup, "play"); Directory.CreateDirectory(sd); return sd; }
        string session = "s32";
        var f = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "play_session.txt"));
        if (File.Exists(f) && File.ReadAllText(f).Trim().Length > 0) session = File.ReadAllText(f).Trim();
        var d = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "work", session)); Directory.CreateDirectory(d); return d;
    }
    static string Base() => Path.Combine(Work(), "play_" + SessionState.GetString(Stamp, "x"));
    static double Now() => EditorApplication.timeSinceStartup;

    static BdspPlay()
    {
        EditorApplication.update += Tick;
        if (SessionState.GetString(Phase, "") != "")
        {
            CacheLogState();
            Application.logMessageReceivedThreaded += OnLog;
        }
    }

    [MenuItem("BDSP/Play/Boot from level0 (timed)")]
    public static void Run()
    {
        SessionState.SetString(Stamp, DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(Base() + "_log.txt", "BDSP play log, unity " + Application.unityVersion + "\n");
        EditorSceneManager.OpenScene("Assets/Scenes/level0.unity", OpenSceneMode.Single);
        SetGameViewResolution();
        SessionState.SetString(Phase, "enter");
        CacheLogState();
        Application.logMessageReceivedThreaded += OnLog;
        EditorApplication.EnterPlaymode();
    }

    // s46 C4: OnLog runs on any thread and also while Unity deserializes an asset (a warning raised during Resources.Load), where
    // SessionState / editor-time calls throw "not allowed to be called during serialization" - and that exception surfaced inside the
    // game's own Resources.Load. So the handler uses only values cached on the main thread (CacheLogState) plus a Stopwatch.
    static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    static volatile string logPath;
    static double playOffset = double.NaN;   // play time = clock seconds + playOffset; NaN = Play not entered yet

    static void CacheLogState()
    {
        logPath = Base() + "_log.txt";
        var t0 = SessionState.GetFloat(T0, 0f);
        playOffset = t0 > 0 ? Now() - t0 - clock.Elapsed.TotalSeconds : double.NaN;
    }

    static void OnLog(string condition, string stackTrace, LogType type)
    {
        try
        {
            var path = logPath;
            if (path == null)
                return;
            double off = playOffset;
            var sb = new StringBuilder();
            sb.Append(string.Format("[{0,7:F2}] {1}: {2}\n", double.IsNaN(off) ? 0f : (float)(clock.Elapsed.TotalSeconds + off), type, condition));
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                sb.Append("    " + (stackTrace ?? "").Replace("\n", "\n    ").TrimEnd() + "\n");
            lock (gate) File.AppendAllText(path, sb.ToString());
        }
        catch (Exception)
        {
            // never let the recorder throw into the code that logged
        }
    }

    // ---- interactive play (a human at the keyboard / pad) ----------------------------------------------------------------------

    const string Interactive = "bdsp_play_interactive";   // "" | "play" | "paddiag": what to start once Play mode is up

    [MenuItem("BDSP/Play/Play (keyboard + controller)")]
    public static void Play() { StartInteractive("play"); }

    [MenuItem("BDSP/Play/Play + controller diagnostic")]
    public static void PlayPadDiag() { StartInteractive("paddiag"); }

    static void StartInteractive(string mode)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[BdspPlay] already in Play mode");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene("Assets/Scenes/level0.unity", OpenSceneMode.Single);
        SessionState.SetString(Phase, "");   // never the timed driver's quit
        SessionState.SetString(Interactive, mode);
        PrepareGameView();
        Debug.Log("[BdspPlay] interactive play (" + mode + ")");
        EditorApplication.EnterPlaymode();
    }

    // after the domain reload of entering Play mode: a clean input state (no script), the real-time clock, the diagnostic
    static void TickInteractive()
    {
        var mode = SessionState.GetString(Interactive, "");
        if (mode == "" || !EditorApplication.isPlaying)
            return;
        SessionState.SetString(Interactive, "");
        Time.captureFramerate = 0;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var input = asm.GetType("PcPlatform.PcInput");
            if (input != null)
                input.GetMethod("SetScript").Invoke(null, new object[] { "", "" });   // Scripted = false: the physical input drives
            var diag = asm.GetType("PcPlatform.PcPadDiag");
            if (diag != null && mode == "paddiag")
            {
                var file = Path.Combine(Work(), "pad_diag.txt");
                diag.GetMethod("Begin").Invoke(null, new object[] { file });
                Debug.Log("[BdspPlay] controller diagnostic -> " + file);
            }
        }
    }

    // the Game view: maximized on Play, 16:9 (the game's aspect), focused so the keyboard reaches it. Internal editor API, by
    // reflection; any failure leaves the view as it is.
    static void PrepareGameView()
    {
        const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;
        try
        {
            var asm = typeof(Editor).Assembly;
            var gameViewType = asm.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType);
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null, null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { sizesType.GetProperty("currentGroupType").GetValue(instance, null) });
            int index = Array.FindIndex((string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group, null), t => t.StartsWith("16:9"));
            if (index >= 0)
                gameViewType.GetProperty("selectedSizeIndex", any).SetValue(gameView, index, null);
            var maximize = gameViewType.GetProperty("maximizeOnPlay", any);
            if (maximize != null)
                maximize.SetValue(gameView, true, null);
            gameView.Focus();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BdspPlay] could not prepare the Game view: " + e.Message);
        }
    }

    static void Tick()
    {
        TickInteractive();
        var phase = SessionState.GetString(Phase, "");
        if (phase == "") return;
        if (phase == "enter" && EditorApplication.isPlaying)
        {
            SessionState.SetFloat(T0, (float)Now());
            CacheLogState();
            SessionState.SetString(Phase, "play");
            nextSample = Now(); nextShot = Now() + 10;
            // s34 (Phase 6, PM4): ../work/<session>/play_capfps.txt = N -> Time.captureFramerate = N (the game's 30): every frame then
            // advances game time by exactly 1/N s whatever the editor's real speed, so a script lands on the same frames run after run.
            var cf = Path.Combine(Work(), "play_capfps.txt");
            int capfps;
            if (File.Exists(cf) && int.TryParse(File.ReadAllText(cf).Trim(), out capfps) && capfps > 0)
            {
                Time.captureFramerate = capfps;
                Debug.Log("[BdspPlay] Time.captureFramerate = " + capfps);
            }
            PinClock();
            LoadInputScript();
            Sample();
        }
        else if (phase == "play" && EditorApplication.isPlaying)
        {
            float secs = 120f;
            var f = Path.Combine(Work(), "play_seconds.txt");
            if (File.Exists(f)) float.TryParse(File.ReadAllText(f).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out secs);
            if (Now() >= nextSample) { nextSample = Now() + 5; Sample(); }
            if (Now() >= nextShot && shots < 60) { nextShot = Now() + 10; ScreenCapture.CaptureScreenshot(Base() + "_shot" + (shots++).ToString("00") + ".png"); }
            if (Now() - SessionState.GetFloat(T0, (float)Now()) >= secs)
            {
                Sample();
                SessionState.SetString(Phase, "exit");
                EditorApplication.ExitPlaymode();
            }
        }
        else if (phase == "exit" && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.SetString(Phase, "");
            Application.logMessageReceivedThreaded -= OnLog;
            Summarize();
            EditorApplication.Exit(0);
        }
    }

    // s34 (Phase 6, PM6): ../work/<session>/play_resolution.txt = "WxH" selects a fixed-resolution Game view size (added once as
    // "BDSP WxH"), so the game renders - and ScreenCapture captures - at exactly the reference emulator's output size. BDSP presents
    // 1280x720 docked on Ryubing at scale 1 (work/s34/ryu1). Internal editor API, reached by reflection (2019.4 GameViewSizes).
    static void SetGameViewResolution()
    {
        var f = Path.Combine(Work(), "play_resolution.txt");
        if (!File.Exists(f)) return;
        var parts = File.ReadAllText(f).Trim().ToLowerInvariant().Split('x');
        int w, h;
        if (parts.Length != 2 || !int.TryParse(parts[0], out w) || !int.TryParse(parts[1], out h))
        {
            Debug.LogWarning("[BdspPlay] play_resolution.txt: expected WxH");
            return;
        }
        try
        {
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null, null);
            var groupType = sizesType.GetProperty("currentGroupType").GetValue(instance, null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { groupType });
            var getTexts = group.GetType().GetMethod("GetDisplayTexts");
            string label = "BDSP " + w + "x" + h;
            int index = Array.FindIndex((string[])getTexts.Invoke(group, null), s => s.Contains(label));
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var kindType = asm.GetType("UnityEditor.GameViewSizeType");
                var size = sizeType.GetConstructor(new[] { kindType, typeof(int), typeof(int), typeof(string) })
                    .Invoke(new object[] { Enum.Parse(kindType, "FixedResolution"), w, h, label });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = Array.FindIndex((string[])getTexts.Invoke(group, null), s => s.Contains(label));
            }
            var gameViewType = asm.GetType("UnityEditor.GameView");
            var gameView = EditorWindow.GetWindow(gameViewType);
            gameViewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gameView, index, null);
            Debug.Log("[BdspPlay] Game view size " + label + " (index " + index + ")");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BdspPlay] could not set the Game view size: " + e);
        }
    }

    // s34 (Phase 6, PM4): ../work/<session>/play_clock.txt = "HH:MM[:SS]" pins the field's time of day. GameManager.elapsedTimeOfDay =
    // (timeScale * (nowTime.Ticks % day) + tickOffset) % day (shipped @02cd0a50) drives the lighting period / blend
    // (currentPeriodOfDayEx), EnvironmentController's minute clock and the wall clocks (FieldEventClockEntity); nowTime is
    // DateTime.Now, so two runs minutes apart - or across 04:00 / 10:00 / 17:00 / 20:00 - render differently. timeScale = 0 +
    // tickOffset = T freezes it at T (the shipped statics the debug Viewer also drives). Tooling only: no game code changes.
    static void PinClock()
    {
        var f = Path.Combine(Work(), "play_clock.txt");
        if (!File.Exists(f)) return;
        TimeSpan t;
        if (!TimeSpan.TryParse(File.ReadAllText(f).Trim(), System.Globalization.CultureInfo.InvariantCulture, out t))
        {
            Debug.LogWarning("[BdspPlay] play_clock.txt: not a time");
            return;
        }
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var gm = asm.GetType("GameManager");
            if (gm == null || gm.GetProperty("tickOffset") == null) continue;
            gm.GetProperty("timeScale").SetValue(null, 0L);
            gm.GetProperty("tickOffset").SetValue(null, t.Ticks);
            Debug.Log("[BdspPlay] time of day pinned at " + t + " (GameManager.timeScale 0, tickOffset " + t.Ticks + ")");
            return;
        }
        Debug.LogWarning("[BdspPlay] GameManager not found - clock not pinned");
    }

    // s33: ../work/<session>/play_input.txt -> PcPlatform.PcInput.SetScript (Assembly-CSharp, reached by reflection: this asmdef
    // references nothing). Loaded after Play mode starts (the domain reload on entering Play mode clears statics set before it).
    static void LoadInputScript()
    {
        var f = Path.Combine(Work(), "play_input.txt");
        if (!File.Exists(f)) return;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("PcPlatform.PcInput");
            if (t == null) continue;
            t.GetMethod("SetScript").Invoke(null, new object[] { File.ReadAllText(f), Work() });
            Debug.Log("[BdspPlay] input script loaded: " + f);
            return;
        }
        Debug.LogWarning("[BdspPlay] PcPlatform.PcInput not found - input script not loaded");
    }

    static void Sample()
    {
        var sb = new StringBuilder("SAMPLE frame " + Time.frameCount + " scenes:");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            sb.Append(string.Format(" [{0}{1} loaded={2} roots={3}]", s.path, s == SceneManager.GetActiveScene() ? " ACTIVE" : "", s.isLoaded, s.isLoaded ? s.rootCount : 0));
        }
        Debug.Log("[BdspPlay] " + sb);
    }

    static void Summarize()
    {
        string[] lines;
        lock (gate) lines = File.ReadAllLines(Base() + "_log.txt");
        var entries = new List<KeyValuePair<string, string>>(); // (type+message, first stack line block)
        int errors = 0, exceptions = 0, warnings = 0, logs = 0;
        var firstStack = new Dictionary<string, string>(); var counts = new Dictionary<string, int>(); var order = new List<string>();
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            if (!l.StartsWith("[")) continue;
            int c = l.IndexOf("] "); if (c < 0) continue;
            var rest = l.Substring(c + 2);
            if (rest.StartsWith("Error: ")) errors++; else if (rest.StartsWith("Exception: ")) exceptions++; else if (rest.StartsWith("Warning: ")) warnings++; else logs++;
            if (rest.StartsWith("Error: ") || rest.StartsWith("Exception: ") || rest.StartsWith("Assert: "))
            {
                var key = rest.Length > 300 ? rest.Substring(0, 300) : rest;
                if (!counts.ContainsKey(key)) { counts[key] = 0; order.Add(key); var st = new StringBuilder(); for (int j = i + 1; j < lines.Length && j < i + 13 && lines[j].StartsWith("    "); j++) st.AppendLine(lines[j]); firstStack[key] = st.ToString(); }
                counts[key]++;
            }
        }
        var sb = new StringBuilder();
        sb.AppendLine("BDSP play summary " + SessionState.GetString(Stamp, "") + " unity " + Application.unityVersion);
        sb.AppendLine(string.Format("messages: log {0}, warning {1}, error {2}, exception {3}; unique errors/exceptions {4}", logs, warnings, errors, exceptions, order.Count));
        sb.AppendLine("\nscene samples:");
        foreach (var l in lines.Where(x => x.Contains("[BdspPlay] SAMPLE"))) sb.AppendLine("  " + l.Substring(l.IndexOf("SAMPLE")));
        sb.AppendLine("\nnot-found lines (AssetManager / proxy / scenes):");
        foreach (var l in lines.Where(x => x.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 || x.Contains("[UnityEditorProxy]") || x.Contains("Don't match")).Take(200)) sb.AppendLine("  " + l);
        sb.AppendLine("\nunique errors / exceptions (count, first stack):");
        foreach (var k in order.Take(80)) { sb.AppendLine(string.Format("  x{0} {1}", counts[k], k)); sb.Append(firstStack[k]); }
        sb.AppendLine("BDSP_PLAY_DONE");
        File.WriteAllText(Base() + ".txt", sb.ToString());
        Debug.Log("BDSP_PLAY_DONE");
    }
}
