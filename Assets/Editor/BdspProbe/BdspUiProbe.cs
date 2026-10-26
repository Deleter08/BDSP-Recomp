// BDSP play-test campaign (s33): editor-only UI probe - not game code. Called from a PcInput script line `probe <GameObject name>`
// (PcPlatform.PcInput finds this method by reflection). For every ACTIVE GameObject with that name it writes, for the object and each
// ancestor up to its Canvas: RectTransform (anchors, pivot, anchoredPosition, sizeDelta, rect, world corners), layout components
// (LayoutGroup padding / childAlignment / control / expand, ContentSizeFitter, LayoutElement) and, for TextMeshPro, the font asset with
// its FaceInfo, alignment, margins, preferred size, text bounds and every line's ascender / baseline / descender.
// Output: <folder>/probe_<name>_<n>.txt (folder = the play driver's work folder).
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class BdspUiProbe
{
    static int count;
    static int fpsFrame;
    static float fpsTime;

    public static void Dump(string name, string folder)
    {
        if (name == "audio")
        {
            DumpAudio(folder);
            return;
        }
        if (name.StartsWith("ui:"))
        {
            DumpUiState(name.Substring(3), folder);
            return;
        }
        if (name.StartsWith("renderers:"))
        {
            // s34 (D-011): every active Renderer whose hierarchy path contains the substring ("*" = all): materials, shaders,
            // keywords, each texture property (asset path, type, format, size, mips), lightmap index, shadow casting / receiving.
            string f = name.Substring(10).ToLowerInvariant();
            var sbr = new StringBuilder();
            int nr = 0;
            foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (r.gameObject.scene.name == null || !r.gameObject.activeInHierarchy || !r.enabled) continue;
                string path = PathOf(r.transform);
                if (f != "*" && path.ToLowerInvariant().IndexOf(f, StringComparison.Ordinal) < 0) continue;
                nr++;
                sbr.AppendLine(string.Format("{0} [{1}] lightmap {2} cast {3} receive {4}", path, r.GetType().Name, r.lightmapIndex, r.shadowCastingMode, r.receiveShadows));
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { sbr.AppendLine("    material null"); continue; }
                    sbr.AppendLine(string.Format("    material {0} shader {1} keywords [{2}]", m.name, m.shader != null ? m.shader.name : "null", string.Join(" ", m.shaderKeywords)));
                    foreach (var prop in m.GetTexturePropertyNames())
                    {
                        var t = m.GetTexture(prop);
                        if (t == null) continue;
                        var t2 = t as Texture2D;
                        sbr.AppendLine(string.Format("      {0} = {1} [{2} {3} {4}x{5} mips {6}]", prop, UnityEditor.AssetDatabase.GetAssetPath(t), t.GetType().Name,
                            t2 != null ? t2.format.ToString() : "-", t.width, t.height, t2 != null ? t2.mipmapCount : 0));
                    }
                }
            }
            sbr.AppendLine("renderers " + nr);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, string.Format("probe_renderers_{0:00}.txt", count++)), sbr.ToString());
            UnityEngine.Debug.Log("[BdspUiProbe] renderers: " + nr);
            return;
        }
        if (name == "open:zukan")
        {
            // s36: open the Pokédex exactly as XMenuTop does (@ ItemType.Zukan), for saves made before the Pokédex is obtained.
            var w = Dpr.UI.UIManager.Instance.CreateUIWindow<Dpr.UI.UIZukan>(UIWindowID.ZUKAN);
            w.Open(Dpr.UI.UIWindow.WINDOWID_FIELD);
            UnityEngine.Debug.Log("[BdspUiProbe] open:zukan");
            return;
        }
        if (name == "open:setting")
        {
            // s39: open Options exactly as XMenuTop does (ItemType.Setting) - test of the PC Display Mode / Resolution rows.
            var w = Dpr.UI.UIManager.Instance.CreateUIWindow<Dpr.UI.SettingWindow>(UIWindowID.SETTING);
            w.Open(Dpr.UI.UIWindow.WINDOWID_FIELD);
            UnityEngine.Debug.Log("[BdspUiProbe] open:setting");
            return;
        }
        if (name == "open:card")
        {
            // s40: open the trainer card exactly as XMenuTop does (ItemType.Card) - test of PcRenderScale.
            var w = Dpr.UI.UIManager.Instance.CreateUIWindow<Dpr.UI.UICard>(UIWindowID.CARD);
            w.Open(Dpr.UI.UIWindow.WINDOWID_FIELD);
            UnityEngine.Debug.Log("[BdspUiProbe] open:card");
            return;
        }
        if (name == "open:box")
        {
            // s40: open the Pokémon boxes exactly as the PC script command does (EvDataManager @ CreateUIWindow<BoxWindow>.Open(FIELD, false)).
            Dpr.UI.UIManager.Instance.CreateUIWindow<Dpr.UI.BoxWindow>(UIWindowID.BOX).Open(Dpr.UI.UIWindow.WINDOWID_FIELD, false);
            UnityEngine.Debug.Log("[BdspUiProbe] open:box");
            return;
        }
        if (name == "open:bag")
        {
            // s40: open the Bag exactly as XMenuTop does (ItemType.Bag).
            var w = Dpr.UI.UIManager.Instance.CreateUIWindow<Dpr.UI.UIBag>(UIWindowID.BAG);
            w.Open(pokemonParam: null, prevWindowId: Dpr.UI.UIWindow.WINDOWID_FIELD);
            UnityEngine.Debug.Log("[BdspUiProbe] open:bag");
            return;
        }
        if (name == "open:townmap")
        {
            // s40: open the Town Map exactly as XMenuTop does (ItemType.Map) - also on saves without the map item.
            var w = Dpr.UI.UIManager.Instance.CreateUIWindow<Dpr.UI.TownmapWindow>(UIWindowID.MAP);
            w.Open(new Dpr.UI.TownmapWindow.Param() { type = Dpr.UI.Townmap.TownmapType.Default }, Dpr.UI.UIWindow.WINDOWID_FIELD);
            UnityEngine.Debug.Log("[BdspUiProbe] open:townmap");
            return;
        }
        if (name == "townmap")
        {
            // s40: every active Townmap: cursor world position, cell root, the cursor's cell, PcUnit, the cursor offset from the cell
            // root in UI units (/ PcUnit) - the same numbers at every resolution when right.
            var sbt = new StringBuilder();
            sbt.AppendLine(string.Format("probe townmap frame {0} screen {1}x{2}", Time.frameCount, Screen.width, Screen.height));
            foreach (var tm in Resources.FindObjectsOfTypeAll<Dpr.UI.Townmap>())
            {
                if (tm.gameObject.scene.name == null || !tm.isActiveAndEnabled) continue;
                float u = tm.PcUnit;
                var root = tm.cellRoot.position;
                var d = tm.cursorPos - root;
                sbt.AppendLine(string.Format("  {0} unit {1:F3} cursor {2} cellRoot {3} cell {4} | cursor-root units {5}", PathOf(tm.transform), u,
                    tm.cursorPos.ToString("F1"), root.ToString("F1"), tm.ToCellPos(tm.cursorPos), (d / u).ToString("F1")));
            }
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, string.Format("probe_townmap_{0:00}.txt", count++)), sbt.ToString());
            UnityEngine.Debug.Log("[BdspUiProbe] townmap");
            return;
        }
        if (name.StartsWith("uipos:"))
        {
            // s40: uipos:<a>[&<b>...] - every active RectTransform whose path contains all the substrings: world position and world size,
            // both also divided by PcUiRoot.UnitScale (= UI units; the same numbers at every resolution when the layout is right).
            var keys = name.Substring(6).ToLowerInvariant().Split('&');
            var sbu = new StringBuilder();
            sbu.AppendLine(string.Format("probe {0} frame {1} screen {2}x{3}", name, Time.frameCount, Screen.width, Screen.height));
            int nu = 0;
            foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (rt.gameObject.scene.name == null || !rt.gameObject.activeInHierarchy) continue;
                string path = PathOf(rt);
                string low = path.ToLowerInvariant();
                if (!keys.All(k => low.IndexOf(k, StringComparison.Ordinal) >= 0)) continue;
                if (++nu > 60) break;
                float unit = PcPlatform.PcUiRoot.UnitScale(rt);
                var size = Vector2.Scale(rt.rect.size, (Vector2)rt.lossyScale);
                sbu.AppendLine(string.Format("  {0} unit {1:F3} pos {2} size {3} | units pos {4} size {5}", path, unit, rt.position.ToString("F1"),
                    size.ToString("F1"), (rt.position / unit).ToString("F1"), (size / unit).ToString("F1")));
            }
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, string.Format("probe_uipos_{0:00}.txt", count++)), sbu.ToString());
            UnityEngine.Debug.Log("[BdspUiProbe] " + name + " " + nu);
            return;
        }
        if (name == "boxicons")
        {
            // s40: every active box / party item with a Pokémon: item world position, its icon root's world + local position, the
            // icon offset from the item in world pixels and in UI units (/ PcUiRoot.UnitScale) - equal at every resolution when right.
            var sbb = new StringBuilder();
            sbb.AppendLine(string.Format("probe boxicons frame {0} screen {1}x{2}", Time.frameCount, Screen.width, Screen.height));
            foreach (var item in Resources.FindObjectsOfTypeAll<Dpr.UI.BoxItem>())
            {
                if (item.gameObject.scene.name == null || !item.isActiveAndEnabled) continue;
                var icon = item.GetSwapRoot();
                if (icon == null || !icon.gameObject.activeInHierarchy) continue;
                float unit = PcPlatform.PcUiRoot.UnitScale(item);
                Vector3 d = icon.transform.position - item.transform.position;
                sbb.AppendLine(string.Format("  {0} unit {1} item {2} icon {3} local {4} offset px {5} units {6}", PathOf(item.transform), unit,
                    item.transform.position.ToString("F1"), icon.transform.position.ToString("F1"), icon.transform.localPosition.ToString("F1"),
                    d.ToString("F1"), (d / unit).ToString("F2")));
            }
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, string.Format("probe_boxicons_{0:00}.txt", count++)), sbb.ToString());
            UnityEngine.Debug.Log("[BdspUiProbe] boxicons");
            return;
        }
        if (name == "rtsizes")
        {
            // s40: PcRenderScale state - every active camera with a target texture (size, format, MSAA) and every camera-space canvas
            // (camera, scaler mode, scale factors, pixel rect).
            var sbt = new StringBuilder();
            sbt.AppendLine(string.Format("probe rtsizes frame {0} screen {1}x{2} PcRenderScale.Factor {3}", Time.frameCount, Screen.width, Screen.height,
                PcPlatform.PcRenderScale.Factor));
            foreach (var cam in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (cam.gameObject.scene.name == null || cam.targetTexture == null) continue;
                var rt = cam.targetTexture;
                sbt.AppendLine(string.Format("  camera {0} active {1} -> {2} {3}x{4} {5} msaa {6}", PathOf(cam.transform), cam.isActiveAndEnabled, rt.name,
                    rt.width, rt.height, rt.format, rt.antiAliasing));
            }
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (c.gameObject.scene.name == null || c.renderMode != RenderMode.ScreenSpaceCamera) continue;
                var s = c.GetComponent<CanvasScaler>();
                sbt.AppendLine(string.Format("  canvas {0} active {1} camera {2} scaler {3} scalerFactor {4} canvas.scaleFactor {5} pixelRect {6}", PathOf(c.transform),
                    c.isActiveAndEnabled, c.worldCamera != null ? c.worldCamera.name : "null", s != null ? s.uiScaleMode.ToString() : "none",
                    s != null ? s.scaleFactor : 0f, c.scaleFactor, c.pixelRect));
            }
            foreach (var ri in Resources.FindObjectsOfTypeAll<RawImage>())
            {
                if (ri.gameObject.scene.name == null || !ri.isActiveAndEnabled || !(ri.texture is RenderTexture)) continue;
                sbt.AppendLine(string.Format("  rawimage {0} rect {1}x{2} texture {3}x{4}", PathOf(ri.transform), ri.rectTransform.rect.width, ri.rectTransform.rect.height,
                    ri.texture.width, ri.texture.height));
            }
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, string.Format("probe_rtsizes_{0:00}.txt", count++)), sbt.ToString());
            UnityEngine.Debug.Log("[BdspUiProbe] rtsizes");
            return;
        }
        if (name.StartsWith("setfps:"))
        {
            // s37: setfps:<targetFrameRate>:<vSyncCount> - change the frame pacing mid-run.
            var a = name.Substring(7).Split(':');
            Application.targetFrameRate = int.Parse(a[0]);
            QualitySettings.vSyncCount = int.Parse(a[1]);
            UnityEngine.Debug.Log(string.Format("[BdspUiProbe] setfps target {0} vsync {1}", Application.targetFrameRate, QualitySettings.vSyncCount));
            return;
        }
        if (name == "fps")
        {
            // s37: frame pacing state + the measured rate since the previous `probe fps`.
            float now = Time.realtimeSinceStartup;
            int frames = Time.frameCount - fpsFrame;
            string line = string.Format("t {0:F2} frames {1} in {2:F2}s = {3:F1} fps | targetFrameRate {4} vSyncCount {5} captureFramerate {6} refresh {7} timeScale {8}",
                now, frames, now - fpsTime, frames / Mathf.Max(now - fpsTime, 0.0001f), Application.targetFrameRate, QualitySettings.vSyncCount,
                Time.captureFramerate, Screen.currentResolution.refreshRate, Time.timeScale);
            fpsFrame = Time.frameCount;
            fpsTime = now;
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "probe_fps.txt"), line + "\n");
            return;
        }
        if (name == "zukanpos")
        {
            // s36: the description panel's captured hide positions vs the live world positions they were read from.
            var sbz = new StringBuilder();
            sbz.AppendLine(string.Format("probe zukanpos frame {0} t {1:F2} screen {2}x{3}", Time.frameCount, Time.realtimeSinceStartup, Screen.width, Screen.height));
            const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            foreach (var p in Resources.FindObjectsOfTypeAll<Dpr.UI.ZukanDescriptionPanel>())
            {
                if (p.gameObject.scene.name == null) continue;
                var mv = (RectTransform)typeof(Dpr.UI.ZukanDescriptionPanel).GetField("modelViewRectTransform", bf).GetValue(p);
                sbz.AppendLine(string.Format("  {0}: hideModelViewPosition {1} hideInfoButtonPosition {2}", PathOf(p.transform),
                    typeof(Dpr.UI.ZukanDescriptionPanel).GetField("hideModelViewPosition", bf).GetValue(p),
                    typeof(Dpr.UI.ZukanDescriptionPanel).GetField("hideInfoButtonPosition", bf).GetValue(p)));
                sbz.AppendLine(string.Format("  modelView {0} world {1} local {2} anchored {3}", mv != null ? PathOf(mv) : "null", mv != null ? mv.position.ToString("F2") : "-",
                    mv != null ? mv.localPosition.ToString("F2") : "-", mv != null ? mv.anchoredPosition.ToString("F2") : "-"));
            }
            foreach (var t in Resources.FindObjectsOfTypeAll<RectTransform>())
            {
                if (t.gameObject.scene.name == null) continue;
                if (t.name == "ModelViewPosition" || t.name == "InfoButtonPosition" || t.name == "Zukan" || t.name == "Window")
                    sbz.AppendLine(string.Format("  {0} active {1} world {2} lossyScale {3} rect {4}", PathOf(t), t.gameObject.activeInHierarchy, t.position.ToString("F2"),
                        t.lossyScale.ToString("F4"), t.rect));
            }
            foreach (var c in Resources.FindObjectsOfTypeAll<Canvas>())
                if (c.gameObject.scene.name != null && c.isRootCanvas && c.name == "Zukan")
                    sbz.AppendLine(string.Format("  root canvas {0} mode {1} scaleFactor {2}", PathOf(c.transform), c.renderMode, c.scaleFactor));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, string.Format("probe_zukanpos_{0:00}.txt", count++)), sbz.ToString());
            return;
        }
        if (name.StartsWith("rdcap:"))
        {
            RdCapture(name.Substring(6), folder);
            return;
        }
        if (name == "camera")
        {
            // s34 (D-006): which HDR format the cameras render into - tier settings are per build target (the shipped ones are Switch,
            // hdrMode 2 = R11G11B10); without Standalone entries the editor falls back to FP16.
            var sbc = new StringBuilder();
            var tier = Graphics.activeTier;
            var ts = UnityEditor.Rendering.EditorGraphicsSettings.GetTierSettings(UnityEditor.BuildTargetGroup.Standalone, tier);
            sbc.AppendLine(string.Format("activeTier {0}: Standalone hdr {1} hdrMode {2} renderingPath {3}", tier, ts.hdr, ts.hdrMode, ts.renderingPath));
            foreach (var cam in Camera.allCameras)
                sbc.AppendLine(string.Format("  camera {0} allowHDR {1} allowMSAA {2} target {3} depth {4} cmdbufs {5}", cam.name, cam.allowHDR, cam.allowMSAA,
                    cam.targetTexture != null ? cam.targetTexture.name + " " + cam.targetTexture.format : "screen", cam.depth, cam.commandBufferCount));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, string.Format("probe_camera_{0:00}.txt", count++)), sbc.ToString());
            UnityEngine.Debug.Log("[BdspUiProbe] camera: " + sbc.ToString().Split('\n')[0]);
            return;
        }
        var sb = new StringBuilder();
        sb.AppendLine(string.Format("probe '{0}' frame {1} t {2:F2} screen {3}x{4}", name, Time.frameCount, Time.realtimeSinceStartup, Screen.width, Screen.height));
        int found = 0;
        foreach (var t in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (t.name != name || !t.gameObject.activeInHierarchy || t.gameObject.scene.name == null)
                continue;
            found++;
            sb.AppendLine("== match " + found + ": " + PathOf(t));
            for (Transform p = t; p != null; p = p.parent)
            {
                DumpOne(sb, p as RectTransform);
                if (p.GetComponent<Canvas>() != null && p.GetComponent<Canvas>().isRootCanvas)
                    break;
            }
        }
        if (found == 0)
            sb.AppendLine("(no active object)");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, string.Format("probe_{0}_{1:00}.txt", name, count++)), sb.ToString());
        UnityEngine.Debug.Log("[BdspUiProbe] " + name + ": " + found + " active match(es)");
    }

    // `probe audio`: every Wwise game object's playing IDs and their source play positions (two probes some seconds apart show
    // whether the Switch-built banks really decode and play in the Windows Wwise runtime - D4 first-boot check).
    static void DumpAudio(string folder)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Format("probe audio frame {0} t {1:F2}", Time.frameCount, Time.realtimeSinceStartup));
        int total = 0;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene.name == null || !go.activeInHierarchy || go.GetComponent<AkGameObj>() == null && go.name != "AudioManager")
                continue;
            uint n = 32;
            var ids = new uint[32];
            var r = AkSoundEngine.GetPlayingIDsFromGameObject(go, ref n, ids);
            if (n == 0)
                continue;
            sb.Append(string.Format("  {0} ({1}): {2} playing:", go.name, r, n));
            for (int i = 0; i < n; i++)
            {
                int pos;
                var pr = AkSoundEngine.GetSourcePlayPosition(ids[i], out pos);
                sb.Append(string.Format(" [id {0} pos {1}ms {2}]", ids[i], pos, pr));
            }
            sb.AppendLine();
            total += (int)n;
        }
        sb.AppendLine("total playing " + total);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, string.Format("probe_audio_{0:00}.txt", count++)), sb.ToString());
        UnityEngine.Debug.Log("[BdspUiProbe] audio: " + total + " playing");
    }

    // `probe ui:<path substring>`: every ACTIVE GameObject whose hierarchy path contains the substring (case-insensitive) - its
    // CanvasGroup alpha, Image (enabled, sprite, colour alpha), TextMeshPro text (enabled, colour alpha, the text itself) and Animator
    // (controller, current state + the clip it plays, normalizedTime, layer weight). Shows whether a UI piece is absent, hidden, or
    // present but not animated / not filled.
    static void DumpUiState(string filter, string folder)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Format("probe ui:{0} frame {1} t {2:F2}", filter, Time.frameCount, Time.realtimeSinceStartup));
        string f = filter.ToLowerInvariant();
        int n = 0;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene.name == null || !go.activeInHierarchy)
                continue;
            string path = PathOf(go.transform);
            if (path.ToLowerInvariant().IndexOf(f, StringComparison.Ordinal) < 0)
                continue;
            var parts = new System.Collections.Generic.List<string>();
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
                parts.Add(string.Format("CanvasGroup a={0:F2}", cg.alpha));
            var img = go.GetComponent<Image>();
            if (img != null)
            {
                parts.Add(string.Format("Image{0} sprite={1} a={2:F2} type={3} fill={4} ppuMul={5:F2}", img.enabled ? "" : "(off)", img.sprite != null ? img.sprite.name : "null", img.color.a, img.type, img.fillCenter, img.pixelsPerUnitMultiplier));
                var rt = (RectTransform)go.transform;
                parts.Add(string.Format("rect={0}x{1:F1} canvasScale={2:F3}", rt.rect.width.ToString("F1"), rt.rect.height, img.canvas != null ? img.canvas.scaleFactor : -1f));
                if (img.sprite != null)
                {
                    var sp = img.sprite;
                    parts.Add(string.Format("sprite rect={0} border={1} ppu={2} packed={3} texRect={4} outerUV={5} innerUV={6} pad={7}", sp.rect, sp.border, sp.pixelsPerUnit, sp.packed,
                        SafeTexRect(sp), UnityEngine.Sprites.DataUtility.GetOuterUV(sp), UnityEngine.Sprites.DataUtility.GetInnerUV(sp), UnityEngine.Sprites.DataUtility.GetPadding(sp)));
                }
                var cr = go.GetComponent<CanvasRenderer>();
                if (cr != null)
                    parts.Add(string.Format("CR cull={0} alpha={1:F2} inherited={2:F2} depth={3} mats={4} shader={5}", cr.cull, cr.GetAlpha(), cr.GetInheritedAlpha(), cr.absoluteDepth, cr.materialCount,
                        cr.materialCount > 0 && cr.GetMaterial(0) != null ? cr.GetMaterial(0).shader.name : "-"));
            }
            var tmp = go.GetComponent<TMP_Text>();
            if (tmp != null)
                parts.Add(string.Format("TMP{0} a={1:F2} text=\"{2}\"", tmp.enabled ? "" : "(off)", tmp.color.a, (tmp.text ?? "").Replace("\n", "\\n")));
            var an = go.GetComponent<Animator>();
            if (an != null)
            {
                string state = "-";
                if (an.isInitialized && an.runtimeAnimatorController != null)
                {
                    var st = an.GetCurrentAnimatorStateInfo(0);
                    var clips = an.GetCurrentAnimatorClipInfo(0);
                    state = string.Format("hash={0} t={1:F2} clip={2}", st.shortNameHash, st.normalizedTime, clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "none");
                }
                parts.Add(string.Format("Animator{0} ctrl={1} init={2} {3}", an.enabled ? "" : "(off)", an.runtimeAnimatorController != null ? an.runtimeAnimatorController.name : "null", an.isInitialized, state));
            }
            sb.AppendLine(path + (parts.Count > 0 ? "  | " + string.Join(" | ", parts.ToArray()) : ""));
            n++;
        }
        sb.AppendLine("objects " + n);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, string.Format("probe_ui_{0}_{1:00}.txt", filter.Replace('/', '_'), count++)), sb.ToString());
        UnityEngine.Debug.Log("[BdspUiProbe] ui:" + filter + ": " + n + " objects");
    }

    // s34 PM5: `probe rdcap:<label>` - a RenderDoc capture of one Game view frame (the editor must run with -load-renderdoc:
    // tools/import_watchdog.py --unity-arg=-load-renderdoc). The Game view's own RenderDoc path: Begin / RepaintImmediately / End;
    // the newest .rdc RenderDoc wrote is copied to <folder>/<label>.rdc. Internal editor API, reached by reflection.
    static void RdCapture(string label, string folder)
    {
        var asm = typeof(UnityEditor.Editor).Assembly;
        var rd = asm.GetType("UnityEditorInternal.RenderDoc");
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        if (rd == null || !(bool)rd.GetMethod("IsLoaded", flags).Invoke(null, null))
        {
            UnityEngine.Debug.LogWarning("[BdspUiProbe] rdcap: RenderDoc is not loaded (start the editor with -load-renderdoc)");
            return;
        }
        var gvType = asm.GetType("UnityEditor.GameView");
        var gv = UnityEditor.EditorWindow.GetWindow(gvType);
        var t0 = DateTime.Now.AddSeconds(-1);
        rd.GetMethod("BeginCaptureRenderDoc", flags).Invoke(null, new object[] { gv });
        typeof(UnityEditor.EditorWindow).GetMethod("RepaintImmediately", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).Invoke(gv, null);
        rd.GetMethod("EndCaptureRenderDoc", flags).Invoke(null, new object[] { gv });
        string newest = null;
        var roots = new[] { Path.Combine(Path.GetTempPath(), "RenderDoc"), Path.GetTempPath(), Path.GetFullPath("Temp") };
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var f in Directory.GetFiles(root, "*.rdc", SearchOption.TopDirectoryOnly))
                if (File.GetLastWriteTime(f) >= t0 && (newest == null || File.GetLastWriteTime(f) > File.GetLastWriteTime(newest)))
                    newest = f;
        }
        Directory.CreateDirectory(folder);
        if (newest != null)
        {
            File.Copy(newest, Path.Combine(folder, label + ".rdc"), true);
            UnityEngine.Debug.Log("[BdspUiProbe] rdcap " + label + " <- " + newest);
        }
        else
            UnityEngine.Debug.LogWarning("[BdspUiProbe] rdcap " + label + ": no new .rdc found in " + string.Join(", ", roots));
    }

    static string SafeTexRect(Sprite sp)
    {
        try { return sp.textureRect.ToString(); }
        catch (Exception) { return "n/a"; }
    }

    static string PathOf(Transform t)
    {
        string s = t.name;
        for (var p = t.parent; p != null; p = p.parent)
            s = p.name + "/" + s;
        return s;
    }

    static string V(Vector2 v) { return string.Format("({0:F2}, {1:F2})", v.x, v.y); }

    static void DumpOne(StringBuilder sb, RectTransform rt)
    {
        if (rt == null)
            return;
        var c = new Vector3[4];
        rt.GetWorldCorners(c);
        sb.AppendLine(string.Format("  [{0}] anchors {1}-{2} pivot {3} pos {4} size {5} rect {6} worldBL ({7:F1},{8:F1}) worldTR ({9:F1},{10:F1}) scale {11}",
            rt.name, V(rt.anchorMin), V(rt.anchorMax), V(rt.pivot), V(rt.anchoredPosition), V(rt.sizeDelta), rt.rect, c[0].x, c[0].y, c[2].x, c[2].y, rt.lossyScale));
        foreach (var comp in rt.GetComponents<Component>())
        {
            if (comp == null) { sb.AppendLine("     (missing script)"); continue; }
            var lg = comp as HorizontalOrVerticalLayoutGroup;
            if (lg != null)
                sb.AppendLine(string.Format("     {0} padding L{1} R{2} T{3} B{4} align {5} spacing {6} control W{7} H{8} expand W{9} H{10} enabled {11}",
                    lg.GetType().Name, lg.padding.left, lg.padding.right, lg.padding.top, lg.padding.bottom, lg.childAlignment, lg.spacing,
                    lg.childControlWidth, lg.childControlHeight, lg.childForceExpandWidth, lg.childForceExpandHeight, lg.enabled));
            var csf = comp as ContentSizeFitter;
            if (csf != null)
                sb.AppendLine(string.Format("     ContentSizeFitter h {0} v {1} enabled {2}", csf.horizontalFit, csf.verticalFit, csf.enabled));
            var le = comp as LayoutElement;
            if (le != null)
                sb.AppendLine(string.Format("     LayoutElement min {0}x{1} pref {2}x{3} flex {4}x{5} ignore {6}", le.minWidth, le.minHeight, le.preferredWidth, le.preferredHeight, le.flexibleWidth, le.flexibleHeight, le.ignoreLayout));
            var img = comp as Image;
            if (img != null)
                sb.AppendLine(string.Format("     Image sprite {0} type {1} color {2} enabled {3} ppuMul {4}", img.sprite != null ? img.sprite.name + " rect " + img.sprite.rect + " border " + img.sprite.border + " texRect " + (img.sprite.packed && img.sprite.packingMode == SpritePackingMode.Tight ? "(tight-packed)" : img.sprite.textureRect.ToString()) + " texRectOffset " + img.sprite.textureRectOffset + " pivot " + img.sprite.pivot + " padding " + UnityEngine.Sprites.DataUtility.GetPadding(img.sprite) : "null", img.type, img.color, img.enabled, img.pixelsPerUnitMultiplier));
            var tmp = comp as TMP_Text;
            if (tmp != null)
            {
                var f = tmp.font;
                sb.AppendLine(string.Format("     {0} text '{1}' font '{2}' material '{3}' size {4} align {5} margin {6} enableWrap {7} overflow {8} preferred {9}x{10} bounds {11}",
                    tmp.GetType().Name, tmp.text, f != null ? f.name : "null", tmp.fontSharedMaterial != null ? tmp.fontSharedMaterial.name + " / " + tmp.fontSharedMaterial.shader.name : "null",
                    tmp.fontSize, tmp.alignment, tmp.margin, tmp.textWrappingMode != TMPro.TextWrappingModes.NoWrap, tmp.overflowMode, tmp.preferredWidth, tmp.preferredHeight, tmp.textBounds));
                if (f != null)
                {
                    var fi = f.faceInfo;
                    sb.AppendLine(string.Format("     face '{0}' pointSize {1} scale {2} lineHeight {3} ascent {4} cap {5} mean {6} baseline {7} descent {8} under {9} atlas {10}x{11} padding {12} version '{13}'",
                        fi.familyName, fi.pointSize, fi.scale, fi.lineHeight, fi.ascentLine, fi.capLine, fi.meanLine, fi.baseline, fi.descentLine, fi.underlineOffset,
                        f.atlasWidth, f.atlasHeight, f.atlasPadding, f.version));
                }
                var ti = tmp.textInfo;
                if (ti != null)
                    for (int i = 0; i < ti.lineCount && i < 4; i++)
                    {
                        var li = ti.lineInfo[i];
                        sb.AppendLine(string.Format("     line {0}: ascender {1:F2} baseline {2:F2} descender {3:F2} lineHeight {4:F2} chars {5}..{6}",
                            i, li.ascender, li.baseline, li.descender, li.lineHeight, li.firstCharacterIndex, li.lastCharacterIndex));
                    }
            }
        }
    }
}
