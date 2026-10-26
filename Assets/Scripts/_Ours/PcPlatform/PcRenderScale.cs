#if !SWITCH
// PC platform layer: the render textures of the UI 3D views at the screen's pixel density (author s40, a 3840x2160 trainer card
// screenshot: "It doesn't looked like true 4k at all, especially the 3d model. Any workaround?"). NOT shipped code.
//
// The shipped game sizes these render textures in units of its 1280x720 UI - a RawImage's rect, or a literal 1280x720 - which are
// screen pixels at 720p. Above 720p the textures are stretched: x3 at 3840x2160. Affected: the trainer card (its WHOLE card - text,
// art and the player model - is drawn by CardModelView's cameras into the model view's texture), the Pokédex / summary / party model
// view, ball capsules, badge get, the multi-model view (Union Room / matching / contest), the demos (DemoCamera: intro, evolution,
// hatch, trade, Hall of Fame, Mystery Gift ...), PokeRenderViewer (starter choice, Pokémon viewer), the field-move cut-in capture, the
// GMS view and the Secret Base statue editor's pedestal view.
//
// Each of those textures is made `Factor` times larger, Factor = screen pixels per UI unit = the main canvas's scale factor (UIManager
// "Canvas": overlay, ScaleWithScreenSize 1280x720, match width -> Screen.width / 1280), never below 1. Every camera-space canvas drawn
// into such a texture with a Constant Pixel Size scaler (CardModelView, Capsule3DView, UIManager's model-view background) gets
// scaleFactor x Factor, so its layout in UI units is unchanged. Camera aspects and every UI-unit coordinate the game computes (uvRects,
// GMS screen positions) are unchanged; screen-pixel <-> texture-pixel picking (ball capsule seals, badge case touch) keeps the shipped
// 720p relation "screen pixel = texture pixel" at every resolution.
//
// pc_settings.ini `ui_render=native` (default) | `shipped` (Factor 1 = the shipped sizes), read once.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PcPlatform
{
    public static class PcRenderScale
    {
        const float UiWidth = 1280f;   // UIManager "Canvas" CanvasScaler reference resolution 1280x720, match width (0)

        static int mode = -1;   // -1 unread, 0 shipped, 1 native
        static readonly Dictionary<RawImage, Texture> shippedTexture = new Dictionary<RawImage, Texture>();

        public static bool Native
        {
            get
            {
                if (mode < 0)
                {
                    var v = PcSettingsIni.Get("ui_render");
                    mode = string.Equals(v, "shipped", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                    UnityEngine.Debug.Log("[PcPlatform] ui render: " + (mode == 1 ? "native (UI 3D views at screen pixel density)" : "shipped (1280x720 UI units)")
                        + " (pc_settings.ini ui_render=native|shipped)");
                }
                return mode == 1;
            }
        }

        // screen pixels per UI unit, at least 1; 1 when ui_render=shipped
        public static float Factor
        {
            get { return Native ? Mathf.Max(1f, PcUiRoot.UiScale) : 1f; }
        }

        // a shipped size in UI units -> texture pixels (truncated as the shipped (int) casts are; k = 1 gives the shipped value)
        public static int Px(float units, float k)
        {
            return Mathf.Max(1, (int)(units * k));
        }

        // Every Screen Space - Camera canvas rendered by `camera` whose scaler is Constant Pixel Size: scaleFactor = shipped value x k.
        public static void ScaleCanvases(Camera camera, float k)
        {
            if (camera == null)
                return;

            foreach (var canvas in camera.transform.root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.worldCamera != camera)
                    continue;

                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
                    continue;

                // one shipped-value record with PcUiRoot's screen-canvas scaling (a camera canvas may render to the screen first)
                scaler.scaleFactor = PcUiRoot.ShippedScale(scaler) * k;
            }
        }

        // The Secret Base pedestal view: a 1280x1280 RenderTexture ASSET shown in a 720x720 RawImage. When the screen needs more pixels
        // than the asset has, the RawImage gets a larger copy of its descriptor (the asset itself is never changed).
        public static void FitSquare(RawImage image)
        {
            if (image == null)
                return;

            Texture shipped;
            if (!shippedTexture.TryGetValue(image, out shipped))
            {
                shipped = image.texture;
                shippedTexture[image] = shipped;
            }

            var asset = shipped as RenderTexture;
            if (asset == null)
                return;

            var rect = image.rectTransform.rect;
            int need = Px(Mathf.Max(rect.width, rect.height), Factor);
            int size = Mathf.Max(asset.width, need);
            var current = image.texture as RenderTexture;
            if (current != null && current.width == size)
                return;

            if (current != null && current != asset)
                current.Release();

            if (size == asset.width)
            {
                image.texture = asset;
                return;
            }

            var desc = asset.descriptor;
            desc.width = size;
            desc.height = size;
            var copy = new RenderTexture(desc);
            copy.name = asset.name + " (PC " + size + ")";
            image.texture = copy;
        }
    }
}
#endif
