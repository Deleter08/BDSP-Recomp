#if !SWITCH
// PC platform layer: an on-screen Pro Controller for touch screens (Android, 2026-10-20). NOT shipped code.
//
// The game reads one Pro Controller (PcHid, Npad No1 FullKey); the console's own touch screen stays "docked, no touches" (PcTouch).
// On a phone the controller is drawn over the game and driven by Input.touches: left stick, d-pad, A/B/X/Y (Switch positions:
// A right, B bottom, X top, Y left), L/ZL and R/ZR in the top corners, -/+ at the top centre, a small right stick, and a "pad" toggle
// between - and + that hides / shows the rest. PcHid ORs Apply() into the state it builds from the keyboard / pads, so a Bluetooth
// pad keeps working alongside it.
//   - Buttons are hit-tested every frame at the finger's CURRENT position (a thumb can slide from B to A).
//   - A stick belongs to the finger that touched down inside it until that finger lifts (it may then leave the circle).
//   - No EventSystem / raycasts: the images are raycastTarget = false, so the overlay never takes input from the game's own UI.
// Layout unit u = 1/10 of the safe area's height; everything is placed inside Screen.safeArea. Every layout change logs the
// screen-pixel centre of each control (and the `adb shell input` coordinates, y from the top) for scripted taps.
using System.Collections.Generic;
using nn.hid;
using UnityEngine;
using UnityEngine.UI;

namespace PcPlatform
{
    public sealed class PcVirtualPad : MonoBehaviour
    {
        const int StickMax = 32767;
        const float DeadZone = 0.12f;

        enum Shape { Circle, Pill }

        sealed class Control
        {
            public string Name;
            public NpadButton Button;       // None for sticks / the toggle
            public int Stick;               // 0 = not a stick, 1 = left, 2 = right
            public bool Toggle;
            public Shape Shape;
            public Vector2 Center;          // screen pixels, origin bottom-left (Input.touches)
            public Vector2 Size;            // full width / height in pixels (circles: Size.x = diameter)
            public Image Image;
            public RectTransform Knob;
            public bool Pressed;
            public int Finger = -1;         // the finger that owns a stick
            public Vector2 Value;           // stick: -1..1

            public bool Hit(Vector2 p, float slack)
            {
                if (Shape == Shape.Circle)
                    return (p - Center).sqrMagnitude <= (Size.x * 0.5f + slack) * (Size.x * 0.5f + slack);
                return Mathf.Abs(p.x - Center.x) <= Size.x * 0.5f + slack && Mathf.Abs(p.y - Center.y) <= Size.y * 0.5f + slack;
            }
        }

        static PcVirtualPad instance;
        static bool visible = true;

        readonly List<Control> controls = new List<Control>();
        RectTransform root;
        Rect laidOutFor;
        Sprite circle;
        Font font;
        int lastFrame = -1;
        bool togglePressedLast;

        // ---- PcHid's entry point -----------------------------------------------------------------------------------------------
        public static void Apply(ref NpadButton b, ref int lx, ref int ly, ref int rx, ref int ry)
        {
            if (instance == null)
                Create();
            instance.Sample();
            if (!visible)
                return;
            foreach (var c in instance.controls)
            {
                if (c.Button != NpadButton.None && c.Pressed)
                    b |= c.Button;
                else if (c.Stick == 1)
                {
                    lx += (int)(c.Value.x * StickMax);
                    ly += (int)(c.Value.y * StickMax);
                }
                else if (c.Stick == 2)
                {
                    rx += (int)(c.Value.x * StickMax);
                    ry += (int)(c.Value.y * StickMax);
                }
            }
        }

        static void Create()
        {
            var go = new GameObject("PcVirtualPad");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PcVirtualPad>();
            instance.Build();
            PcConfig.Log("hid: on-screen Pro Controller (touch) created - " + Input.touchSupported + " touch support, multi-touch " + Input.multiTouchEnabled);
        }

        // ---- the overlay -----------------------------------------------------------------------------------------------------------
        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            root = (RectTransform)transform;
            circle = MakeCircleSprite(128);
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            Add("LStick", NpadButton.None, 1, Shape.Circle);
            Add("RStick", NpadButton.None, 2, Shape.Circle);
            Add("Up", NpadButton.Up, 0, Shape.Circle);
            Add("Down", NpadButton.Down, 0, Shape.Circle);
            Add("Left", NpadButton.Left, 0, Shape.Circle);
            Add("Right", NpadButton.Right, 0, Shape.Circle);
            Add("A", NpadButton.A, 0, Shape.Circle);
            Add("B", NpadButton.B, 0, Shape.Circle);
            Add("X", NpadButton.X, 0, Shape.Circle);
            Add("Y", NpadButton.Y, 0, Shape.Circle);
            Add("L", NpadButton.L, 0, Shape.Pill);
            Add("ZL", NpadButton.ZL, 0, Shape.Pill);
            Add("R", NpadButton.R, 0, Shape.Pill);
            Add("ZR", NpadButton.ZR, 0, Shape.Pill);
            Add("-", NpadButton.Minus, 0, Shape.Circle);
            Add("+", NpadButton.Plus, 0, Shape.Circle);
            var t = Add("pad", NpadButton.None, 0, Shape.Pill);
            t.Toggle = true;
            Layout();
        }

