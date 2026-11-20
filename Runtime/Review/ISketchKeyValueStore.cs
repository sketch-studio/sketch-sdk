namespace Sketch.Sdk
{
    /// <summary>
    /// Small key/value persistence for SDK retention state (review gate, notification permission asked).
    /// The game may supply its own (e.g. DataBlock-backed); the default is <see cref="SketchPlayerPrefsStore"/>.
    /// </summary>
    public interface ISketchKeyValueStore
    {
        int GetInt(string key, int fallback = 0);
        void SetInt(string key, int value);
        string GetString(string key, string fallback = "");
        void SetString(string key, string value);
        void Save();
    }
}
