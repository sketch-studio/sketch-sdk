#if SKETCHSDK_FIREBASE_RC
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Optional Firebase Remote Config provider, compiled only when the Firebase Remote Config package is installed.
    /// Fetches and activates once per launch, then feeds every key into SketchRemoteConfig (server answer still wins,
    /// SketchSdkSettings defaults stay underneath). Missing google-services.json = warning, defaults keep working.
    /// </summary>
    public static class SketchFirebaseRemoteConfig
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Application.isEditor && !Application.isPlaying) return;
            SketchFirebaseDependencies.Once(FirebaseApp.CheckAndFixDependenciesAsync).ContinueWithOnMainThread(dependencies =>
            {
                if (dependencies.IsFaulted || dependencies.IsCanceled || dependencies.Result != DependencyStatus.Available)
                {
                    SketchLog.Warn("Firebase Remote Config unavailable (google-services.json missing?); defaults in use.");
                    return;
                }
                var rc = FirebaseRemoteConfig.DefaultInstance;
                rc.FetchAndActivateAsync().ContinueWithOnMainThread(fetch =>
                {
                    if (fetch.IsFaulted || fetch.IsCanceled) { SketchLog.Warn("Firebase Remote Config fetch failed; cached/default values in use."); }
                    Apply(rc);
                });
            });
        }

        static void Apply(FirebaseRemoteConfig rc)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in rc.AllValues)
            {
                string raw = pair.Value.StringValue ?? string.Empty;
                // Typed like server values so GetInt/GetBool/GetDouble behave the same; JSON stays parsed.
                string trimmed = raw.Trim();
                if (trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[') && SketchJson.TryParse(trimmed, out object parsed)) values[pair.Key] = parsed;
                else values[pair.Key] = raw;
            }
            SketchRemoteConfig.ApplyProviderValues(values, "Firebase");
        }
    }
}
#endif
