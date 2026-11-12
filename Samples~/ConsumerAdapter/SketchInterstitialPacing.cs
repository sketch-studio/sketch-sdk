namespace Sketch.Sdk.Samples
{
    /// <summary>Example app-owned interstitial spacing rule; choose interval in the game's config.</summary>
    public static class SketchInterstitialPacing
    {
        public static bool Allowed(double now, double lastShownAt, double intervalSeconds) =>
            double.IsNegativeInfinity(lastShownAt) || intervalSeconds <= 0.0 || now - lastShownAt >= intervalSeconds;
    }
}
