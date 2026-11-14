using Sketch.Sdk;
using UnityEngine;

namespace Sketch.Sdk.Samples
{
    /// <summary>
    /// Developer test panel (IMGUI, Editor and development builds only): tap the small "SDK" button top-left to open.
    /// Lets you fire every ad format, toggle remove-ads, open the privacy form and read SDK state on a device.
    /// </summary>
    public sealed class SketchSdkTestPanel : MonoBehaviour
    {
        bool _open;
        string _last = "";

        void Awake()
        {
#if !(UNITY_EDITOR || DEVELOPMENT_BUILD)
            Destroy(this);
#endif
        }

        void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.dpi / 160f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            if (!_open)
            {
                // IMGUI works with both the legacy Input Manager and the Input System package.
                if (GUI.Button(new Rect(4, 4, 44, 28), "SDK")) _open = true;
                return;
            }
            float w = Screen.width / scale - 20f;
            GUILayout.BeginArea(new Rect(10, 10, w, Screen.height / scale - 20f), GUI.skin.box);
            GUILayout.Label($"Sketch SDK {SketchSdkVersion.VERSION}  ready={SketchSdkApp.IsReady}  session={SketchIdentity.Session}");
            GUILayout.Label($"rewarded={SketchAds.IsRewardedReady} inter={SketchAds.IsInterstitialReady} appOpen={SketchAds.IsAppOpenReady} native={SketchAds.IsNativeReady}");
            GUILayout.Label($"removeAds={SketchAds.IsAdsRemoved} banner={SketchAds.BannerHeightPixels}px config={SketchRemoteConfig.ConfigId}");
            GUILayout.Label($"consent: canRequest={SketchConsent.CanRequestAds} privacyRequired={SketchConsent.IsPrivacyOptionsRequired}");
            GUILayout.Label($"server queue={(SketchServerClient.Current != null ? SketchServerClient.Current.PendingCount.ToString() : "off")}  last: {_last}");
            if (GUILayout.Button("Rewarded")) SketchAds.ShowRewarded("test_panel", ok => _last = "rewarded " + ok);
            if (GUILayout.Button("Interstitial (skip cooldown)")) SketchAds.ShowInterstitial("test_panel", () => _last = "inter closed", skipCooldown: true);
            if (GUILayout.Button("Interstitial (pacing: " + (SketchAds.Policy.CanShowInterstitial("test_panel", SketchIdentity.NowUnix) ? "ok" : SketchAds.Policy.LastBlockReason) + ")"))
                SketchAds.ShowInterstitial("test_panel", () => _last = "inter closed");
            if (GUILayout.Button("App open")) SketchAds.ShowAppOpen("test_panel", () => _last = "app open closed", coldStart: false);
            if (GUILayout.Button(SketchAds.IsBannerWanted ? "Hide banner" : "Show banner"))
            {
                if (SketchAds.IsBannerWanted) SketchAds.HideBanner(); else SketchAds.ShowBanner();
            }
            if (GUILayout.Button("Remove ads: " + SketchAds.IsAdsRemoved)) SketchAds.SetAdsRemoved(!SketchAds.IsAdsRemoved);
            if (GUILayout.Button("Privacy options")) SketchConsent.ShowPrivacyOptions(() => _last = "privacy closed");
            if (GUILayout.Button("Reset consent (next launch)")) SketchConsent.ResetForTesting();
            if (GUILayout.Button("Log test event")) SketchAnalytics.Log("test_panel_event", "time", Time.realtimeSinceStartup);
            if (GUILayout.Button("Close")) _open = false;
            GUILayout.EndArea();
        }
    }
}
