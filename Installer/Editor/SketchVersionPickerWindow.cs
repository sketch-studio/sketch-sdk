using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Installer
{
    /// <summary>Lists every vX.Y.Z tag; one click installs that version (up- or downgrade).</summary>
    sealed class SketchVersionPickerWindow : EditorWindow
    {
        List<SketchSemVer> _tags;
        string _error;
        bool _loading;
        Vector2 _scroll;

        public static void Open()
        {
            var window = GetWindow<SketchVersionPickerWindow>(true, "Sketch SDK Versions");
            window.minSize = new Vector2(280, 320);
            window.Reload();
        }

        void Reload()
        {
            _loading = true;
            _error = null;
            SketchTagFetcher.FetchAsync((tags, error) =>
            {
                _loading = false;
                _tags = tags;
                _error = error;
                Repaint();
            });
        }

        void OnGUI()
        {
            bool hasInstalled = SketchInstallerBootstrap.TryGetInstalled(out SketchSemVer installed);
            EditorGUILayout.LabelField("Installed", hasInstalled ? installed.Tag : "none");
            EditorGUILayout.Space();
            if (_loading) { EditorGUILayout.HelpBox("Reading tags...", MessageType.Info); return; }
            if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (GUILayout.Button("Refresh")) Reload();
            if (_tags == null) return;

            using (new EditorGUI.DisabledScope(SketchPackageInstaller.IsBusy))
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                foreach (SketchSemVer tag in _tags)
                {
                    bool current = hasInstalled && tag.CompareTo(installed) == 0;
                    string label = tag.Tag + (current ? "  (installed)" : string.Empty);
                    if (GUILayout.Button(label) &&
                        EditorUtility.DisplayDialog("Sketch SDK", (current ? "Reinstall " : "Install ") + tag.Tag + "?", "Install", "Cancel"))
                    {
                        SketchPackageInstaller.Install(tag);
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
