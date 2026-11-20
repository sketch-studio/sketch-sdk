using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>
    /// Default SDK-internal store (same as the SDK identity / remote config cache keys). Game save data never goes here;
    /// a game that wants everything in its own save system assigns its own <see cref="ISketchKeyValueStore"/>.
    /// </summary>
    public sealed class SketchPlayerPrefsStore : ISketchKeyValueStore
    {
        public int GetInt(string key, int fallback = 0) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public string GetString(string key, string fallback = "") => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value ?? string.Empty);
        public void Save() => PlayerPrefs.Save();
    }
}
