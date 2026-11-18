using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Sketch.Sdk.Editor
{
    /// <summary>Bilingual Sketch SDK control centre for configuration, diagnostics and documentation.</summary>
    public sealed class SketchSdkSettingsWindow : EditorWindow
    {
        SketchSdkSettings _settings;
        UnityEditor.Editor _inspector;
        string _status = string.Empty;
        bool _vietnamese;
        ScrollView _settingsScroll;
        Label _statusLabel;

        [MenuItem("Sketch SDK/Settings", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<SketchSdkSettingsWindow>("Sketch SDK");
            window.minSize = new Vector2(560, 680);
            window.Show();
        }

        [MenuItem("Sketch SDK/Select Settings Asset", priority = 1)]
        static void SelectAsset()
        {
            SketchSdkSettings settings = SketchSdkEditorUtil.FindOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Sketch SDK/Install Google Mobile Ads", priority = 20)]
        static void InstallGma() => SketchGmaInstaller.Install();

        [MenuItem("Sketch SDK/Documentation", priority = 40)]
        static void OpenDocs() => Application.OpenURL("https://github.com/sketch-studio/sketch-sdk#documentation");

        void OnEnable()
        {
            _settings = SketchSdkEditorUtil.FindOrCreateSettings();
            CreateGUI();
        }

        void OnDisable()
        {
            if (_inspector != null) DestroyImmediate(_inspector);
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 16;
            rootVisualElement.style.paddingRight = 16;
            rootVisualElement.style.paddingTop = 14;
            rootVisualElement.style.paddingBottom = 12;
            var header = new VisualElement();
            header.style.paddingBottom = 10;
            var titleRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            titleRow.Add(new Label("SKETCH SDK") { style = { fontSize = 22, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
            var language = new Button(() => { _vietnamese = !_vietnamese; CreateGUI(); }) { text = _vietnamese ? "English" : "Tiếng Việt" };
            titleRow.Add(language);
            header.Add(titleRow);
            header.Add(new Label(_vietnamese ? "Monetization mobile, quyền riêng tư, analytics và remote config trong một lớp dễ bảo trì." : "Mobile monetization, privacy, analytics and remote configuration in one maintainable layer"));
            header.Add(new Label("v" + SketchSdkVersion.VERSION + "  •  " + AssetDatabase.GetAssetPath(_settings)) { style = { opacity = .65f, marginTop = 4 } });
            rootVisualElement.Add(header);
            rootVisualElement.Add(new Label(_vietnamese ? "Trạng thái hệ thống" : "System status") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } });
            var status = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginBottom = 8 } };
            AddStatusCard(status, "Google Mobile Ads", SketchSdkEditorUtil.IsGoogleMobileAdsInstalled);
            AddStatusCard(status, "Firebase Analytics", SketchSdkEditorUtil.IsFirebaseAnalyticsInstalled);
            AddStatusCard(status, "Sketch Server", _settings.HasServerCredentials);
            AddStatusCard(status, SketchSdkEditorUtil.ADMOB_DEFINE, SketchSdkEditorUtil.AdMobDefineActive);
            rootVisualElement.Add(status);
            _settingsScroll = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            UnityEditor.Editor.CreateCachedEditor(_settings, null, ref _inspector);
            _settingsScroll.Add(new IMGUIContainer(() => { _inspector?.OnInspectorGUI(); }) { style = { minHeight = 420 } });
            rootVisualElement.Add(_settingsScroll);
            var buttons = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 8 } };
            buttons.Add(new Button(SaveAndVerify) { text = _vietnamese ? "Lưu & kiểm tra" : "Save & Verify" });
            buttons.Add(new Button(SelectAsset) { text = _vietnamese ? "Chọn asset" : "Select Asset" });
            buttons.Add(new Button(OpenDocs) { text = _vietnamese ? "Mở tài liệu" : "Open docs" });
            rootVisualElement.Add(buttons);
            _statusLabel = new Label(_status) { style = { marginTop = 6, whiteSpace = WhiteSpace.Normal } };
            rootVisualElement.Add(_statusLabel);
        }

        void AddStatusCard(VisualElement parent, string label, bool ready)
        {
            var card = new VisualElement { style = { minWidth = 120, marginRight = 6, marginBottom = 6, paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 6, backgroundColor = new Color(0.12f, 0.14f, 0.18f) } };
            card.Add(new Label(ready ? "● READY" : "● SETUP") { style = { color = ready ? new Color(.35f, .82f, .48f) : new Color(.95f, .62f, .28f), unityFontStyleAndWeight = FontStyle.Bold } });
            card.Add(new Label(label) { style = { opacity = .8f } });
            parent.Add(card);
        }

        void SaveAndVerify()
        {
            _settings.Android.Trim();
            _settings.Ios.Trim();
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            SketchSdkEditorUtil.SyncAdMobDefine();
            SketchSdkEditorUtil.SyncFirebaseDefine();
            string sync = SketchSdkEditorUtil.SyncAppIds(_settings);
            List<SketchSdkEditorUtil.Issue> issues = SketchSdkEditorUtil.Validate(_settings, forRelease: true);
            int errors = issues.FindAll(i => i.Type == MessageType.Error).Count;
            _status = $"Saved. {sync} Release check: {errors} error(s), {issues.Count - errors} note(s).";
            if (_statusLabel != null) _statusLabel.text = _status;
            Debug.Log("[SketchSdk] " + _status);
        }
    }
}
