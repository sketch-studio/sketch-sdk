using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Sketch.Sdk.Editor
{
    /// <summary>Reads the project (assemblies, files, build scenes, catalogs) into SketchSetupFacts for the checklist.</summary>
    public static class SketchSetupScanner
    {
        public const string MANIFEST_PATH = "Assets/Plugins/Android/AndroidManifest.xml";
        public const string IAP_JSON_PATH = "Assets/Resources/IAPProductCatalog.json";
        public const string FIREBASE_ANDROIDLIB_PATH = "Assets/Plugins/Android/FirebaseApp.androidlib";

        /// <summary>Every call SketchMediationPrivacy can make, checked against the installed adapters (types/methods).</summary>
        static void ScanMediationPrivacy(SketchSetupFacts facts)
        {
            var calls = new List<SketchMediationPrivacyCall>(SketchMediationPrivacy.Plan(true, true, true));
            foreach (SketchMediationPrivacyCall call in calls)
            {
                Type type = Type.GetType(call.TypeName, false);
                if (type == null) continue;
                Type[] args = call.Args.Select(a => a.GetType()).ToArray();
                bool found = type.GetMethod(call.Method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, args, null) != null;
                if (!found) facts.MediationPrivacyMissing.Add(call.Network + "." + call.Method);
                else if (!facts.MediationPrivacyNetworks.Contains(call.Network)) facts.MediationPrivacyNetworks.Add(call.Network);
            }
        }

        public static SketchSetupFacts Scan(SketchSdkSettings settings)
        {
            var assemblies = new HashSet<string>(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name));
            var facts = new SketchSetupFacts
            {
                SettingsLoadableAtRuntime = SketchSdkEditorUtil.IsLoadableAtRuntime(settings),
                GoogleMobileAdsInstalled = SketchSdkEditorUtil.IsGoogleMobileAdsInstalled,
                UmpInstalled = SketchSdkEditorUtil.IsUmpInstalled,
                AdMobDefineActive = SketchSdkEditorUtil.AdMobDefineActive,
                LevelPlayInstalled = assemblies.Contains("SketchSdk.LevelPlay"),
                MaxInstalled = assemblies.Contains("SketchSdk.Max"),
                LiftoffInstalled = assemblies.Contains("SketchSdk.Liftoff"),
                FirebaseAnalyticsInstalled = assemblies.Contains("Firebase.Analytics"),
                FirebaseRemoteConfigInstalled = assemblies.Contains("Firebase.RemoteConfig"),
                UnityIapInstalled = assemblies.Contains("UnityEngine.Purchasing") || assemblies.Contains("Unity.Purchasing"),
                IapProductCatalogJson = File.Exists(IAP_JSON_PATH),
                GoogleServicesJson = FindGoogleServicesJson() != null,
                FirebaseAndroidLib = Directory.Exists(FIREBASE_ANDROIDLIB_PATH),
                AndroidManifestXml = File.Exists(MANIFEST_PATH) ? File.ReadAllText(MANIFEST_PATH) : null,
                EnabledScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            };
            ScanMediationPrivacy(facts);
            foreach (SketchIapCatalog catalog in FindCatalogs())
            {
                facts.SketchIapCatalogs++;
                facts.SketchIapProducts += catalog.Products.Count;
                var errors = new List<string>();
                if (!catalog.Validate(errors)) facts.CatalogErrors.AddRange(errors.Select(e => catalog.name + ": " + e));
            }
            return facts;
        }

        public static List<SketchIapCatalog> FindCatalogs() =>
            AssetDatabase.FindAssets("t:" + nameof(SketchIapCatalog))
                .Select(g => AssetDatabase.LoadAssetAtPath<SketchIapCatalog>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).ToList();

        /// <summary>Project path of google-services.json under Assets (any folder), or null.</summary>
        public static string FindGoogleServicesJson()
        {
            foreach (string guid in AssetDatabase.FindAssets("google-services"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).Equals("google-services.json", StringComparison.OrdinalIgnoreCase)) return path;
            }
            return null;
        }
    }
}
