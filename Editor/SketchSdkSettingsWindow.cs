using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>Bilingual Sketch SDK control centre for configuration, diagnostics and documentation.</summary>
    public sealed class SketchSdkSettingsWindow : EditorWindow
    {
        SketchSdkSettings _settings;
        UnityEditor.Editor _inspector;
        Vector2 _scroll;
        string _status = string.Empty;
        bool _vietnamese;
        GUIStyle _title, _subtitle, _card, _muted;

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
            _title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 22 };
            _subtitle = new GUIStyle(EditorStyles.label) { wordWrap = true, normal = { textColor = new Color(.62f, .66f, .72f) } };
            _card = new GUIStyle("HelpBox") { padding = new RectOffset(12, 12, 10, 10) };
            _muted = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(.58f, .62f, .68f) } };
        }

        void OnDisable()
        {
            if (_inspector != null) DestroyImmediate(_inspector);
        }

        void OnGUI()
        {
            if (_settings == null) _settings = SketchSdkEditorUtil.FindOrCreateSettings();
            if (_title == null) OnEnable();
            using (new EditorGUILayout.VerticalScope(_card))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("SKETCH SDK", _title);
                    GUILayout.FlexibleSpace();
                    _vietnamese = GUILayout.Toggle(_vietnamese, "Tiếng Việt", "Button", GUILayout.Width(100));
                }
                EditorGUILayout.LabelField(_vietnamese
                    ? "Monetization mobile, quyền riêng tư, analytics và remote config trong một lớp dễ bảo trì."
                    : "Mobile monetization, privacy, analytics and remote configuration in one maintainable layer.", _subtitle);
                EditorGUILayout.LabelField("v" + SketchSdkVersion.VERSION + "  •  " + AssetDatabase.GetAssetPath(_settings), _muted);
            }

            EditorGUILayout.LabelField(_vietnamese ? "Trạng thái hệ thống" : "System status", EditorStyles.boldLabel);
            DrawStatus();
            EditorGUILayout.Space(6);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            UnityEditor.Editor.CreateCachedEditor(_settings, null, ref _inspector);
            EditorGUI.BeginChangeCheck();
            _inspector.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(_settings);
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(_vietnamese ? "Lưu & kiểm tra" : "Save & Verify", GUILayout.Height(32))) SaveAndVerify();
                if (GUILayout.Button(_vietnamese ? "Chọn asset" : "Select Asset", GUILayout.Height(32))) SelectAsset();
                if (GUILayout.Button(_vietnamese ? "Mở tài liệu" : "Open docs", GUILayout.Height(32))) OpenDocs();
            }
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        void DrawStatus()
        {
            bool gma = SketchSdkEditorUtil.IsGoogleMobileAdsInstalled;
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStatusCard("Google Mobile Ads", gma);
                if (!gma && GUILayout.Button(_vietnamese ? "Cài đặt" : "Install", GUILayout.Width(80))) SketchGmaInstaller.Install();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStatusCard("Firebase Analytics", SketchSdkEditorUtil.IsFirebaseAnalyticsInstalled);
                DrawStatusCard("Sketch Server", _settings.HasServerCredentials);
                DrawStatusCard(SketchSdkEditorUtil.ADMOB_DEFINE, SketchSdkEditorUtil.AdMobDefineActive);
            }

            List<SketchSdkEditorUtil.Issue> issues = SketchSdkEditorUtil.Validate(_settings, forRelease: false);
            foreach (var issue in issues) EditorGUILayout.HelpBox(issue.Text, issue.Type);
        }

        void DrawStatusCard(string label, bool ready)
        {
            using (new EditorGUILayout.VerticalScope(_card, GUILayout.MinHeight(48)))
            {
                Color old = GUI.color;
                GUI.color = ready ? new Color(.35f, .82f, .48f) : new Color(.95f, .62f, .28f);
                EditorGUILayout.LabelField(ready ? "● READY" : "● SETUP", EditorStyles.boldLabel);
                GUI.color = old;
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            }
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
            Debug.Log("[SketchSdk] " + _status);
        }
    }
}
