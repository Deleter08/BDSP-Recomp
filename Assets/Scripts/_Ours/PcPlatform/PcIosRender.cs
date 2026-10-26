#if !SWITCH
// iOS (Metal) and Android (Vulkan / GLES3) world-render switches (the ios_* keys apply to both, so one pc_settings.ini serves both
// phones), read once from <persistentDataPath>/pc_settings.ini so a fix can be toggled on the phone by pushing the ini (iOS: xcrun
// devicectl ... copy to Documents/pc_settings.ini; Android: adb push to /sdcard/Android/data/<package>/files/pc_settings.ini).
//   ios_dof_skip=1|0  skip the DepthOfField chain (full chain whitened the iPhone frame, as on the Pixel)   default 1
//   ios_depth=copy|bind|builtin  _OpaqueDepthTexture source for water/fog: copy = CopyTexture of the depth target into its own RT
//                     (as BattleMultipleCameraCompositor does; default - binding the attachment that water is depth-tested
//                     against reads stale tile memory on Apple GPUs), bind = the attachment itself (Android/GLES), builtin = Mac
//   ios_cam_depth=1|0 main camera depthTextureMode |= Depth so _CameraDepthTexture exists (water/fog read it)     default 1
//   ios_flip=1|0      vertical flip of the world camera's final image (frame showed upside down with the  default 1 on iOS;
//                     DoF chain skipped: that chain's last pass was the one undoing Metal's RT flip)    Android: graphicsUVStartsAtTop
//                                                                                                       (Vulkan 1, GLES 0)
//   ios_shadow_cascades=0|1|2|4  runtime QualitySettings.shadowCascades (default 2; 0 = leave the tier's)
// Off device (Mac / editor) these report the old Android behaviour (skip on, flip off) and read no ini.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PcPlatform
{
    public static class PcIosRender
    {
        static bool _read;
        static bool _skipDof = true;
        static bool _flip;
        public const int DepthCopy = 0, DepthBind = 1, DepthBuiltin = 2;
        static int _depth = DepthCopy;
        static bool _camDepth = true;
        static bool _blurCapture = true;
        static bool _keepPost = true;
        static bool _fogDepthCopy = true;

        static void Read()
        {
            if (_read) return;
            _read = true;
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
#if UNITY_IOS
            _flip = true;
#else
            _flip = SystemInfo.graphicsUVStartsAtTop;   // Vulkan: same unflipped RT as Metal; GLES: already upright
#endif
            try
            {
                string v = PcSettingsIni.Get("ios_dof_skip");
                if (v == "0") _skipDof = false;
                v = PcSettingsIni.Get("ios_flip");
                if (v == "0") _flip = false;
                v = PcSettingsIni.Get("ios_depth");
                if (v == "bind") _depth = DepthBind; else if (v == "builtin") _depth = DepthBuiltin;
                v = PcSettingsIni.Get("ios_cam_depth");
                if (v == "0") _camDepth = false;
                v = PcSettingsIni.Get("ios_blur_capture");
                if (v == "0") _blurCapture = false;
                v = PcSettingsIni.Get("ios_keep_post");
                if (v == "0") _keepPost = false;
                v = PcSettingsIni.Get("ios_fog_depth_copy");
                if (v == "0") _fogDepthCopy = false;
            }
            catch (System.Exception e) { global::UnityEngine.Debug.LogWarning("PcIosRender: ini read failed: " + e.Message); }
            // ios_shadow_cascades (default 2): >1 makes Unity build the screen-space shadow mask the ported shaders expect
            { string sc = PcSettingsIni.Get("ios_shadow_cascades"); int n = sc == "1" ? 1 : sc == "4" ? 4 : sc == "0" ? 0 : 2;
              if (n > 0) global::UnityEngine.QualitySettings.shadowCascades = n;
              global::UnityEngine.Debug.Log("PcIosRender shadows: cascades=" + global::UnityEngine.QualitySettings.shadowCascades + " tier=" + global::UnityEngine.Graphics.activeTier
               ); }
            global::UnityEngine.Debug.Log("PcIosRender: ios_dof_skip=" + (_skipDof ? 1 : 0) + " ios_flip=" + (_flip ? 1 : 0) + " ios_depth=" + _depth
                + " ios_cam_depth=" + (_camDepth ? 1 : 0) + " ios_blur_capture=" + (_blurCapture ? 1 : 0) + " ios_keep_post=" + (_keepPost ? 1 : 0) + " ios_fog_depth_copy=" + (_fogDepthCopy ? 1 : 0) + " copyTextureSupport=" + SystemInfo.copyTextureSupport + " uvTop=" + SystemInfo.graphicsUVStartsAtTop);
#endif
        }

        public static bool SkipDof { get { Read(); return _skipDof; } }
        public static bool Flip { get { Read(); return _flip; } }
        public static int DepthMode { get { Read(); return _depth; } }
        public static bool CamDepth { get { Read(); return _camDepth; } }
        // ios_blur_capture=0: drop the X-menu backdrop capture that replaces the skipped DoF chain (ghost-overlay test)
        public static bool BlurCapture { get { Read(); return _blurCapture; } }
        // ios_keep_post=0: do not carry Bloom/fog over the skipped DoF pass (pre-2026-10-24 behaviour)
        public static bool KeepPost { get { Read(); return _keepPost; } }
        // ios_fog_depth_copy=0: fog samples the live depth attachment (stains test)
        public static bool FogDepthCopy { get { Read(); return _fogDepthCopy; } }

        static readonly Dictionary<Camera, CommandBuffer> _flips = new Dictionary<Camera, CommandBuffer>();
        static readonly int FlipTmp = Shader.PropertyToID("_PcIosFlipTmp");

        // Adds (once per camera) a final pass that mirrors the camera target vertically. No-op unless Flip.
        public static void AttachFlip(Camera cam)
        {
            if (cam == null || !Flip) return;
            DetachFlip(cam);
            var cb = new CommandBuffer { name = "PcIosRender.Flip" };
            cb.GetTemporaryRT(FlipTmp, -1, -1, 0, FilterMode.Point);
            cb.Blit(BuiltinRenderTextureType.CameraTarget, FlipTmp);
            cb.Blit(FlipTmp, BuiltinRenderTextureType.CameraTarget, new Vector2(1f, -1f), new Vector2(0f, 1f));
            cb.ReleaseTemporaryRT(FlipTmp);
            cam.AddCommandBuffer(CameraEvent.AfterImageEffects, cb);
            _flips[cam] = cb;
        }

        public static void DetachFlip(Camera cam)
        {
            if (cam == null) return;
            CommandBuffer cb;
            if (_flips.TryGetValue(cam, out cb))
            {
                cam.RemoveCommandBuffer(CameraEvent.AfterImageEffects, cb);
                cb.Release();
                _flips.Remove(cam);
            }
        }

        // ---- fog diagnostics (2026-10-24): Bloom Finalize's distance fog turned the frame white once its output was kept ----
        // ios_fog=0       fog keywords off (bloom only)
        // ios_fog_depth=  0 compositor/default _GlobalDepthTexture, 1 = as set (default), 2 = camera _CameraDepthTexture
        static int _fogDepth = -1; static int _fog = -1; static bool _fogLogged;
        static void ReadFog()
        {
            if (_fog >= 0) return;
            string v = PcSettingsIni.Get("ios_fog"); _fog = v == "0" ? 0 : 1;
            v = PcSettingsIni.Get("ios_fog_depth"); _fogDepth = v == "0" ? 0 : v == "2" ? 2 : 1;
            UnityEngine.Debug.Log("PcIosRender fog: ios_fog=" + _fog + " ios_fog_depth=" + _fogDepth);
        }
        public static void BeginFog(UnityEngine.Rendering.CommandBuffer cb)
        {
            ReadFog();
            if (_fogDepth == 2)
                cb.SetGlobalTexture("_GlobalDepthTexture", UnityEngine.Rendering.BuiltinRenderTextureType.Depth);
        }
        public static void FogParams(UnityEngine.Material m, UnityEngine.FogMode mode, float start, float end, float density, float farClip, UnityEngine.Color c, UnityEngine.Color far)
        {
            ReadFog();
            if (_fog == 0)
            {
                m.DisableKeyword("_FOGMODE_LINEAR"); m.DisableKeyword("_FOGMODE_EXP"); m.DisableKeyword("_FOGMODE_EXP2"); m.EnableKeyword("_FOGMODE_NONE");
            }
            if (!_fogLogged)
            {
                _fogLogged = true;
                UnityEngine.Debug.Log("PcIosRender fogparams mode=" + mode + " start=" + start + " end=" + end + " density=" + density + " far=" + farClip + " base=" + c + " farColor=" + far
                    + " zbuf=" + UnityEngine.Shader.GetGlobalVector("_ZBufferParams") + " reversedZ=" + UnityEngine.SystemInfo.usesReversedZBuffer);
            }
        }

        // ios_bloom_copy=1: Bloom Finalize replaced by a plain copy (is the finalize shader the white?)
        public static bool BloomCopy { get { ReadFog(); return PcSettingsIni.Get("ios_bloom_copy") == "1"; } }
        // GPU readback of the post temporary (Bloom output) and the compositor colour buffer, logged at frames 300/600/900
        static int _probeN;
        public static void Probe(UnityEngine.RenderTexture post)
        {
            int f = UnityEngine.Time.frameCount;
            if (_probeN >= 3 || f < 300 * (_probeN + 1)) return;
            _probeN++;
            Req("post", post);
            var comp = UnityEngine.Object.FindObjectOfType<global::MultipleCameraCompositor>();
            Req("color", comp != null ? comp.colorBufferTexture : null);
        }
        static void Req(string tag, UnityEngine.RenderTexture rt)
        {
            if (rt == null) { UnityEngine.Debug.Log("PcIosProbe " + tag + " null"); return; }
            string info = tag + " " + rt.width + "x" + rt.height + " " + rt.format;
            UnityEngine.Rendering.AsyncGPUReadback.Request(rt, 0, UnityEngine.TextureFormat.RGBAFloat, r =>
            {
                if (r.hasError) { UnityEngine.Debug.Log("PcIosProbe " + info + " error"); return; }
                var d = r.GetData<float>(); double R = 0, G = 0, B = 0, mx = 0; int n = 0;
                for (int k = 0; k < d.Length; k += 4 * 97) { R += d[k]; G += d[k + 1]; B += d[k + 2]; mx = System.Math.Max(mx, System.Math.Max(d[k], System.Math.Max(d[k + 1], d[k + 2]))); n++; }
                UnityEngine.Debug.Log("PcIosProbe " + info + " mean=" + (R / n).ToString("F3") + "," + (G / n).ToString("F3") + "," + (B / n).ToString("F3") + " max=" + mx.ToString("F2") + " n=" + n);
            });
        }

        // ios_main_color=0: old behaviour (CameraTarget as is)
        public static UnityEngine.Rendering.RenderTargetIdentifier MainColor(UnityEngine.Rendering.RenderTargetIdentifier id)
        {
            if (PcSettingsIni.Get("ios_main_color") == "0") return id;
            if (id != (UnityEngine.Rendering.RenderTargetIdentifier)UnityEngine.Rendering.BuiltinRenderTextureType.CameraTarget) return id;
            var comp = UnityEngine.Object.FindObjectOfType<global::MultipleCameraCompositor>();
            if (comp == null || comp.colorBufferTexture == null) return id;
            return new UnityEngine.Rendering.RenderTargetIdentifier(comp.colorBufferTexture);
        }

        // Camera-aware: redirect only for the world camera the compositor drives. Any other camera (Pokedex / summary model
        // views, which render into their own targetTexture) keeps CameraTarget = its own RT - redirecting those drew the
        // Pokedex model into the hidden world buffer and left the Pokedex view empty.
        public static UnityEngine.Rendering.RenderTargetIdentifier MainColor(UnityEngine.Rendering.RenderTargetIdentifier id, UnityEngine.Camera cam)
        {
            // only cameras that render into their own targetTexture keep their target (a null camera / the title and
            // world cameras keep the redirect - requiring comp.mainCamera == cam brought the white screen back on the title)
            if (cam != null && cam.targetTexture != null) return id;
            return MainColor(id);
        }
    }
}
#endif
