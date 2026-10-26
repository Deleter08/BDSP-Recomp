#if !SWITCH
// PC platform layer: keep the UIManager window roots at the screen centre (s36, the Pokédex model view off by (320, 180)). NOT shipped code.
//
// UIManager.prefab's Main / Transition / BlurBg are plain RectTransforms (no Canvas) authored at world (640, 360) = the centre of the
// Switch's 1280x720 screen. Every UI window is its own Screen Space Overlay canvas: UIManager.CreateUIWindow parents it under
// Main/Actives with localPosition 0, so the window sits at (640, 360) until Unity's next canvas update re-centres it at
// (Screen.width / 2, Screen.height / 2). Windows that read world positions while they open (UIZukan.Initialize ->
// ZukanDescriptionPanel.SetHidePositions(ModelViewPosition.position, InfoButtonPosition.position), then DOMove to them) keep the stale
// value. On the Switch both centres are (640, 360), so the shipped game never shows it; at 1920x1080 the Pokédex model view landed
// (320, 180) px down-left (measured: work/s36/dex2/probe_zukanpos_*.txt). Pinning the roots to the real screen centre makes the
// opening frame's world positions equal to the ones Unity settles on, as on the Switch.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PcPlatform
{
    [DefaultExecutionOrder(-32000)]
    public class PcUiRoot : MonoBehaviour
    {
        static readonly string[] Roots = { "Main", "Transition", "BlurBg" };

        // Screen pixels per 1280x720 UI unit. Everywhere but Android / iOS (iPhone 15 Pro 2556x1179 = 19.5:9): Screen.width / 1280 (UIManager "Canvas", match width). Android screens
        // wider than 16:9 (Pixel 6 Pro 3120x1440 = 13:6): match width would make the canvas 590 units tall and crop the top and bottom of
        // the 720-unit layout, so fit the height instead - the 1280x720 area is centred, the extra width is side margin. Same value at 16:9.
        public static float UiScale
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
#else
                return Screen.width / 1280f;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            var go = new GameObject("PcUiRoot");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<PcUiRoot>();
            Canvas.willRenderCanvases += ScaleConstantPixelCanvases;
        }

        // s40 (the title logo, "Press the A Button" and the staff roll at 1/3 size at 3840x2160): a root canvas drawn to the SCREEN
        // with a Constant Pixel Size scaler keeps 1 UI unit = 1 pixel, i.e. it was laid out for the 1280x720 Switch (Title.unity
        // Canvas, Field.unity Canvas x2 + CaptureCanvas, SecretBaseController, UgFieldManager, DigFossilController, UNION_BattleRuleWindow,
        // RankingBoard ... - work/s40/cps_canvas_scan.txt). Scale them like UIManager's main canvas (ScaleWithScreenSize 1280x720, match
        // width): scaleFactor = shipped x Screen.width / 1280 (identity at 1280x720). Run before the canvases render, every frame, so a
        // canvas created this frame is scaled before its first frame and a resolution change applies at once. Camera canvases drawn into
        // a render texture are PcRenderScale's (same shipped-value record, ShippedScale).
        static readonly Dictionary<CanvasScaler, float> shippedScale = new Dictionary<CanvasScaler, float>();
        static int scaledFrame = -1;

        // the scaler's shipped scaleFactor, recorded the first time any PC code touches it
        public static float ShippedScale(CanvasScaler scaler)
        {
            float shipped;
            if (!shippedScale.TryGetValue(scaler, out shipped))
            {
                if (shippedScale.Count > 512)
                {
                    var dead = new List<CanvasScaler>();
                    foreach (var key in shippedScale.Keys)
                        if (key == null)
                            dead.Add(key);
                    foreach (var key in dead)
                        shippedScale.Remove(key);
                }
                shipped = scaler.scaleFactor;
                shippedScale[scaler] = shipped;
            }
            return shipped;
        }

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        static CanvasScaler[] scalerCache;
        static int scalerCacheFrame = -1000;
#endif
        static void ScaleConstantPixelCanvases()
        {
            if (Time.frameCount == scaledFrame)
                return;
            scaledFrame = Time.frameCount;

            float k = UiScale;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            // FindObjectsOfType every frame costs GC + CPU on the phone; rescan every 10 frames (a new canvas waits <= 1/6 s)
            if (scalerCache == null || Time.frameCount - scalerCacheFrame >= 10)
            {
                scalerCache = FindObjectsOfType<CanvasScaler>();
                scalerCacheFrame = Time.frameCount;
            }
            foreach (var scaler in scalerCache)
            {
                if (scaler == null)
                    continue;
#else
            foreach (var scaler in FindObjectsOfType<CanvasScaler>())
            {
#endif
#if UNITY_ANDROID || UNITY_IOS
                FitWideScreenScaler(scaler);
#endif
                if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
                    continue;
                var canvas = scaler.GetComponent<Canvas>();
                if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
                    continue;
                if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    var cam = canvas.worldCamera;
                    if (cam == null || cam.targetTexture != null)
                        continue;
                }
                float want = ShippedScale(scaler) * k;
                if (scaler.scaleFactor != want)
                {
                    scaler.scaleFactor = want;
                    canvas.scaleFactor = want;   // now, not at the scaler's next Update
                }
            }
        }

#if UNITY_ANDROID || UNITY_IOS
        // Scale With Screen Size canvases drawn to the screen, match width (0): on a screen wider than the reference, match height instead
        // (scale = Screen.height / reference.y) so the full reference height stays visible. Applied now, not at the scaler's next Update,
        // so a canvas created this frame never shows one frame at the cropping scale. Identity when the screen is not wider than the
        // reference (16:9 for the 1280x720 canvases). Canvases drawn into render textures are left alone (PcRenderScale sizes those).
        static void FitWideScreenScaler(CanvasScaler scaler)
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                return;
            var canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || !canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace)
                return;
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                var cam = canvas.worldCamera;
                if (cam == null || cam.targetTexture != null)
                    return;
            }
            var reference = scaler.referenceResolution;
            if (reference.x <= 0f || reference.y <= 0f || (float)Screen.width / Screen.height <= reference.x / reference.y)
                return;
            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
                return;
            if (scaler.matchWidthOrHeight != 1f)
                scaler.matchWidthOrHeight = 1f;
            float want = Screen.height / reference.y;   // log-lerp at match 1
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.scaleFactor != want)
                canvas.scaleFactor = want;
        }
