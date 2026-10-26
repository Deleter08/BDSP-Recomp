// s44 (2026-10-14): editor-only render of the contest score gauge (Dance.prefab UIDanceInfo/UIScoreGauge) - PC tooling, not game code.
// Author report: the pink gauge fill drew as a solid block instead of a thin bar inside the frame. Cause: AssetRipper exported the
// page-backed copies (<name>_N) of atlas-packed sprites cropped (cnt_fever_gauge_01a_0: 287x24.4 instead of the shipped 287x80 with
// a 38.3 px trim offset); tools/sprite_restore_shipped.py pass 2 restores them. This probe instantiates the shipped gauge on a
// 1280x720 camera canvas, applies what ScoreGauge.Initialize / SetUp / SetGaugeRatio / SetColorRatio(0) do (shader, frame widths,
// fill amounts, base colour) for a few ratios, renders each to ../work/s44/gauge_<case>.png and logs the gauge sprites' geometry.
//   BdspContestGaugeProbe.Run (-executeMethod, marker BDSP_GAUGE_PROBE_DONE) -> ../work/s44/gauge_probe.txt, then quits.
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BdspContestGaugeProbe
{
    static readonly StringBuilder sb = new StringBuilder();
    static string Work() { var d = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "work", "s44")); Directory.CreateDirectory(d); return d; }
    static void Log(string s) { sb.AppendLine(s); UnityEngine.Debug.Log("[BdspContestGaugeProbe] " + s); }

    static Transform Find(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = Find(c, name); if (r != null) return r; }
        return null;
    }

    [MenuItem("BDSP/Probe/Contest score gauge")]
    public static void Run()
    {
        try { Probe(); }
        catch (Exception e) { Log("EXCEPTION " + e); }
        Log("BDSP_GAUGE_PROBE_DONE");
        File.WriteAllText(Path.Combine(Work(), "gauge_probe.txt"), sb.ToString());
        if (Application.isBatchMode || Environment.GetCommandLineArgs().Length > 0 && Array.IndexOf(Environment.GetCommandLineArgs(), "-executeMethod") >= 0)
            EditorApplication.Exit(0);
    }

    static void Probe()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/sceneobj/Dance.prefab");
        var src = prefab.GetComponentInChildren<Dpr.Contest.ScoreGauge>(true);
        Log("unity " + Application.unityVersion + ", gauge " + src.name);
        var so = new SerializedObject(src);
        var baseColor = so.FindProperty("baseColor").colorValue;
        var gaugeMat = so.FindProperty("gaugeMat").objectReferenceValue as Material;
        var shader = Shader.Find("Custom/Contest/ColorAdditve");
        Log("baseColor " + baseColor + ", gaugeMat " + (gaugeMat ? gaugeMat.name : "null") + ", shader " + (shader ? shader.name + " supported=" + shader.isSupported : "NOT FOUND"));

        var camGo = new GameObject("BdspGaugeCam");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.16f, 1f);
        cam.orthographic = true;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        var canvasGo = new GameObject("BdspGaugeCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 10;

        var gauge = UnityEngine.Object.Instantiate(src.gameObject, canvasGo.transform, false);
        gauge.SetActive(true);
        var grt = gauge.GetComponent<RectTransform>();
        grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0.5f);
        grt.anchoredPosition = Vector2.zero;
        grt.localScale = Vector3.one;

        // ScoreGauge.Initialize: gaugeMat.shader = FindShader("Custom/Contest/ColorAdditve") - on a copy, the asset stays untouched
        var mat = new Material(gaugeMat) { shader = shader };
        foreach (var img in gauge.GetComponentsInChildren<Image>(true))
        {
            if (img.material == gaugeMat) img.material = mat;
            var sp = img.sprite;
            if (sp == null) { Log(string.Format("  image {0}: no sprite", img.name)); continue; }
            var pad = UnityEngine.Sprites.DataUtility.GetPadding(sp);
            Log(string.Format("  image {0}: type {1} sprite {2} rect {3} textureRect {4} textureRectOffset {5} padding {6} border {7} packed {8} tex {9} rectTransform {10}",
                img.name, img.type, sp.name, sp.rect, sp.textureRect, sp.textureRectOffset, pad, sp.border, sp.packed, sp.texture ? sp.texture.name : "null",
                img.rectTransform.rect));
        }

        var t = gauge.transform;
        var content = Find(t, "Content").GetComponent<RectTransform>();
        var leftFrame = Find(t, "LeftGaugeFrame").GetComponent<RectTransform>();
        var rightFrame = Find(t, "RightGaugeFrame").GetComponent<RectTransform>();
        var left = Find(t, "LeftScoreGauge").GetComponent<Image>();
        var leftBg = Find(t, "LeftGaugeBG").GetComponent<Image>();
        var right = Find(t, "RightScoreGauge").GetComponent<Image>();
        var rightBg = Find(t, "RightGaugeBG").GetComponent<Image>();
        float totalWidth = content.sizeDelta.x;

        var cases = new[] { new[] { 0.64f, 0.35f }, new[] { 0.36f, 0.20f }, new[] { 0.64f, 0.80f }, new[] { 0.64f, 0.0f } };
        foreach (var c in cases)
        {
            float success = c[0], ratio = c[1];
            // SetUp: frame widths from the success ratio
            var ls = leftFrame.sizeDelta; ls.x = totalWidth * success; leftFrame.sizeDelta = ls;
            var rs = rightFrame.sizeDelta; rs.x = (1f - success) * totalWidth; rightFrame.sizeDelta = rs;
            var rp = rightFrame.anchoredPosition; rp.x = leftFrame.sizeDelta.x; rightFrame.anchoredPosition = rp;
            // SetGaugeRatio + the BG fills of SetScoreRatio (goal == current)
            float l = ratio < success ? ratio / success : 1f, r = success <= ratio ? (ratio - success) / (1f - success) : 0f;
            left.fillAmount = leftBg.fillAmount = l;
            right.fillAmount = rightBg.fillAmount = r;
            // SetColorRatio(0): gaugeColor = baseColor, alpha 1
            var gc = baseColor; gc.a = 1f; left.color = gc; right.color = gc;

            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            string name = string.Format("gauge_s{0:00}_r{1:00}", success * 100, ratio * 100);
            File.WriteAllBytes(Path.Combine(Work(), name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            var wc = new Vector3[4];
            left.rectTransform.GetWorldCorners(wc);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, wc[0]), b = RectTransformUtility.WorldToScreenPoint(cam, wc[2]);
            Log(string.Format("{0}: success {1} ratio {2} -> left fill {3:F3} right fill {4:F3}; LeftScoreGauge rect px x {5:F1}..{6:F1} y(top-down) {7:F1}..{8:F1}",
                name, success, ratio, l, r, a.x, b.x, 720 - b.y, 720 - a.y));
        }
        cam.targetTexture = null;
        rt.Release();
    }
}
