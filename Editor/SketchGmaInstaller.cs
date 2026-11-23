using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>
    /// Adds Google Mobile Ads (and its External Dependency Manager) from OpenUPM:
    /// a scoped registry for com.google, then Package Manager resolves the latest com.google.ads.mobile.
    /// </summary>
    public static class SketchGmaInstaller
    {
        const string REGISTRY_NAME = "package.openupm.com";
        const string REGISTRY_URL = "https://package.openupm.com";
        const string GMA_PACKAGE = "com.google.ads.mobile";
        static AddRequest _request;
        static AddRequest _adapterAdd;
        static RemoveRequest _adapterRemove;
        static string _adapterName;

        internal static bool IsBusy => _request != null || _adapterAdd != null || _adapterRemove != null;

        internal static void ChangeMediationAdapter(string packageName, bool remove)
        {
            if (IsBusy || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (remove)
                {
                    if (!EditorUtility.DisplayDialog("Remove mediation adapter", "Remove " + packageName + " from this Unity project?", "Remove", "Cancel")) return;
                    _adapterRemove = Client.Remove(packageName);
                }
                else
                {
                    EnsureScopedRegistry();
                    _adapterAdd = Client.Add(packageName);
                }
                _adapterName = packageName;
                EditorApplication.update += PollAdapter;
            }
            catch (Exception error)
            {
                Debug.LogError("[SketchSdk] Mediation adapter request failed: " + error.Message);
            }
        }

        static void PollAdapter()
        {
            Request request = _adapterAdd != null ? (Request)_adapterAdd : _adapterRemove;
            if (request == null || !request.IsCompleted) return;
            EditorApplication.update -= PollAdapter;
            if (request.Status == StatusCode.Success)
                Debug.Log("[SketchSdk] " + (_adapterAdd != null ? "Installed " + _adapterAdd.Result.packageId : "Removed " + _adapterName));
            else
                Debug.LogError("[SketchSdk] " + _adapterName + ": " + request.Error?.message);
            _adapterAdd = null;
            _adapterRemove = null;
            foreach (var window in Resources.FindObjectsOfTypeAll<SketchSdkSettingsWindow>())
                window.Repaint();
        }

        public static void Install()
        {
            if (IsBusy) return;
            if (SketchSdkEditorUtil.IsGoogleMobileAdsInstalled)
            {
                EditorUtility.DisplayDialog("Sketch SDK", "Google Mobile Ads is already installed.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Sketch SDK",
                    "Add the OpenUPM registry (scope com.google) to Packages/manifest.json and install com.google.ads.mobile?",
                    "Install", "Cancel")) return;
            try { EnsureScopedRegistry(); }
            catch (Exception e)
            {
                Debug.LogError("[SketchSdk] Could not edit Packages/manifest.json: " + e.Message);
                return;
            }
            _request = Client.Add(GMA_PACKAGE);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null || !_request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (_request.Status == StatusCode.Success)
                Debug.Log($"[SketchSdk] Installed {_request.Result.packageId}. Open Sketch SDK > Settings and press Save & Verify.");
            else
                Debug.LogError("[SketchSdk] Google Mobile Ads install failed: " + _request.Error?.message +
                               " — or import the .unitypackage from github.com/googleads/googleads-mobile-unity/releases.");
            _request = null;
        }

        /// <summary>Adds { name, url, scopes:[com.google] } to scopedRegistries when missing (minimal, order-preserving JSON edit).</summary>
        static void EnsureScopedRegistry()
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Packages", "manifest.json");
            string json = File.ReadAllText(path);
            if (!(SketchJson.Parse(json) is Dictionary<string, object> root)) throw new FormatException("manifest.json is not an object");
            var registries = root.TryGetValue("scopedRegistries", out object raw) && raw is List<object> list ? list : new List<object>();
            foreach (object item in registries)
            {
                if (!(item is Dictionary<string, object> reg) || !SketchJson.TryGetString(reg, "url", out string url)) continue;
                if (!string.Equals(url.TrimEnd('/'), REGISTRY_URL, StringComparison.OrdinalIgnoreCase)) continue;
                var scopes = reg.TryGetValue("scopes", out object s) && s is List<object> l ? l : new List<object>();
                if (!scopes.Contains("com.google")) { scopes.Add("com.google"); reg["scopes"] = scopes; Write(path, root); }
                return;
            }
            registries.Add(new Dictionary<string, object>
            {
                ["name"] = REGISTRY_NAME,
                ["url"] = REGISTRY_URL,
                ["scopes"] = new List<object> { "com.google" },
            });
            root["scopedRegistries"] = registries;
            Write(path, root);
        }

        static void Write(string path, Dictionary<string, object> root)
        {
            File.WriteAllText(path, Pretty(SketchJson.Serialize(root)));
            Client.Resolve();
        }

        /// <summary>Indent compact JSON with 2 spaces so manifest.json stays readable in diffs.</summary>
        static string Pretty(string compact)
        {
            var b = new System.Text.StringBuilder(compact.Length * 2);
            int indent = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = 0; i < compact.Length; i++)
            {
                char c = compact[i];
                if (inString)
                {
                    b.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') { inString = true; b.Append(c); continue; }
                switch (c)
                {
                    case '{': case '[':
                        b.Append(c).Append('\n').Append(' ', ++indent * 2); break;
                    case '}': case ']':
                        b.Append('\n').Append(' ', --indent * 2).Append(c); break;
                    case ',':
                        b.Append(c).Append('\n').Append(' ', indent * 2); break;
                    case ':':
                        b.Append(": "); break;
                    default: b.Append(c); break;
                }
            }
            return b.Append('\n').ToString();
        }
    }
}