#endif

        // Before every other script's Update (execution order -32000), so a window created this frame already sees the pinned roots.
        void Update()
        {
            // isInstantiated / GetGameObject, not Instance: the Instance getter enables the component and searches when it is missing.
            if (!Dpr.UI.UIManager.isInstantiated)
                return;
            var centre = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            var t = Dpr.UI.UIManager.GetGameObject().transform;
            foreach (var name in Roots)
            {
                var root = t.Find(name);
                if (root == null)
                    continue;
                var p = root.position;
                if (p.x != centre.x || p.y != centre.y)
                    root.position = new Vector3(centre.x, centre.y, p.z);
            }
        }

        // s40 (author: the box screen's party icon sat on the "Lv." text at 3840x2160): world pixels per UI unit for `c`'s canvas.
        // On the Switch the UI is 1280x720 = 1 UI unit per world pixel, and shipped code adds UI-unit quantities (offset constants,
        // localPosition, rect sizes) straight to Screen Space Overlay WORLD positions (BoxItem.Select @01aaeb10:
        // transform.position.x + _root.localPosition.x). Above 720p those offsets come out 1 / scale too small; multiply them by this
        // (1 at 1280x720, 3 at 3840x2160). World-minus-world deltas need nothing.
        // Read from the root canvas TRANSFORM (= its scaleFactor once applied), so it always agrees with the world positions read in the
        // same frame, also on a window's opening frame.
        public static float UnitScale(Component c)
        {
            var canvas = c != null ? c.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
                return 1f;
            float s = canvas.rootCanvas.transform.lossyScale.x;
            return s > 0f ? s : canvas.rootCanvas.scaleFactor;
        }
    }
}
#endif
