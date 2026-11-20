namespace Sketch.Sdk
{
    /// <summary>
    /// Durable remove-ads flag. The SDK default is PlayerPrefs (backward compatible); a game with its own save system
    /// supplies an implementation backed by that save and registers it with SketchAds.UseAdsRemovedStore.
    /// </summary>
    public interface ISketchAdsRemovedStore
    {
        bool Load();
        /// <summary>Must be durable when it returns (write and flush the game save).</summary>
        void Save(bool removed);
    }
}
