using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>Shared Editor helpers: settings asset, scripting defines, Google Mobile Ads detection and App ID sync.</summary>
    public static class SketchSdkEditorUtil
    {
        public const string ADMOB_DEFINE = "SKETCHSDK_ADMOB";
        public const string FIREBASE_DEFINE = "SKETCHSDK_FIREBASE";
        const string GMA_SETTINGS_TYPE = "GoogleMobileAds.Editor.GoogleMobileAdsSettings";

        static readonly NamedBuildTarget[] MobileTargets = { NamedBuildTarget.Android, NamedBuildTarget.iOS };

        public static SketchSdkSettings FindSettings()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SketchSdkSettings>(SketchSdkSettings.DEFAULT_ASSET_PATH);
            if (asset != null) return asset;
            string guid = AssetDatabase.FindAssets("t:" + nameof(SketchSdkSettings)).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<SketchSdkSettings>(AssetDatabase.GUIDToAssetPath(guid));
        }

        public static SketchSdkSettings FindOrCreateSettings()
        {
            SketchSdkSettings settings = FindSettings();
            if (settings != null) return settings;
            string folder = Path.GetDirectoryName(SketchSdkSettings.DEFAULT_ASSET_PATH)?.Replace('\\', '/');
            EnsureFolder(folder);
            settings = ScriptableObject.CreateInstance<SketchSdkSettings>();
            AssetDatabase.CreateAsset(settings, SketchSdkSettings.DEFAULT_ASSET_PATH);
            AssetDatabase.SaveAssets();
            Debug.Log("[SketchSdk] Created " + SketchSdkSettings.DEFAULT_ASSET_PATH);
            return settings;
        }

        static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        // ---------------- Google Mobile Ads detection ----------------

        /// <summary>True when the Google Mobile Ads Unity plugin (UPM or .unitypackage) is in the project.</summary>
        public static bool IsGoogleMobileAdsInstalled =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "GoogleMobileAds") ||
            AssetDatabase.FindAssets("GoogleMobileAds t:DefaultAsset").Any(g => AssetDatabase.GUIDToAssetPath(g).EndsWith("/GoogleMobileAds.dll", StringComparison.OrdinalIgnoreCase));

        public static bool IsUmpInstalled =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "GoogleMobileAds.Ump");

        public static bool HasDefine(NamedBuildTarget target, string define) =>
            PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Select(s => s.Trim()).Contains(define);

        public static void SetDefine(NamedBuildTarget target, string define, bool enabled)
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbols(target).Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            bool has = symbols.Contains(define);
            if (has == enabled) return;
            if (enabled) symbols.Add(define);
            else symbols.RemoveAll(s => s == define);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", symbols));
            Debug.Log($"[SketchSdk] {(enabled ? "Added" : "Removed")} {define} for {target.TargetName}.");
        }

        /// <summary>SKETCHSDK_ADMOB on Android/iOS exactly when the plugin exists (prevents compile errors either way).</summary>
        public static void SyncAdMobDefine()
        {
            bool installed = IsGoogleMobileAdsInstalled && IsUmpInstalled;
            foreach (NamedBuildTarget target in MobileTargets) SetDefine(target, ADMOB_DEFINE, installed);
        }

        public static bool AdMobDefineActive => MobileTargets.All(t => HasDefine(t, ADMOB_DEFINE));

        /// <summary>Firebase Analytics imported as a .unitypackage (Assets/Firebase) has no UPM entry for the asmdef versionDefine;
        /// set SKETCHSDK_FIREBASE from the loaded assembly instead. UPM installs get the define from the asmdef as well.</summary>
        public static bool IsFirebaseAnalyticsInstalled =>
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Firebase.Analytics");

        public static void SyncFirebaseDefine()
        {
            bool installed = IsFirebaseAnalyticsInstalled;
            foreach (NamedBuildTarget target in MobileTargets) SetDefine(target, FIREBASE_DEFINE, installed);
        }

        /// <summary>The runtime loads Resources/SketchSdkSettings; any other location means "no settings" on device.</summary>
        public static bool IsLoadableAtRuntime(SketchSdkSettings settings)
        {
            if (settings == null) return false;
            string path = AssetDatabase.GetAssetPath(settings).Replace('\\', '/');
            return path.Contains("/Resources/") && Path.GetFileNameWithoutExtension(path) == SketchSdkSettings.RESOURCE_NAME &&
                   path.Substring(path.LastIndexOf("/Resources/", StringComparison.Ordinal) + 11) == SketchSdkSettings.RESOURCE_NAME + ".asset";
        }

        // ---------------- App ID sync into GoogleMobileAdsSettings ----------------

        public static ScriptableObject FindGmaSettings()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(GMA_SETTINGS_TYPE, false))
                .FirstOrDefault(t => t != null);
            if (type == null) return null;
            string guid = AssetDatabase.FindAssets("t:GoogleMobileAdsSettings").FirstOrDefault();
            if (guid != null) return AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
            // Let the plugin create its own asset (LoadInstance is internal/public depending on version).
            var load = type.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return load?.Invoke(null, null) as ScriptableObject;
        }

        /// <summary>Writes the App IDs (test App ID when blank) into the GMA settings. Returns a status line.</summary>
        public static string SyncAppIds(SketchSdkSettings settings)
        {
            if (settings == null) return "No SketchSdkSettings.";
            ScriptableObject gma = FindGmaSettings();
            if (gma == null) return "Google Mobile Ads settings not found (plugin missing).";
            string android = settings.Android.AppId?.Trim();
            string ios = settings.Ios.AppId?.Trim();
            if (string.IsNullOrEmpty(android)) android = SketchAdMobTestIds.ANDROID_APP_ID;
            if (string.IsNullOrEmpty(ios)) ios = SketchAdMobTestIds.IOS_APP_ID;
            var so = new SerializedObject(gma);
            bool changed = SetString(so, "adMobAndroidAppId", android) | SetString(so, "adMobIOSAppId", ios);
            if (changed)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(gma);
                AssetDatabase.SaveAssets();
            }
            return changed ? "App IDs written to Google Mobile Ads settings." : "App IDs already in sync.";
        }

        static bool SetString(SerializedObject so, string name, string value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property == null || property.stringValue == value) return false;
            property.stringValue = value;
            return true;
        }

        // ---------------- validation ----------------

        public struct Issue
        {
            public MessageType Type;
            public string Text;
            public Issue(MessageType type, string text) { Type = type; Text = text; }
        }

        public static List<Issue> Validate(SketchSdkSettings s, bool forRelease)
        {
            var issues = new List<Issue>();
            if (s == null) { issues.Add(new Issue(MessageType.Error, "SketchSdkSettings asset missing.")); return issues; }
            if (!IsLoadableAtRuntime(s))
                issues.Add(new Issue(MessageType.Error, $"Settings asset must be at a Resources root named {SketchSdkSettings.RESOURCE_NAME}.asset (now {AssetDatabase.GetAssetPath(s)}); the game would run without ids."));
            if (!IsGoogleMobileAdsInstalled)
                issues.Add(new Issue(MessageType.Warning, "Google Mobile Ads plugin not installed: no real ads on device (Editor uses fake ads)."));
            else if (!AdMobDefineActive)
                issues.Add(new Issue(MessageType.Warning, ADMOB_DEFINE + " is not set for Android/iOS."));
            CheckPlatform(issues, "Android", s.Android, forRelease);
            CheckPlatform(issues, "iOS", s.Ios, forRelease);
            if (forRelease && s.ForceTestAds)
                issues.Add(new Issue(MessageType.Warning, "Force Test Ads is ON: this build earns nothing."));
            if (s.ServerEnabled && !s.HasServerCredentials)
                issues.Add(new Issue(MessageType.Warning, "Sketch Server enabled but URL / App Id / API key missing."));
            if (s.ServerEnabled && s.ServerUrl != null && s.ServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !s.ServerUrl.Contains("localhost") && !s.ServerUrl.Contains("127.0.0.1"))
                issues.Add(new Issue(MessageType.Warning, "Server URL is http://; Android blocks cleartext traffic by default. Use https."));
            return issues;
        }

        static void CheckPlatform(List<Issue> issues, string name, SketchAdUnitIds ids, bool forRelease)
        {
            if (ids == null) return;
            bool anyUnit = !string.IsNullOrWhiteSpace(ids.Banner) || !string.IsNullOrWhiteSpace(ids.Interstitial) ||
                           !string.IsNullOrWhiteSpace(ids.Rewarded) || !string.IsNullOrWhiteSpace(ids.AppOpen) || !string.IsNullOrWhiteSpace(ids.Native) ||
                           !string.IsNullOrWhiteSpace(ids.RewardedInterstitial);
            if (!anyUnit) { issues.Add(new Issue(MessageType.Info, $"{name}: no ad unit ids (formats left blank stay off).")); return; }
            if (string.IsNullOrWhiteSpace(ids.AppId))
                issues.Add(new Issue(forRelease ? MessageType.Error : MessageType.Warning, $"{name}: AdMob App ID is empty (the app crashes on launch without it)."));
            else if (!ids.AppId.Trim().Contains("~"))
                issues.Add(new Issue(MessageType.Error, $"{name}: App ID must contain '~' (ca-app-pub-XXXX~YYYY); an ad unit id uses '/'."));
            foreach (var pair in new[] { ("Banner", ids.Banner), ("Interstitial", ids.Interstitial), ("Rewarded", ids.Rewarded), ("App open", ids.AppOpen), ("Native", ids.Native), ("Rewarded interstitial", ids.RewardedInterstitial) })
            {
                string id = pair.Item2?.Trim();
                if (string.IsNullOrEmpty(id)) continue;
                if (!id.StartsWith("ca-app-pub-") || !id.Contains("/"))
                    issues.Add(new Issue(MessageType.Error, $"{name} {pair.Item1}: '{id}' is not an ad unit id (ca-app-pub-XXXX/YYYY)."));
                else if (forRelease && SketchAdMobTestIds.IsTestId(id))
                    issues.Add(new Issue(MessageType.Warning, $"{name} {pair.Item1}: Google test id in a release build."));
            }
        }
    }
}
