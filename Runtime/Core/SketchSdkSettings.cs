using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>AdMob ids of one platform. Ad unit ids look like ca-app-pub-XXXX/YYYY, the app id like ca-app-pub-XXXX~ZZZZ.</summary>
    [Serializable]
    public sealed class SketchAdUnitIds
    {
        public string AppId = "";
        public string Banner = "";
        public string Interstitial = "";
        public string Rewarded = "";
        public string AppOpen = "";
        public string Native = "";
        public string RewardedInterstitial = "";

        public void Trim()
        {
            AppId = (AppId ?? "").Trim();
            Banner = (Banner ?? "").Trim();
            Interstitial = (Interstitial ?? "").Trim();
            Rewarded = (Rewarded ?? "").Trim();
            AppOpen = (AppOpen ?? "").Trim();
            Native = (Native ?? "").Trim();
            RewardedInterstitial = (RewardedInterstitial ?? "").Trim();
        }
    }

    [Serializable]
    public sealed class SketchRemoteDefault
    {
        public string Key = "";
        [Tooltip("Number, true/false, text or JSON")] public string Value = "";
    }

    public enum SketchConsentDebugGeography { Disabled = 0, EEA = 1, NotEEA = 2 }

    /// <summary>
    /// The one config asset of a game. Created by menu Sketch SDK > Settings at Assets/SketchSdk/Resources/SketchSdkSettings.asset.
    /// Keys stay in the game project; the package never ships ids.
    /// </summary>
    public sealed class SketchSdkSettings : ScriptableObject
    {
        public const string RESOURCE_NAME = "SketchSdkSettings";
        public const string DEFAULT_ASSET_PATH = "Assets/SketchSdk/Resources/SketchSdkSettings.asset";

        [Header("General")]
        [Tooltip("Initialize the SDK automatically before the first scene loads. Turn off to call SketchSdkApp.Initialize yourself.")]
        public bool AutoInitialize = true;
        [Tooltip("Extra [SketchSdk] logs in Editor and development builds.")]
        public bool VerboseLogs = true;

        [Header("AdMob")]
        public SketchAdUnitIds Android = new SketchAdUnitIds();
        public SketchAdUnitIds Ios = new SketchAdUnitIds();
        [Tooltip("Use Google's official test ad units in Editor and development builds (never risk your AdMob account while testing).")]
        public bool TestAdsInDevelopmentBuild = true;
        [Tooltip("Use Google's test ad units everywhere, also release builds. For internal test builds only.")]
        public bool ForceTestAds;
        [Tooltip("Hashed device ids printed by the Google Mobile Ads SDK in logcat ('Use RequestConfiguration.Builder().setTestDeviceIds').")]
        public List<string> TestDeviceIds = new List<string>();
        public SketchAdMobTag ChildDirected = SketchAdMobTag.Unspecified;
        public SketchAdMobTag UnderAgeOfConsent = SketchAdMobTag.Unspecified;
        public SketchAdMobContentRating MaxContentRating = SketchAdMobContentRating.General;
        [Tooltip("Seconds the native ad may take to attach before it is closed as LoadFailed (0 = no limit).")]
        public float NativeAttachTimeoutSeconds = 3f;

        [Header("Ad pacing (remote config keys override these, see README)")]
        public SketchAdsPolicySettings Pacing = new SketchAdsPolicySettings();

        [Header("Consent (Google UMP)")]
        [Tooltip("Ask GDPR/US-state consent with Google's User Messaging Platform before ads. Create the message in AdMob > Privacy & messaging.")]
        public bool RequestConsent = true;
        [Tooltip("Pretend the device is in this region (development builds only) to test the consent form.")]
        public SketchConsentDebugGeography ConsentDebugGeography = SketchConsentDebugGeography.Disabled;

        [Header("Sketch Server (analytics, remote config, revenue)")]
        public bool ServerEnabled;
        public string ServerUrl = "https://api.sketchapi.win/api/v1";
        [Tooltip("App id shown in the Sketch dashboard (Admin > Apps).")]
        public string ServerAppId = "";
        [Tooltip("App API key (sgk_...) shown once when the app was created.")]
        public string ServerApiKey = "";
        [Tooltip("Seconds between event uploads.")]
        public float ServerFlushIntervalSeconds = 15f;
        [Tooltip("Fetch GET /config on launch and stamp configId on events (A/B tests).")]
        public bool ServerRemoteConfig = true;
        public float RemoteConfigTimeoutSeconds = 5f;

        [Header("Firebase (optional, used when the Firebase Analytics package is installed)")]
        [Tooltip("Send every SketchAnalytics event to Firebase Analytics too.")]
        public bool FirebaseAnalytics = true;
        [Tooltip("Log ad_impression with value/currency on every AdMob paid event. Leave off when AdMob is linked to Firebase (it logs ad_impression itself).")]
        public bool FirebaseLogAdImpression;

        [Header("Remote config defaults (used until the server answers)")]
        public List<SketchRemoteDefault> RemoteDefaults = new List<SketchRemoteDefault>();

        [Header("Revenue milestones")]
        [Tooltip("Lifetime ad+IAP revenue (USD) thresholds that log reach_rev_<n> once each, e.g. for Google Ads tROAS.")]
        public List<float> RevenueMilestonesUsd = new List<float> { 0.01f, 0.05f, 0.1f, 0.5f, 1f };

        [Header("Store")]
        [Tooltip("iOS App Store numeric id, used by SketchRating.OpenStorePage on iOS.")]
        public string IosAppStoreId = "";

        public SketchAdUnitIds CurrentPlatformIds
        {
            get
            {
#if UNITY_IOS
                return Ios;
#else
                return Android;
#endif
            }
        }

        public bool UseTestAds
        {
            get
            {
                if (ForceTestAds) return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return TestAdsInDevelopmentBuild;
#else
                return false;
#endif
            }
        }

        public bool HasServerCredentials =>
            ServerEnabled && !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(ServerAppId) && !string.IsNullOrWhiteSpace(ServerApiKey);

        /// <summary>Ad unit ids for this platform, swapped for Google test units when UseTestAds.</summary>
        public SketchAdMobConfig BuildAdMobConfig()
        {
            SketchAdUnitIds ids = CurrentPlatformIds ?? new SketchAdUnitIds();
            ids.Trim();
            bool test = UseTestAds;
            bool ios = false;
#if UNITY_IOS
            ios = true;
#endif
            return new SketchAdMobConfig
            {
                BannerId = Pick(ids.Banner, test, SketchAdMobTestIds.Banner(ios)),
                InterstitialId = Pick(ids.Interstitial, test, SketchAdMobTestIds.Interstitial(ios)),
                RewardedId = Pick(ids.Rewarded, test, SketchAdMobTestIds.Rewarded(ios)),
                AppOpenId = Pick(ids.AppOpen, test, SketchAdMobTestIds.AppOpen(ios)),
                NativeId = Pick(ids.Native, test, SketchAdMobTestIds.Native(ios)),
                RewardedInterstitialId = Pick(ids.RewardedInterstitial, test, SketchAdMobTestIds.RewardedInterstitial(ios)),
                TestDeviceIds = TestDeviceIds != null ? TestDeviceIds.ToArray() : Array.Empty<string>(),
                ChildDirected = ChildDirected,
                UnderAgeOfConsent = UnderAgeOfConsent,
                MaxContentRating = MaxContentRating,
                NonPersonalized = false,
                NativeAttachTimeoutSeconds = NativeAttachTimeoutSeconds,
            };
        }

        /// <summary>Test mode keeps a format off when its real id is blank, so a game only loads the formats it uses.</summary>
        static string Pick(string real, bool test, string testId) =>
            string.IsNullOrWhiteSpace(real) ? string.Empty : test ? testId : real;

        static SketchSdkSettings _cached;

        /// <summary>Loads Resources/SketchSdkSettings (null when the game has not created it yet).</summary>
        public static SketchSdkSettings Load()
        {
            if (_cached == null) _cached = Resources.Load<SketchSdkSettings>(RESOURCE_NAME);
            return _cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _cached = null;
    }
}
