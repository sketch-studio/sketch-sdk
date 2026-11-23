using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>
    /// Creates the game-owned Assets/SketchDev folder ONCE: SketchRemoteConfig.Dev.cs (the game's remote keys) and SketchDev.asmref
    /// (compiles the folder into SketchSdk.Runtime so the partial class can extend SketchRemoteConfig). Existing files are never
    /// touched, and the SDK installer never owns this folder, so SDK updates keep the game's keys.
    /// </summary>
    public static class SketchDevScaffold
    {
        public const string FOLDER = "Assets/SketchDev";
        public const string DEV_FILE = FOLDER + "/SketchRemoteConfig.Dev.cs";
        public const string ASMREF_FILE = FOLDER + "/SketchDev.asmref";

        const string ASMREF = "{\n    \"reference\": \"SketchSdk.Runtime\"\n}\n";

        const string TEMPLATE =
@"using System.Collections.Generic;

namespace Sketch.Sdk
{
    // Game-owned remote config keys. Created once by Sketch SDK; SDK updates never overwrite this file.
    // Every key added here shows in Sketch SDK > Manager > Remote Config (type, default, description, export, overrides).
    public static partial class SketchRemoteConfig
    {
        static partial void RegisterGameKeys(List<SketchRemoteKeyDef> keys)
        {
            // keys.Add(SketchRemoteKeyDef.Int(""start_lives"", 3, ""Lives at the start of a run.""));
        }

        // Typed accessor next to each key, read it anywhere in the game:
        // public static int StartLives => GetInt(""start_lives"", 3);
    }
}
";

        public static bool Exists => File.Exists(DEV_FILE);

        /// <summary>Writes the missing files only. Returns true when something was created.</summary>
        public static bool EnsureCreated()
        {
            bool created = false;
            if (!Directory.Exists(FOLDER)) Directory.CreateDirectory(FOLDER);
            if (!File.Exists(ASMREF_FILE)) { File.WriteAllText(ASMREF_FILE, ASMREF); created = true; }
            if (!File.Exists(DEV_FILE)) { File.WriteAllText(DEV_FILE, TEMPLATE); created = true; }
            if (created)
            {
                AssetDatabase.Refresh();
                Debug.Log("[SketchSdk] Created " + DEV_FILE + " (game remote keys, never overwritten by SDK updates).");
            }
            return created;
        }
    }
}
