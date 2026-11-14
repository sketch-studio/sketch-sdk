using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>Menu Sketch SDK > Settings: one page to fill AdMob ids, pacing, consent and the Sketch Server key.</summary>
    public sealed class SketchSdkSettingsWindow : EditorWindow
    {
        SketchSdkSettings _settings;
        UnityEditor.Editor _inspector;
        Vector2 _scroll;
        string _status = string.Empty;

        [MenuItem("Sketch SDK/Settings", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<SketchSdkSettingsWindow>("Sketch SDK");
            window.minSize = new Vector2(440, 520);
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
        static void OpenDocs() => Application.OpenURL("https://github.com/CodeSketch-dev/sketch-sdk#readme");

        void OnEnable() => _settings = SketchSdkEditorUtil.FindOrCreateSettings();

        void OnDisable()
        {
            if (_inspector != null) DestroyImmediate(_inspector);
        }

        void OnGUI()
        {
            if (_settings == null) _settings = SketchSdkEditorUtil.FindOrCreateSettings();
            EditorGUILayout.LabelField("Sketch SDK " + SketchSdkVersion.VERSION, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(_settings), EditorStyles.miniLabel);

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
                if (GUILayout.Button("Save & Verify", GUILayout.Height(28))) SaveAndVerify();
                if (GUILayout.Button("Select Asset", GUILayout.Height(28))) SelectAsset();
            }
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        void DrawStatus()
        {
            bool gma = SketchSdkEditorUtil.IsGoogleMobileAdsInstalled;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Google Mobile Ads", gma ? "installed" : "missing", GUILayout.ExpandWidth(true));
                if (!gma && GUILayout.Button("Install", GUILayout.Width(80))) SketchGmaInstaller.Install();
            }
            EditorGUILayout.LabelField(SketchSdkEditorUtil.ADMOB_DEFINE, SketchSdkEditorUtil.AdMobDefineActive ? "on (Android, iOS)" : "off");
            EditorGUILayout.LabelField("Firebase Analytics", SketchSdkEditorUtil.IsFirebaseAnalyticsInstalled ? "installed (events forwarded)" : "not installed (optional)");
            EditorGUILayout.LabelField("Sketch Server", _settings.HasServerCredentials ? "configured" : _settings.ServerEnabled ? "incomplete" : "off");

            List<SketchSdkEditorUtil.Issue> issues = SketchSdkEditorUtil.Validate(_settings, forRelease: false);
            foreach (var issue in issues) EditorGUILayout.HelpBox(issue.Text, issue.Type);
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
