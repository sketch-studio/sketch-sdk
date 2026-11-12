namespace Sketch.Sdk
{
    public interface ISketchBannerAdsProvider
    {
        bool IsBannerReady { get; }
        float BannerHeightPixels { get; }
        void ShowBanner();
        void HideBanner();
    }
}
