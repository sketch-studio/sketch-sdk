using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Store page helper for rate-us buttons. The game owns the popup and when to show it.</summary>
    public static class SketchRating
    {
        public static string StoreUrl
        {
            get
            {
#if UNITY_IOS
                string id = SketchSdkApp.Settings != null ? SketchSdkApp.Settings.IosAppStoreId : string.Empty;
                return string.IsNullOrWhiteSpace(id) ? string.Empty : "itms-apps://itunes.apple.com/app/id" + id.Trim() + "?action=write-review";
#else
                return "market://details?id=" + Application.identifier;
#endif
            }
        }

        public static void OpenStorePage(string placement = "rate")
        {
            string url = StoreUrl;
            if (string.IsNullOrEmpty(url)) { SketchLog.Warn("Store URL unknown (set IosAppStoreId in SketchSdkSettings)."); return; }
            SketchAds.SuppressNextAppOpen();
            SketchAnalytics.Log("rate_open_store", "placement", placement ?? string.Empty);
#if UNITY_ANDROID && !UNITY_EDITOR
            try { Application.OpenURL(url); }
            catch { Application.OpenURL("https://play.google.com/store/apps/details?id=" + Application.identifier); }
#else
            Application.OpenURL(url);
#endif
        }
    }
}
