using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Installer
{
    /// <summary>
    /// Copies a tagged SDK payload into Assets/SketchSdk. Only files listed in the install manifest are owned:
    /// those may be replaced or deleted; everything else (game files, settings, the generated splash scene) is left alone.
    /// Locally edited owned files are listed and backed up before they are overwritten.
    /// </summary>
    static class SketchPackageInstaller
    {
        public static bool IsBusy { get; private set; }

        public static bool Install(SketchSemVer version)
        {
            if (IsBusy) return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Sketch SDK", "Leave Play Mode before installing the SDK.", "OK");
                return false;
            }
            IsBusy = true;
            string tag = version.Tag;
            string workDir = Path.Combine(Path.GetFullPath("Temp"), "SketchSdkInstall_" + tag + "_" + DateTime.Now.Ticks);
            bool refreshBlocked = false;
            try
            {
                string payloadRoot = SketchPayloadSource.Fetch(tag, workDir);
                ValidatePayload(payloadRoot);

                EditorUtility.DisplayProgressBar("Sketch SDK", "Comparing files...", 0.6f);
                Dictionary<string, string> incoming = CollectPayload(payloadRoot);
                SketchInstallManifest old = SketchInstallManifest.Read(SketchInstallerPaths.MANIFEST_PATH);
                List<string> removed = CollectRemoved(old, incoming);
                List<string> conflicts = CollectConflicts(old, incoming, removed);

                if (conflicts.Count > 0 && !ConfirmOverwrite(conflicts)) return false;

                AssetDatabase.DisallowAutoRefresh();
                refreshBlocked = true;
                if (conflicts.Count > 0) Backup(conflicts);

                EditorUtility.DisplayProgressBar("Sketch SDK", "Copying " + tag + "...", 0.75f);
                var manifest = new SketchInstallManifest { Stamp = tag };
                foreach (KeyValuePair<string, string> file in incoming)
                {
                    string source = Path.Combine(payloadRoot, file.Key);
                    string target = Path.Combine(SketchInstallerPaths.INSTALL_ROOT, file.Key);
                    if (!File.Exists(target) || SketchInstallManifest.Sha1(target) != file.Value)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.Copy(source, target, true);
                    }
                    manifest.Files[file.Key] = file.Value;
                }
                DeleteRemoved(removed);
                // Written last: a failure above leaves the old stamp, so the next check offers the install again.
                manifest.Write(SketchInstallerPaths.MANIFEST_PATH);

                SketchPostInstall.RemoveLegacyUpmEntry();
                SessionState.SetBool(SketchInstallerPaths.SESSION_PENDING_FINALIZE, true);
                Debug.Log("[SketchSdkInstaller] Installed Sketch SDK " + tag + " (" + incoming.Count + " files, " + removed.Count + " removed, " + conflicts.Count + " backed up).");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[SketchSdkInstaller] Install of " + tag + " failed: " + e.Message);
                EditorUtility.DisplayDialog("Sketch SDK", "Install of " + tag + " failed:\n" + e.Message + "\n\nThe install manifest was not updated; run it again when fixed.", "OK");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                TryDeleteDirectory(workDir);
                if (refreshBlocked)
                {
                    AssetDatabase.AllowAutoRefresh();
                    AssetDatabase.Refresh();
                    SketchPostInstall.ResolvePackages();
                }
                IsBusy = false;
            }
        }

        static void ValidatePayload(string root)
        {
            string packageJson = Path.Combine(root, "package.json");
            if (!File.Exists(packageJson) || File.ReadAllText(packageJson).IndexOf("\"" + SketchInstallerPaths.UPM_PACKAGE_NAME + "\"", StringComparison.Ordinal) < 0)
                throw new IOException("payload has no " + SketchInstallerPaths.UPM_PACKAGE_NAME + " package.json");
            if (!Directory.Exists(Path.Combine(root, "Runtime"))) throw new IOException("payload has no Runtime folder");
        }

        /// <summary>relative path -> sha1 of every payload file, including the .meta of each top-level entry.</summary>
        static Dictionary<string, string> CollectPayload(string root)
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string dir in SketchInstallerPaths.PAYLOAD_DIRECTORIES)
            {
                string full = Path.Combine(root, dir);
                if (!Directory.Exists(full)) continue;
                AddIfExists(root, dir + ".meta", files);
                foreach (string file in Directory.GetFiles(full, "*", SearchOption.AllDirectories))
                {
                    string rel = Relative(root, file);
                    if (IsIgnored(rel)) continue;
                    files[rel] = SketchInstallManifest.Sha1(file);
                }
            }
            foreach (string name in SketchInstallerPaths.PAYLOAD_FILES)
            {
                AddIfExists(root, name, files);
                AddIfExists(root, name + ".meta", files);
            }
            return files;
        }

        static void AddIfExists(string root, string rel, Dictionary<string, string> files)
        {
            string full = Path.Combine(root, rel);
            if (File.Exists(full) && !IsIgnored(rel)) files[rel] = SketchInstallManifest.Sha1(full);
        }

        static List<string> CollectRemoved(SketchInstallManifest old, Dictionary<string, string> incoming)
        {
            var removed = new List<string>();
            foreach (string rel in old.Files.Keys)
                if (!incoming.ContainsKey(rel) && !IsProtected(rel)) removed.Add(rel);
            return removed;
        }

        /// <summary>Files on disk that would be overwritten or deleted although they differ from what the SDK installed.</summary>
        static List<string> CollectConflicts(SketchInstallManifest old, Dictionary<string, string> incoming, List<string> removed)
        {
            var conflicts = new List<string>();
            void Check(string rel, string incomingHash)
            {
                string target = Path.Combine(SketchInstallerPaths.INSTALL_ROOT, rel);
                if (!File.Exists(target)) return;
                string current = SketchInstallManifest.Sha1(target);
                if (current == incomingHash) return;
                if (old.Files.TryGetValue(rel, out string installed) && installed == current) return;
                conflicts.Add(rel);
            }
            foreach (KeyValuePair<string, string> file in incoming) Check(file.Key, file.Value);
            foreach (string rel in removed) Check(rel, null);
            conflicts.Sort(StringComparer.Ordinal);
            return conflicts;
        }

        static bool ConfirmOverwrite(List<string> conflicts)
        {
            var sb = new StringBuilder();
            sb.Append(conflicts.Count).Append(" SDK file(s) differ from what the installer wrote and will be replaced.\nA copy goes to ")
              .Append(SketchInstallerPaths.BACKUP_ROOT).Append("/<timestamp>/ first.\n\n");
            int shown = Math.Min(conflicts.Count, 15);
            for (int i = 0; i < shown; i++) sb.Append("- ").Append(conflicts[i]).Append('\n');
            if (conflicts.Count > shown) sb.Append("... and ").Append(conflicts.Count - shown).Append(" more (see Console)\n");
            Debug.LogWarning("[SketchSdkInstaller] Locally edited SDK files:\n" + string.Join("\n", conflicts));
            return EditorUtility.DisplayDialog("Sketch SDK: local edits", sb.ToString(), "Back up and update", "Cancel");
        }

        static void Backup(List<string> conflicts)
        {
            string dir = Path.Combine(SketchInstallerPaths.BACKUP_ROOT, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            foreach (string rel in conflicts)
            {
                string source = Path.Combine(SketchInstallerPaths.INSTALL_ROOT, rel);
                string target = Path.Combine(dir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, true);
            }
            Debug.Log("[SketchSdkInstaller] Backed up " + conflicts.Count + " edited file(s) to " + dir);
        }

        static void DeleteRemoved(List<string> removed)
        {
            foreach (string rel in removed)
            {
                string target = Path.Combine(SketchInstallerPaths.INSTALL_ROOT, rel);
                if (File.Exists(target)) File.Delete(target);
            }
            // A removed folder .meta means the folder left the SDK; drop it only when nothing else lives in it.
            foreach (string rel in removed)
            {
                if (!rel.EndsWith(".meta", StringComparison.Ordinal)) continue;
                string dir = Path.Combine(SketchInstallerPaths.INSTALL_ROOT, rel.Substring(0, rel.Length - 5));
                DeleteEmptyTree(dir);
            }
        }

        static bool DeleteEmptyTree(string dir)
        {
            if (!Directory.Exists(dir)) return true;
            bool empty = true;
            foreach (string sub in Directory.GetDirectories(dir))
            {
                if (DeleteEmptyTree(sub))
                {
                    string meta = sub + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                }
                else empty = false;
            }
            if (Directory.GetFiles(dir).Length > 0) empty = false;
            if (empty) Directory.Delete(dir);
            return empty;
        }

        /// <summary>Never owned, never written or deleted: game settings and the splash hook.</summary>
        public static bool IsProtected(string rel)
        {
            if (rel.IndexOf("Resources/", StringComparison.Ordinal) < 0) return false;
            string name = Path.GetFileName(rel);
            return name.StartsWith("SketchSdkSettings", StringComparison.Ordinal)
                || name.StartsWith("SketchSplashHook", StringComparison.Ordinal);
        }

        static bool IsIgnored(string rel)
        {
            if (IsProtected(rel)) return true;
            foreach (string segment in rel.Split('/'))
                if (segment.Length == 0 || segment[0] == '.' || segment.EndsWith("~", StringComparison.Ordinal)) return true;
            return false;
        }

        static string Relative(string root, string file)
        {
            return file.Substring(root.Length).Replace('\\', '/').TrimStart('/');
        }

        static void TryDeleteDirectory(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch (Exception e) { Debug.LogWarning("[SketchSdkInstaller] Could not delete " + dir + ": " + e.Message); }
        }
    }
}
