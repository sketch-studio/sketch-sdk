namespace Sketch.Sdk
{
    public interface ISketchBannerAdsProvider
    {
        bool IsBannerReady { get; }
        /// <summary>Actual native height in physical pixels while loaded and shown; zero otherwise.</summary>
        float BannerHeightPixels { get; }
        void ShowBanner();
        void HideBanner();
    }
}
