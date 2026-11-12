namespace Sketch.Sdk
{
    public interface ISketchNativeAdsProvider
    {
        bool IsNativeReady { get; }
        bool ShowNativeInRect(SketchNativeRequest request, SketchNativeCallbacks callbacks);
        void HideNative();
    }
}
