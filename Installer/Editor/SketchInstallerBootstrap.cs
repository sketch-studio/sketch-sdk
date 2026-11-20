using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Installer
{
    /// <summary>Once per editor session: compare the newest vX.Y.Z tag with the installed stamp and offer Install / Update.</summary>
    [InitializeOnLoad]
    static class SketchInstallerBootstrap
    {
        static SketchInstallerBootstrap()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += OnEditorLoaded;
        }

        static void OnEditorLoaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SketchPostInstall.FinalizeWhenReady();
            if (SessionState.GetBool(SketchInstallerPaths.SESSION_CHECKED, false)) return;
            SessionState.SetBool(SketchInstallerPaths.SESSION_CHECKED, true);
            CheckForUpdates(false);
        }

        public static bool TryGetInstalled(out SketchSemVer installed)
        {
            return SketchSemVer.TryParse(SketchInstallManifest.Read(SketchInstallerPaths.MANIFEST_PATH).Stamp, out installed);
        }

        [MenuItem("Sketch SDK/Check For Updates", priority = 60)]
        static void CheckForUpdatesMenu() => CheckForUpdates(true);

        [MenuItem("Sketch SDK/Reinstall Current", priority = 61)]
        static void ReinstallCurrent()
        {
            if (!TryGetInstalled(out SketchSemVer installed))
            {
                EditorUtility.DisplayDialog("Sketch SDK", "No installer manifest at " + SketchInstallerPaths.MANIFEST_PATH + ". Use Check For Updates to install.", "OK");
                return;
            }
            if (EditorUtility.DisplayDialog("Sketch SDK", "Reinstall Sketch SDK " + installed.Tag + "?", "Reinstall", "Cancel"))
                SketchPackageInstaller.Install(installed);
        }

        [MenuItem("Sketch SDK/Install Version...", priority = 62)]
        static void InstallVersion() => SketchVersionPickerWindow.Open();

        static void CheckForUpdates(bool interactive)
        {
            SketchTagFetcher.FetchAsync((tags, error) => OnTags(tags, error, interactive));
        }

        static void OnTags(List<SketchSemVer> tags, string error, bool interactive)
        {
            if (error != null || tags.Count == 0)
            {
                string message = "Could not read Sketch SDK versions: " + (error ?? "no vX.Y.Z tags found");
                if (interactive) EditorUtility.DisplayDialog("Sketch SDK", message, "OK");
                else Debug.LogWarning("[SketchSdkInstaller] " + message);
                return;
            }
            SketchSemVer latest = tags[0];
            bool hasInstalled = TryGetInstalled(out SketchSemVer installed);
            if (hasInstalled && installed.CompareTo(latest) >= 0)
            {
                if (interactive) EditorUtility.DisplayDialog("Sketch SDK", "Sketch SDK " + installed.Tag + " is up to date.", "OK");
                return;
            }
            if (!interactive && SessionState.GetString(SketchInstallerPaths.SESSION_DECLINED, string.Empty) == latest.Tag) return;

            string action = hasInstalled ? "Update" : "Install";
            string body = hasInstalled
                ? "Update Sketch SDK " + installed.Tag + " -> " + latest.Tag + "?\n\nLocally edited SDK files are listed and backed up first."
                : "Install Sketch SDK " + latest.Tag + " into " + SketchInstallerPaths.INSTALL_ROOT + "?";
            if (EditorUtility.DisplayDialog("Sketch SDK", body, action, "Later"))
                SketchPackageInstaller.Install(latest);
            else
                SessionState.SetString(SketchInstallerPaths.SESSION_DECLINED, latest.Tag);
        }
    }
}
