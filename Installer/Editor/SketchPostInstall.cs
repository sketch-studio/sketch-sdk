using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Sketch.Sdk.Installer
{
    /// <summary>Project wiring after a copy: drop the old UPM install, resolve packages, put the splash scene at build index 0.</summary>
    static class SketchPostInstall
    {
        const string MANIFEST_JSON = "Packages/manifest.json";
        static readonly Regex LegacyEntry = new Regex("^[ \\t]*\"com\\.sketch\\.sdk\"[ \\t]*:[ \\t]*\"[^\"]*\"[ \\t]*,?[ \\t]*\\r?\\n", RegexOptions.Multiline);
        static readonly Regex TrailingComma = new Regex(",(\\s*\\})");

        /// <summary>Removes a `com.sketch.sdk` dependency (old UPM git install) so it does not clash with Assets/SketchSdk.</summary>
        public static void RemoveLegacyUpmEntry()
        {
            if (!File.Exists(MANIFEST_JSON)) return;
            string text = File.ReadAllText(MANIFEST_JSON);
            Match match = LegacyEntry.Match(text);
            if (!match.Success) return;
            string updated = TrailingComma.Replace(LegacyEntry.Replace(text, string.Empty, 1), "$1");
            File.WriteAllText(MANIFEST_JSON, updated);
            Debug.Log("[SketchSdkInstaller] Removed the old UPM entry " + match.Value.Trim() + " from " + MANIFEST_JSON + ".");
        }

        public static void ResolvePackages()
        {
            try { Client.Resolve(); }
            catch (Exception e) { Debug.LogWarning("[SketchSdkInstaller] Package resolve failed: " + e.Message); }
        }

        /// <summary>Runs on the first editor load after an install, once scripts compiled: build the splash scene if missing, then index 0.</summary>
        public static void FinalizeWhenReady()
        {
            if (!SessionState.GetBool(SketchInstallerPaths.SESSION_PENDING_FINALIZE, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += FinalizeWhenReady;
                return;
            }
            SessionState.SetBool(SketchInstallerPaths.SESSION_PENDING_FINALIZE, false);
            if (!File.Exists(SketchInstallerPaths.SPLASH_SCENE_PATH))
            {
                // The builder lives in the SDK (Splash/Editor); it also sets the build index.
                if (!EditorApplication.ExecuteMenuItem(SketchInstallerPaths.BUILD_SPLASH_MENU))
                    Debug.LogWarning("[SketchSdkInstaller] Menu '" + SketchInstallerPaths.BUILD_SPLASH_MENU + "' not found; build the splash scene by hand.");
            }
            EnsureSplashFirst(SketchInstallerPaths.SPLASH_SCENE_PATH);
        }

        /// <summary>Puts the scene at build index 0 and keeps every other scene in its order.</summary>
        public static bool EnsureSplashFirst(string scenePath)
        {
            if (!File.Exists(scenePath)) return false;
            EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
            if (current.Length > 0 && current[0].path == scenePath && current[0].enabled) return true;
            var scenes = new List<EditorBuildSettingsScene>(current.Length + 1) { new EditorBuildSettingsScene(scenePath, true) };
            foreach (EditorBuildSettingsScene scene in current)
                if (scene.path != scenePath) scenes.Add(scene);
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[SketchSdkInstaller] " + scenePath + " is now build index 0.");
            return true;
        }
    }
}
