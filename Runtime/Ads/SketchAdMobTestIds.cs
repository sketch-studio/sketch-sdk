namespace Sketch.Sdk
{
    /// <summary>Google's public AdMob demo ad units (developers.google.com/admob/*/test-ads). Safe to click; never earn.</summary>
    public static class SketchAdMobTestIds
    {
        public const string ANDROID_APP_ID = "ca-app-pub-3940256099942544~3347511713";
        public const string IOS_APP_ID = "ca-app-pub-3940256099942544~1458002511";

        public static string Banner(bool ios) => ios ? "ca-app-pub-3940256099942544/2435281174" : "ca-app-pub-3940256099942544/9214589741";
        public static string Interstitial(bool ios) => ios ? "ca-app-pub-3940256099942544/4411468910" : "ca-app-pub-3940256099942544/1033173712";
        public static string Rewarded(bool ios) => ios ? "ca-app-pub-3940256099942544/1712485313" : "ca-app-pub-3940256099942544/5224354917";
        public static string AppOpen(bool ios) => ios ? "ca-app-pub-3940256099942544/5575463023" : "ca-app-pub-3940256099942544/9257395921";
        public static string Native(bool ios) => ios ? "ca-app-pub-3940256099942544/3986624511" : "ca-app-pub-3940256099942544/2247696110";
        public static string RewardedInterstitial(bool ios) => ios ? "ca-app-pub-3940256099942544/6978759866" : "ca-app-pub-3940256099942544/5354046379";

        public static bool IsTestId(string id) => !string.IsNullOrEmpty(id) && id.StartsWith("ca-app-pub-3940256099942544");
    }
}