        Control Add(string name, NpadButton button, int stick, Shape shape)
        {
            var c = new Control { Name = name, Button = button, Stick = stick, Shape = shape };
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);
            c.Image = go.AddComponent<Image>();
            c.Image.sprite = circle;
            c.Image.type = shape == Shape.Pill ? Image.Type.Sliced : Image.Type.Simple;
            c.Image.raycastTarget = false;
            if (stick != 0)
            {
                var knob = new GameObject("knob", typeof(RectTransform));
                knob.transform.SetParent(go.transform, false);
                var ki = knob.AddComponent<Image>();
                ki.sprite = circle;
                ki.raycastTarget = false;
                ki.color = new Color(1f, 1f, 1f, 0.55f);
                c.Knob = (RectTransform)knob.transform;
            }
            else
            {
                var label = new GameObject("label", typeof(RectTransform));
                label.transform.SetParent(go.transform, false);
                var text = label.AddComponent<Text>();
                text.font = font;
                text.text = Glyph(name);
                text.alignment = TextAnchor.MiddleCenter;
                text.color = new Color(0f, 0f, 0f, 0.8f);
                text.raycastTarget = false;
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 8;
                text.resizeTextMaxSize = 200;
                var lr = (RectTransform)label.transform;
                lr.anchorMin = new Vector2(0.15f, 0.15f);
                lr.anchorMax = new Vector2(0.85f, 0.85f);
                lr.offsetMin = lr.offsetMax = Vector2.zero;
            }
            controls.Add(c);
            return c;
        }

        static string Glyph(string name)
        {
            switch (name)
            {
                case "Up": return "▲";
                case "Down": return "▼";
                case "Left": return "◀";
                case "Right": return "▶";
                default: return name;
            }
        }

        void Layout()
        {
            Rect s = Screen.safeArea;
            laidOutFor = s;
            float u = s.height / 10f;
            float x0 = s.xMin, x1 = s.xMax, y0 = s.yMin, y1 = s.yMax, cx = s.center.x;
            Place("LStick", x0 + 2.6f * u, y0 + 2.6f * u, 3.2f * u, 0);
            Place("RStick", x1 - 5.7f * u, y0 + 1.6f * u, 2.0f * u, 0);
            Vector2 d = new Vector2(x0 + 2.6f * u, y0 + 6.4f * u);
            float arm = 0.85f * u, dsize = 1.1f * u;
            Place("Up", d.x, d.y + arm, dsize, 0);
            Place("Down", d.x, d.y - arm, dsize, 0);
            Place("Left", d.x - arm, d.y, dsize, 0);
            Place("Right", d.x + arm, d.y, dsize, 0);
            Vector2 f = new Vector2(x1 - 2.5f * u, y0 + 3.0f * u);
            float off = 1.15f * u, fsize = 1.3f * u;
            Place("A", f.x + off, f.y, fsize, 0);
            Place("B", f.x, f.y - off, fsize, 0);
            Place("X", f.x, f.y + off, fsize, 0);
            Place("Y", f.x - off, f.y, fsize, 0);
            float sy = y1 - 0.75f * u, sw = 2.0f * u, sh = 1.0f * u;
            Place("L", x0 + 1.3f * u, sy, sw, sh);
            Place("ZL", x0 + 3.6f * u, sy, sw, sh);
            Place("R", x1 - 1.3f * u, sy, sw, sh);
            Place("ZR", x1 - 3.6f * u, sy, sw, sh);
            Place("-", cx - 1.9f * u, sy, 0.9f * u, 0);
            Place("+", cx + 1.9f * u, sy, 0.9f * u, 0);
            Place("pad", cx, sy, 1.6f * u, 0.7f * u);
            // adb `input` takes DEVICE pixels, y from the top; touches + Screen are in render pixels when the game lowered the resolution
            float kx = Display.main.systemWidth / (float)Screen.width, ky = Display.main.systemHeight / (float)Screen.height;
            var sb = new System.Text.StringBuilder();
            foreach (var c in controls)
                sb.AppendFormat(" {0}=({1:F0},{2:F0})", c.Name, c.Center.x * kx, (Screen.height - c.Center.y) * ky);
            PcConfig.Log(string.Format("hid: on-screen controller layout for {0}x{1} (device {2}x{3}), safe area {4}; adb `input tap` x,y:{5}",
                Screen.width, Screen.height, Display.main.systemWidth, Display.main.systemHeight, s, sb));
            Refresh();
        }

        void Place(string name, float x, float y, float w, float h)
        {
            var c = controls.Find(k => k.Name == name);
            c.Center = new Vector2(x, y);
            c.Size = new Vector2(w, c.Shape == Shape.Pill ? h : w);
            var rt = (RectTransform)c.Image.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;   // bottom-left of the overlay canvas = screen pixel (0,0)
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = c.Center;
            rt.sizeDelta = c.Size;
            if (c.Knob != null)
            {
                c.Knob.anchorMin = c.Knob.anchorMax = new Vector2(0.5f, 0.5f);
                c.Knob.sizeDelta = c.Size * 0.45f;
                c.Knob.anchoredPosition = Vector2.zero;
            }
        }

        // ---- input -------------------------------------------------------------------------------------------------------------
        void Sample()
        {
            if (Time.frameCount == lastFrame)
                return;
            lastFrame = Time.frameCount;
            if (Screen.safeArea != laidOutFor)
                Layout();

            int n = Input.touchCount;
            bool togglePressed = false;
            foreach (var c in controls)
            {
                c.Pressed = false;
                if (c.Stick != 0)
                {
                    bool alive = false;
                    for (int i = 0; i < n; i++)
                    {
                        var t = Input.GetTouch(i);
                        if (t.fingerId == c.Finger && t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled)
                        {
                            alive = true;
                            break;
                        }
                    }
                    if (!alive)
                        c.Finger = -1;
                }
            }
            for (int i = 0; i < n; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    continue;
                Control stick = controls.Find(c => c.Stick != 0 && c.Finger == t.fingerId);
                if (stick == null && t.phase == TouchPhase.Began && visible)
                {
                    stick = controls.Find(c => c.Stick != 0 && c.Finger < 0 && c.Hit(t.position, 0f));
                    if (stick != null)
                        stick.Finger = t.fingerId;
                }
                if (stick != null)
                {
                    Vector2 v = (t.position - stick.Center) / (stick.Size.x * 0.5f);
                    if (v.sqrMagnitude > 1f)
                        v.Normalize();
                    stick.Value = v.magnitude < DeadZone ? Vector2.zero : v;
                    continue;
                }
                // buttons: the closest hit one (slack lets a thumb between two d-pad arrows press the nearer)
                Control best = null;
                float bestD = float.MaxValue;
                foreach (var c in controls)
                {
                    if (c.Stick != 0 || (!visible && !c.Toggle))
                        continue;
                    if (!c.Hit(t.position, 0.15f * c.Size.y))
                        continue;
                    float dd = (t.position - c.Center).sqrMagnitude;
                    if (dd < bestD)
                    {
                        bestD = dd;
                        best = c;
                    }
                }
                if (best == null)
                    continue;
                if (best.Toggle)
                    togglePressed = true;
                else
                    best.Pressed = true;
            }
            foreach (var c in controls)
                if (c.Stick != 0 && c.Finger < 0)
                    c.Value = Vector2.zero;
            if (togglePressed && !togglePressedLast)
            {
                visible = !visible;
                PcConfig.Log("hid: on-screen controller " + (visible ? "shown" : "hidden"));
            }
            togglePressedLast = togglePressed;
            Refresh();
        }

        void Refresh()
        {
            foreach (var c in controls)
            {
                bool show = visible || c.Toggle;
                c.Image.enabled = show;
                foreach (var g in c.Image.GetComponentsInChildren<Graphic>())
                    g.enabled = show;
                bool active = c.Pressed || (c.Stick != 0 && c.Finger >= 0);
                c.Image.color = new Color(1f, 1f, 1f, active ? 0.55f : 0.28f);
                if (c.Knob != null)
                    c.Knob.anchoredPosition = c.Value * (c.Size.x * 0.5f);
            }
        }

        static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float r = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                    float a = Mathf.Clamp01(r - d);                                        // 1-pixel anti-aliased edge
                    float ring = d > r - size * 0.06f ? 1f : 0.75f;                        // a brighter rim
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(255 * a * ring));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            // border = radius: a Sliced image stretches the middle, which turns the circle into a pill for the shoulder buttons
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(r - 1, r - 1, r - 1, r - 1));
        }
    }
}
#endif
