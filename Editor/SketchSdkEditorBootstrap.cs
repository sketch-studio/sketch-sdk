using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>On every domain reload: create the settings asset once and keep SKETCHSDK_ADMOB in step with the plugin.</summary>
    [InitializeOnLoad]
    static class SketchSdkEditorBootstrap
    {
        const string FIRST_RUN_KEY = "SketchSdk.FirstRunShown";

        static SketchSdkEditorBootstrap()
        {
            // Batch mode (CI) may quit before delayCall runs: sync the define right away so the player scripts compile with it.
            if (Application.isBatchMode)
            {
                try { SketchSdkEditorUtil.SyncAdMobDefine(); SketchSdkEditorUtil.SyncFirebaseDefine(); }
                catch (System.Exception e) { Debug.LogWarning("[SketchSdk] Define sync failed: " + e.Message); }
                return;
            }
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SketchSdkEditorUtil.SyncAdMobDefine();
                SketchSdkEditorUtil.SyncFirebaseDefine();
                bool created = SketchSdkEditorUtil.FindSettings() == null;
                SketchSdkSettings settings = SketchSdkEditorUtil.FindOrCreateSettings();
                if (created || !EditorPrefs.GetBool(FIRST_RUN_KEY + "." + Application.dataPath.GetHashCode(), false))
                {
                    EditorPrefs.SetBool(FIRST_RUN_KEY + "." + Application.dataPath.GetHashCode(), true);
                    SketchSdkSettingsWindow.Open();
                }
                if (settings != null && SketchSdkEditorUtil.IsGoogleMobileAdsInstalled) SketchSdkEditorUtil.SyncAppIds(settings);
            };
        }
    }

    /// <summary>Build gate: syncs the App ID and stops a release build that would crash or ship broken ids.</summary>
    sealed class SketchSdkBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target != BuildTarget.Android && target != BuildTarget.iOS) return;
            SketchSdkEditorUtil.SyncAdMobDefine();
            SketchSdkEditorUtil.SyncFirebaseDefine();
            SketchSdkSettings settings = SketchSdkEditorUtil.FindOrCreateSettings();
            if (!SketchSdkEditorUtil.IsLoadableAtRuntime(settings))
                throw new BuildFailedException($"[SketchSdk] Settings asset is at {AssetDatabase.GetAssetPath(settings)}; move it to {SketchSdkSettings.DEFAULT_ASSET_PATH} (runtime loads Resources/{SketchSdkSettings.RESOURCE_NAME}).");
            if (SketchSdkEditorUtil.IsGoogleMobileAdsInstalled) Debug.Log("[SketchSdk] " + SketchSdkEditorUtil.SyncAppIds(settings));

            bool release = (report.summary.options & BuildOptions.Development) == 0;
            var issues = SketchSdkEditorUtil.Validate(settings, release);
            string platform = target == BuildTarget.iOS ? "iOS" : "Android";
            foreach (var issue in issues)
            {
                if (issue.Type == MessageType.Error) Debug.LogError("[SketchSdk] " + issue.Text);
                else if (issue.Type == MessageType.Warning) Debug.LogWarning("[SketchSdk] " + issue.Text);
            }
            var blocking = issues.Where(i => i.Type == MessageType.Error && (i.Text.StartsWith(platform + ":") || i.Text.StartsWith(platform + " "))).ToList();
            if (release && blocking.Count > 0)
                throw new BuildFailedException("[SketchSdk] " + string.Join(" | ", blocking.Select(i => i.Text)) + " Fix in Sketch SDK > Settings.");
        }
    }
}
