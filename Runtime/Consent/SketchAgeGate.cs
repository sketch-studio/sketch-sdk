using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sketch.Sdk
{
    /// <summary>
    /// Neutral age screen (FTC / Google Play mixed-audience pattern): asks the birth year on a blank keypad, with no default
    /// year, no hint of an age limit and no way to go back and change the answer. Every answer lets the player in; only the
    /// ad signals change (SketchAgePolicy). Built in code because the package ships no prefabs; one overlay canvas, no
    /// per-frame work while open.
    /// </summary>
    public sealed class SketchAgeGate : MonoBehaviour
    {
        const int SORTING_ORDER = 32760;
        const int MAX_AGE_YEARS = 120;
        static readonly Vector2 Reference = new Vector2(1080f, 1920f);
        static readonly Color Dim = new Color(0.05f, 0.06f, 0.1f, 0.94f);
        static readonly Color Card = new Color(0.16f, 0.18f, 0.26f, 1f);
        static readonly Color Key = new Color(0.27f, 0.3f, 0.42f, 1f);
        static readonly Color Accept = new Color(0.2f, 0.72f, 0.42f, 1f);
        static readonly Color Muted = new Color(0.62f, 0.66f, 0.78f, 1f);

        static SketchAgeGate _instance;
        static bool _waitingForScene;

        Font _font;
        Text _yearLabel;
        Text _hintLabel;
        Button _confirm;
        GameObject _ownEventSystem;
        readonly char[] _digits = new char[4];
        int _count;

        public static bool IsOpen => _instance != null;

        /// <summary>Show once the first scene is loaded (or at once when one already is). No-op when the age is known.</summary>
        public static void ShowWhenSceneReady()
        {
            if (SketchAudience.IsKnown || _instance != null) return;
            if (SceneManager.GetActiveScene().isLoaded) { Show(); return; }
            if (_waitingForScene) return;
            _waitingForScene = true;
            SceneManager.sceneLoaded += OnFirstScene;
        }

        static void OnFirstScene(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnFirstScene;
            _waitingForScene = false;
            Show();
        }

        public static void Show()
        {
            if (SketchAudience.IsKnown || _instance != null) return;
            var root = new GameObject("SketchAgeGate", typeof(RectTransform));
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<SketchAgeGate>();
            _instance.Build();
            SketchAnalytics.Log("sketch_age_gate_shown");
        }

        void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SORTING_ORDER;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;
            gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SketchAudience.Changed += OnAgeKnownElsewhere;

            var dim = Panel("Dim", transform, Dim);
            Stretch(dim.rectTransform);

            var card = Panel("Card", transform, Card);
            Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(880f, 1280f), Vector2.zero);

            SketchAgeGateText strings = SketchAgeGateText.For(Application.systemLanguage);
            Label("Title", card.transform, strings.Title, 60, FontStyle.Bold, Color.white, new Vector2(0f, 530f), new Vector2(800f, 150f));
            var field = Panel("YearField", card.transform, new Color(0.09f, 0.1f, 0.15f, 1f));
            Place(field.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(560f, 150f), new Vector2(0f, 370f));
            _yearLabel = Label("Year", field.transform, string.Empty, 84, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(560f, 150f));
            _hintLabel = Label("Hint", card.transform, strings.Hint, 34, FontStyle.Normal, Muted, new Vector2(0f, 255f), new Vector2(800f, 60f));

            const float keyW = 230f, keyH = 150f, gap = 24f;
            for (int i = 0; i < 9; i++)
            {
                int row = i / 3, col = i % 3;
                char digit = (char)('1' + i);
                KeyButton(digit.ToString(), card.transform, new Vector2((col - 1) * (keyW + gap), 130f - row * (keyH + gap)), new Vector2(keyW, keyH), Key, () => Type(digit));
            }
            float lastRow = 130f - 3 * (keyH + gap);
            KeyButton("←", card.transform, new Vector2(-(keyW + gap), lastRow), new Vector2(keyW, keyH), Key, Erase);
            KeyButton("0", card.transform, new Vector2(0f, lastRow), new Vector2(keyW, keyH), Key, () => Type('0'));
            _confirm = KeyButton(strings.Confirm, card.transform, new Vector2(keyW + gap, lastRow), new Vector2(keyW, keyH), Accept, Confirm);
            Refresh();
        }

        void Type(char digit)
        {
            if (_count >= _digits.Length) return;
            _digits[_count++] = digit;
            Refresh();
        }

        void Erase()
        {
            if (_count > 0) _count--;
            Refresh();
        }

        void Refresh()
        {
            var shown = new char[_digits.Length];
            for (int i = 0; i < shown.Length; i++) shown[i] = i < _count ? _digits[i] : '_';
            _yearLabel.text = string.Join(" ", Array.ConvertAll(shown, c => c.ToString()));
            _confirm.interactable = TryYear(out _);
        }

        bool TryYear(out int year)
        {
            year = 0;
            if (_count != _digits.Length) return false;
            for (int i = 0; i < _count; i++) year = year * 10 + (_digits[i] - '0');
            int now = DateTime.UtcNow.Year;
            return year >= now - MAX_AGE_YEARS && year <= now;
        }

        void Confirm()
        {
            if (!TryYear(out int year)) return;
            SketchAgeBracket bracket = SketchAgePolicy.FromBirthYear(year, DateTime.UtcNow.Year);
            if (bracket == SketchAgeBracket.Unknown) return;
            Close();
            SketchAudience.SetBracket(bracket);
        }

        void OnAgeKnownElsewhere()
        {
            if (SketchAudience.IsKnown && _instance == this) Close();
        }

        void Close()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SketchAudience.Changed -= OnAgeKnownElsewhere;
            if (_ownEventSystem != null) Destroy(_ownEventSystem);
            _instance = null;
            Destroy(gameObject);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A scene with its own EventSystem arrived: drop ours so only one stays active.
            if (_ownEventSystem == null) return;
            var current = EventSystem.current;
            if (current != null && current.gameObject != _ownEventSystem) { Destroy(_ownEventSystem); _ownEventSystem = null; return; }
            var all = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (all.Length > 1) { Destroy(_ownEventSystem); _ownEventSystem = null; }
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
#if ENABLE_LEGACY_INPUT_MANAGER
            _ownEventSystem = new GameObject("SketchAgeGateEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(_ownEventSystem);
#else
            SketchLog.Warn("Age screen: no EventSystem in the scene and the legacy input module is off; add an EventSystem to the first scene.");
#endif
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SketchAudience.Changed -= OnAgeKnownElsewhere;
            if (_instance == this) _instance = null;
        }

        static Image Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        Text Label(string name, Transform parent, string text, int size, FontStyle style, Color color, Vector2 position, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = size;
            label.raycastTarget = false;
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), box, position);
            return label;
        }

        Button KeyButton(string text, Transform parent, Vector2 position, Vector2 size, Color color, Action onClick)
        {
            var image = Panel("Key_" + text, parent, color);
            Place(image.rectTransform, new Vector2(0.5f, 0.5f), size, position);
            var button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());
            image.gameObject.AddComponent<SketchAgeGatePress>();
            Label("Text", image.transform, text, 64, FontStyle.Bold, Color.white, Vector2.zero, size);
            return button;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (_waitingForScene) SceneManager.sceneLoaded -= OnFirstScene;
            _waitingForScene = false;
            _instance = null;
        }
    }

    /// <summary>Press feedback for the age keypad (scale down on press).</summary>
    sealed class SketchAgeGatePress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        static readonly Vector3 Pressed = new Vector3(0.94f, 0.94f, 1f);
        public void OnPointerDown(PointerEventData eventData) => transform.localScale = Pressed;
        public void OnPointerUp(PointerEventData eventData) => transform.localScale = Vector3.one;
    }

    /// <summary>Neutral wording only: never mentions an age limit or why the year is asked in a way that invites lying.</summary>
    readonly struct SketchAgeGateText
    {
        public readonly string Title;
        public readonly string Hint;
        public readonly string Confirm;

        SketchAgeGateText(string title, string hint, string confirm)
        {
            Title = title;
            Hint = hint;
            Confirm = confirm;
        }

        public static SketchAgeGateText For(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Vietnamese: return new SketchAgeGateText("Bạn sinh năm nào?", "Nhập năm sinh của bạn", "OK");
                case SystemLanguage.Spanish: return new SketchAgeGateText("¿En qué año naciste?", "Escribe tu año de nacimiento", "OK");
                case SystemLanguage.Portuguese: return new SketchAgeGateText("Em que ano você nasceu?", "Digite seu ano de nascimento", "OK");
                case SystemLanguage.French: return new SketchAgeGateText("En quelle année es-tu né(e) ?", "Saisis ton année de naissance", "OK");
                case SystemLanguage.German: return new SketchAgeGateText("In welchem Jahr bist du geboren?", "Gib dein Geburtsjahr ein", "OK");
                case SystemLanguage.Indonesian: return new SketchAgeGateText("Kamu lahir tahun berapa?", "Masukkan tahun lahirmu", "OK");
                case SystemLanguage.Russian: return new SketchAgeGateText("В каком году ты родился?", "Введи год рождения", "OK");
                default: return new SketchAgeGateText("What year were you born?", "Enter your birth year", "OK");
            }
        }
    }
}
