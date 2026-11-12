using System;

namespace Sketch.Sdk
{
    public interface ISketchAdsProvider
    {
        void Initialize(Action<bool> onInitialized);
        bool IsReady(SketchAdFormat format);
        void ShowRewarded(Action<bool> onComplete);
        void ShowInterstitial(Action onClosed);
        void ShowAppOpen(Action onClosed);
    }
}
