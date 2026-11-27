#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sketch.Sdk
{
    /// <summary>
    /// Editor stand-in for the Android custom native layout (SketchAdmobNative.java): white panel at the exact screen
    /// rect, the same 26 dp round countdown close 6 dp inside the top-right corner, dimmed ring + X with a bright arc
    /// growing clockwise for SketchNativeClose.CountdownSeconds (0 = tappable at once), then bright and tappable. Only the user closes it. Own overlay canvas, so
    /// the panel eats taps like the device overlay. Never compiled into players.
    /// </summary>
    public sealed class SketchEditorNativeView : MonoBehaviour
    {
        public const string PREF_KEY = "Sketch.Sdk.EditorNativeFake";
        const int SORTING_ORDER = 30000;
        const float TRACK_ALPHA_WAITING = 80f / 255f;
        const float GLYPH_ALPHA_WAITING = 110f / 255f;

        static SketchEditorNativeView _instance;
        static Sprite _ringSprite, _dotSprite;

        Action _onUserClose;
        Image _track, _trackEdge, _arc, _arcEdge, _glyphA, _glyphB, _glyphEdgeA, _glyphEdgeB;
        Button _close;
        RectTransform _panelRect, _closeRect;
        Text _label;
        string _placement;
        float _density;
        bool _onLeft;
        float _remaining;
        int _countdown;
        bool _ready;

        public static bool Enabled
        {
            get => UnityEditor.EditorPrefs.GetBool(PREF_KEY, true);
            set => UnityEditor.EditorPrefs.SetBool(PREF_KEY, value);
        }

        public static bool IsShowing => _instance != null;
        public static Rect LastCloseRect { get; private set; }
        public static bool LastCloseOnLeft { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _instance = null;

        public static void Show(Rect screenPixels, string placement, Action onUserClose)
        {
            if (!Application.isPlaying) return;
            Close();
            var go = new GameObject("[SketchEditorNative]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SketchEditorNativeView>();
            _instance.Build(screenPixels, placement, onUserClose);
        }

        /// <summary>1.5.14: moves / resizes the fake native that is on screen (same close state and callback).</summary>
        public static bool Move(Rect screenPixels)
        {
            var view = _instance;
            if (view == null || view._panelRect == null) return false;
            view._panelRect.anchoredPosition = screenPixels.position;
            view._panelRect.sizeDelta = screenPixels.size;
            Rect closeRect = SketchNativeClose.CloseRect(screenPixels, view._density, view._onLeft);
            LastCloseRect = closeRect;
            if (view._closeRect != null)
            {
                view._closeRect.anchoredPosition = closeRect.position;
                view._closeRect.sizeDelta = closeRect.size;
            }
            if (view._label != null)
                view._label.text = $"NATIVE IN-RECT (Editor)\n{view._placement}\n{screenPixels.width:F0} x {screenPixels.height:F0} px";
            LastPanelRect = screenPixels;
            return true;
        }

        public static Rect LastPanelRect { get; private set; }

        public static void Close()
        {
            var view = _instance;
            _instance = null;
            if (view == null) return;
            view._onUserClose = null;
            Destroy(view.gameObject);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Build(Rect rect, string placement, Action onUserClose)
        {
            _onUserClose = onUserClose;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            gameObject.AddComponent<GraphicRaycaster>();

            // Overlay canvas, no scaler: 1 unit = 1 screen pixel = the rect the Android layout receives.
            RectTransform panel = Rect01("Panel", transform, rect);
            _panelRect = panel;
            _placement = placement;
            LastPanelRect = rect;
            panel.gameObject.AddComponent<Image>().color = Color.white;

            Text label = Child("Label", panel).gameObject.AddComponent<Text>();
            Stretch((RectTransform)label.transform, 24f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = Mathf.RoundToInt(Mathf.Min(rect.width, rect.height) * 0.08f);
            label.color = new Color(0.35f, 0.35f, 0.35f);
            label.raycastTarget = false;
            label.text = $"NATIVE IN-RECT (Editor)\n{placement}\n{rect.width:F0} x {rect.height:F0} px";
            _label = label;

            bool onLeft = SketchNativeClose.NextOnLeft();
            float density = SketchNativeClose.EditorDensity(Screen.width, Screen.height);
            Rect closeRect = SketchNativeClose.CloseRect(rect, density, onLeft);
            LastCloseRect = closeRect;
            LastCloseOnLeft = onLeft;
            _density = density;
            _onLeft = onLeft;
            BuildClose(closeRect, density);
            _countdown = SketchNativeClose.CountdownSeconds;
            _remaining = _countdown;
            _ready = _countdown <= 0;
            Refresh();
        }

        void BuildClose(Rect rect, float density)
        {
            RectTransform hit = Rect01("Close", transform, rect);
            _closeRect = hit;
            var hitImage = hit.gameObject.AddComponent<Image>();
            hitImage.color = Color.clear;
            _close = hit.gameObject.AddComponent<Button>();
            _close.targetGraphic = hitImage;
            _close.transition = Selectable.Transition.None;
            _close.onClick.AddListener(OnCloseClicked);

            Sprite ring = RingSprite();
            float edgeWidth = 3f * density, ringWidth = 2f * density;
            _trackEdge = Layer("TrackEdge", hit, ring, new Color(20f / 255f, 20f / 255f, 20f / 255f), 0f);
            _track = Layer("Track", hit, ring, Color.white, (edgeWidth - ringWidth) * 0.5f);
            _arcEdge = Layer("ArcEdge", hit, ring, new Color(20f / 255f, 20f / 255f, 20f / 255f, 200f / 255f), 0f);
            _arc = Layer("Arc", hit, ring, Color.white, (edgeWidth - ringWidth) * 0.5f);
            foreach (Image arc in new[] { _arcEdge, _arc })
            {
                arc.type = Image.Type.Filled;
                arc.fillMethod = Image.FillMethod.Radial360;
                arc.fillOrigin = (int)Image.Origin360.Top;
                arc.fillClockwise = true;
            }

            float glyph = rect.width * 0.28f * Mathf.Sqrt(2f);
            _glyphEdgeA = Bar("GlyphEdgeA", hit, glyph + 3f * density, 5f * density, 45f, new Color(20f / 255f, 20f / 255f, 20f / 255f));
            _glyphEdgeB = Bar("GlyphEdgeB", hit, glyph + 3f * density, 5f * density, -45f, new Color(20f / 255f, 20f / 255f, 20f / 255f));
            _glyphA = Bar("GlyphA", hit, glyph, 2f * density, 45f, Color.white);
            _glyphB = Bar("GlyphB", hit, glyph, 2f * density, -45f, Color.white);
        }

        void Update()
        {
            if (_ready) return;
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f) _ready = true;
            Refresh();
        }

        void Refresh()
        {
            float remaining = Mathf.Clamp01(_remaining / Mathf.Max(1f, _countdown));
            float track = _ready ? 1f : TRACK_ALPHA_WAITING;
            SetAlpha(_trackEdge, track);
            SetAlpha(_track, track);
            float sweep = _ready ? 0f : 1f - remaining;
            _arc.fillAmount = _arcEdge.fillAmount = sweep;
            _arc.enabled = _arcEdge.enabled = sweep > 0f;
            float glyph = _ready ? 1f : GLYPH_ALPHA_WAITING;
            SetAlpha(_glyphA, glyph);
            SetAlpha(_glyphB, glyph);
            SetAlpha(_glyphEdgeA, _ready ? 1f : GLYPH_ALPHA_WAITING * 0.5f);
            SetAlpha(_glyphEdgeB, _ready ? 1f : GLYPH_ALPHA_WAITING * 0.5f);
            // Waiting: the close ignores taps (device: not clickable, taps reach the ad).
            _close.interactable = _ready;
            if (_ready) enabled = false;
        }

        void OnCloseClicked()
        {
            if (!_ready) return;
            Action callback = _onUserClose;
            Close();
            callback?.Invoke();
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            Color c = graphic.color;
            c.a = alpha * (graphic.name.StartsWith("ArcEdge") ? 200f / 255f : 1f);
            graphic.color = c;
        }

        static RectTransform Rect01(string name, Transform parent, Rect pixels)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = pixels.position;
            rect.sizeDelta = pixels.size;
            return rect;
        }

        static RectTransform Child(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        static Image Layer(string name, RectTransform parent, Sprite sprite, Color color, float inset)
        {
            RectTransform rect = Child(name, parent);
            Stretch(rect, inset);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Image Bar(string name, RectTransform parent, float length, float thickness, float angle, Color color)
        {
            RectTransform rect = Child(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(length, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = DotSprite();
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // Anti-aliased annulus (outer radius 1, stroke ~12 % of the size) used by track and arc.
        static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float center = (size - 1) * 0.5f, outer = size * 0.5f - 1f, inner = outer - size * 0.09f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            _ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return _ringSprite;
        }

        // Rounded-cap bar sprite (sliced) for the X strokes.
        static Sprite DotSprite()
        {
            if (_dotSprite != null) return _dotSprite;
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float center = (size - 1) * 0.5f, radius = size * 0.5f - 1f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _dotSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(15f, 15f, 15f, 15f));
            _dotSprite.hideFlags = HideFlags.HideAndDontSave;
            return _dotSprite;
        }
    }
}
#endif
