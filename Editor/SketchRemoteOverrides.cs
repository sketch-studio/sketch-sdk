using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>Editor-only remote config overrides, stored per project in EditorPrefs (read by SketchRemoteConfig.ApplyEditorOverrides).</summary>
    public static class SketchRemoteOverrides
    {
        static string PrefKey => SketchRemoteConfig.EDITOR_OVERRIDES_PREF + Application.dataPath;

        public static Dictionary<string, string> Load() => SketchRemoteExport.ParseOverrides(EditorPrefs.GetString(PrefKey, string.Empty));

        public static void Save(Dictionary<string, string> overrides)
        {
            if (overrides == null || overrides.Count == 0) EditorPrefs.DeleteKey(PrefKey);
            else EditorPrefs.SetString(PrefKey, SketchRemoteExport.OverridesToJson(overrides));
        }
    }
}
