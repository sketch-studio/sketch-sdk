using System;
using System.Collections.Generic;

namespace Sketch.Sdk.Editor
{
    public enum SketchCheckStatus { Ok, Info, Warning, Error }

    /// <summary>One row of the Manager checklist: area (tab), label, status and a one-line fix hint.</summary>
    public struct SketchCheckItem
    {
        public string Area;
        public string Label;
        public SketchCheckStatus Status;
        public string Hint;

        public SketchCheckItem(string area, string label, SketchCheckStatus status, string hint)
        {
            Area = area; Label = label; Status = status; Hint = hint;
        }
    }

    /// <summary>Project facts the checklist needs. Filled by SketchSetupScanner in the Editor, by hand in tests.</summary>
    public sealed class SketchSetupFacts
    {
        public bool SettingsLoadableAtRuntime;
        public bool GoogleMobileAdsInstalled, UmpInstalled, AdMobDefineActive;
        public bool LevelPlayInstalled, MaxInstalled, LiftoffInstalled;
        public bool FirebaseAnalyticsInstalled, FirebaseRemoteConfigInstalled, UnityIapInstalled;
        public bool IapProductCatalogJson, GoogleServicesJson, FirebaseAndroidLib;
        public int SketchIapCatalogs, SketchIapProducts;
        public List<string> CatalogErrors = new List<string>();
        /// <summary>Text of Assets/Plugins/Android/AndroidManifest.xml, null when the file is missing.</summary>
        public string AndroidManifestXml;
        public string[] EnabledScenes = Array.Empty<string>();
        /// <summary>Installed mediation adapters (network names) whose privacy API SketchMediationPrivacy calls.</summary>
        public List<string> MediationPrivacyNetworks = new List<string>();
        /// <summary>Calls whose adapter is installed but the method is missing (API drift): "Network.Method".</summary>
        public List<string> MediationPrivacyMissing = new List<string>();
        /// <summary>Installed AdMob mediation adapters (Android Dependencies.xml) reviewed for privacy forwarding.</summary>
        public List<SketchMediationAdapterReview> MediationAdapters = new List<SketchMediationAdapterReview>();
    }

    /// <summary>Pure setup checklist (no AssetDatabase): settings + facts -> rows with OK / Info / Warning / Error.</summary>
    public static class SketchSetupChecklist
    {
        public const string AREA_GENERAL = "General", AREA_ADS = "Ads", AREA_IAP = "IAP", AREA_FIREBASE = "Analytics / Firebase",
            AREA_SPLASH = "Splash", AREA_SERVER = "Server", AREA_CONSENT = "Consent";

        public static List<SketchCheckItem> Build(SketchSdkSettings s, SketchSetupFacts f)
        {
            var items = new List<SketchCheckItem>();
            f = f ?? new SketchSetupFacts();
            if (s == null)
            {
                items.Add(new SketchCheckItem(AREA_GENERAL, "Settings asset", SketchCheckStatus.Error, "Missing. Open Sketch SDK > Manager to create it."));
                return items;
            }

            items.Add(f.SettingsLoadableAtRuntime
                ? Ok(AREA_GENERAL, "Settings asset", "Loaded from Resources at runtime.")
                : new SketchCheckItem(AREA_GENERAL, "Settings asset", SketchCheckStatus.Error, "Must be Assets/SketchSdk/Resources/SketchSdkSettings.asset or the game runs without ids."));

            if (s.AdsBackend == SketchAdsBackend.AdMob)
            {
                items.Add(f.GoogleMobileAdsInstalled && f.UmpInstalled
                ? Ok(AREA_ADS, "Google Mobile Ads plugin", "Installed (with UMP).")
                : new SketchCheckItem(AREA_ADS, "Google Mobile Ads plugin", SketchCheckStatus.Error, "Not installed: no real ads on device. Menu Sketch SDK > Install Google Mobile Ads."));
                if (f.GoogleMobileAdsInstalled)
                    items.Add(f.AdMobDefineActive
                    ? Ok(AREA_ADS, "SKETCHSDK_ADMOB define", "Set for Android and iOS.")
                    : new SketchCheckItem(AREA_ADS, "SKETCHSDK_ADMOB define", SketchCheckStatus.Warning, "Not set. Press Save & Verify."));
                CheckPlatform(items, "Android", s.Android);
                CheckPlatform(items, "iOS", s.Ios);
                items.Add(s.ForceTestAds
                ? new SketchCheckItem(AREA_ADS, "Test ads off in release", SketchCheckStatus.Error, "Force Test Ads is ON: a release build earns nothing. Turn it off.")
                : Ok(AREA_ADS, "Test ads off in release", s.TestAdsInDevelopmentBuild ? "Release uses real ids; Editor and dev builds use Google test ads." : "Real ids everywhere (dev builds too: careful with your AdMob account)."));

            }
            else
            {
                bool installed = s.AdsBackend switch
                {
                    SketchAdsBackend.LevelPlay => f.LevelPlayInstalled,
                    SketchAdsBackend.Max => f.MaxInstalled,
                    SketchAdsBackend.Liftoff => f.LiftoffInstalled,
                    _ => false
                };
                string label = s.AdsBackend + " provider";
                items.Add(installed
                    ? Ok(AREA_ADS, label, "Vendor adapter compiled.")
                    : new SketchCheckItem(AREA_ADS, label, SketchCheckStatus.Error, "Install the selected provider's Unity package."));
            }

            // ---------------- IAP ----------------
            bool sellsSomething = f.SketchIapProducts > 0;
            if (f.SketchIapCatalogs == 0)
                items.Add(new SketchCheckItem(AREA_IAP, "SketchIapCatalog", SketchCheckStatus.Info, "No catalog asset (game sells nothing). Create > Sketch SDK > IAP Catalog."));
            else if (f.CatalogErrors.Count > 0)
                items.Add(new SketchCheckItem(AREA_IAP, "SketchIapCatalog", SketchCheckStatus.Error, string.Join(" ", f.CatalogErrors)));
            else if (!sellsSomething)
                items.Add(new SketchCheckItem(AREA_IAP, "SketchIapCatalog", SketchCheckStatus.Warning, "Catalog has no products."));
            else
                items.Add(Ok(AREA_IAP, "SketchIapCatalog", f.SketchIapProducts + " product(s), ids valid."));
            if (sellsSomething)
            {
                items.Add(f.UnityIapInstalled
                    ? Ok(AREA_IAP, "Unity IAP package", "com.unity.purchasing installed.")
                    : new SketchCheckItem(AREA_IAP, "Unity IAP package", SketchCheckStatus.Error, "Install com.unity.purchasing 5.x: without it purchases only work in the Editor fake store."));
                items.Add(f.IapProductCatalogJson
                    ? Ok(AREA_IAP, "IAPProductCatalog.json", "Assets/Resources/IAPProductCatalog.json present.")
                    : new SketchCheckItem(AREA_IAP, "IAPProductCatalog.json", SketchCheckStatus.Warning, "Missing Assets/Resources/IAPProductCatalog.json (Unity IAP catalog)."));
                items.Add(ManifestHasBilling(f.AndroidManifestXml)
                    ? Ok(AREA_IAP, "BILLING permission", "com.android.vending.BILLING in the Android manifest.")
                    : new SketchCheckItem(AREA_IAP, "BILLING permission", SketchCheckStatus.Error, f.AndroidManifestXml == null
                        ? "No Assets/Plugins/Android/AndroidManifest.xml with com.android.vending.BILLING."
                        : "Add <uses-permission android:name=\"com.android.vending.BILLING\" /> to Assets/Plugins/Android/AndroidManifest.xml."));
            }

            // ---------------- Firebase ----------------
            if (s.FirebaseAnalytics)
            {
                items.Add(f.FirebaseAnalyticsInstalled
                    ? Ok(AREA_FIREBASE, "Firebase Analytics plugin", "Installed.")
                    : new SketchCheckItem(AREA_FIREBASE, "Firebase Analytics plugin", SketchCheckStatus.Warning, "Firebase Analytics is ON but the plugin is missing: events stay local."));
                items.Add(f.GoogleServicesJson
                    ? Ok(AREA_FIREBASE, "google-services.json", "Found in Assets.")
                    : new SketchCheckItem(AREA_FIREBASE, "google-services.json", SketchCheckStatus.Error, "Download it from the Firebase console and put it in Assets/."));
                items.Add(f.FirebaseAndroidLib
                    ? Ok(AREA_FIREBASE, "FirebaseApp.androidlib", "Generated in Assets/Plugins/Android.")
                    : new SketchCheckItem(AREA_FIREBASE, "FirebaseApp.androidlib", SketchCheckStatus.Warning, "Not generated yet: re-import google-services.json / resolve Android dependencies."));
            }
            else items.Add(new SketchCheckItem(AREA_FIREBASE, "Firebase Analytics", SketchCheckStatus.Info, "Off (events only go to the Sketch server / debug log)."));

            // ---------------- Splash ----------------
            items.Add(IsSplashFirst(f.EnabledScenes)
                ? Ok(AREA_SPLASH, "Splash at build index 0", f.EnabledScenes[0])
                : new SketchCheckItem(AREA_SPLASH, "Splash at build index 0", SketchCheckStatus.Warning, "Scene 0 is not the splash. Menu Sketch SDK > Build Splash Scene puts it first."));

            // ---------------- Server ----------------
            if (!s.ServerEnabled)
                items.Add(new SketchCheckItem(AREA_SERVER, "Sketch Server", SketchCheckStatus.Info, "Off (no server analytics / remote config)."));
            else if (!s.HasServerCredentials)
                items.Add(new SketchCheckItem(AREA_SERVER, "Sketch Server", SketchCheckStatus.Error, "Enabled but URL, App Id or API key is empty."));
            else if (s.ServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !s.ServerUrl.Contains("localhost") && !s.ServerUrl.Contains("127.0.0.1"))
                items.Add(new SketchCheckItem(AREA_SERVER, "Sketch Server", SketchCheckStatus.Warning, "URL is http://: Android blocks cleartext. Use https."));
            else
                items.Add(Ok(AREA_SERVER, "Sketch Server", "Enabled with URL, App Id and API key."));

            // ---------------- Consent ----------------
            if (s.AdsBackend != SketchAdsBackend.AdMob)
                items.Add(new SketchCheckItem(AREA_CONSENT, "Consent", SketchCheckStatus.Info, "Configure the consent flow for the selected provider."));
            else if (s.RequestConsent && f.GoogleMobileAdsInstalled && !f.UmpInstalled)
                items.Add(new SketchCheckItem(AREA_CONSENT, "Consent (UMP)", SketchCheckStatus.Warning, "Request Consent is ON but the UMP plugin is missing."));
            else if (s.ConsentDebugGeography != SketchConsentDebugGeography.Disabled)
                items.Add(new SketchCheckItem(AREA_CONSENT, "Consent (UMP)", SketchCheckStatus.Info, "Debug geography " + s.ConsentDebugGeography + " (development builds only)."));
            else
                items.Add(Ok(AREA_CONSENT, "Consent (UMP)", s.RequestConsent ? "Consent form asked before ads (EEA/UK/US states)." : "Consent not requested (only OK outside regulated regions)."));

            if (s.AdsBackend == SketchAdsBackend.AdMob)
            {
                items.Add(s.RequireAgeSignal
                    ? Ok(AREA_CONSENT, "Age signals", "Consent and ads wait until the game calls SketchAudience.SetBracket (the SDK shows no age popup); unknown age = minor.")
                    : new SketchCheckItem(AREA_CONSENT, "Age signals", SketchCheckStatus.Info, "Off. Turn on Require Age Signal when the Play target audience includes players under 18."));
                if (f.MediationPrivacyMissing.Count > 0)
                    items.Add(new SketchCheckItem(AREA_CONSENT, "Mediation privacy", SketchCheckStatus.Error,
                        "Adapter privacy API missing (adapter updated?): " + string.Join(", ", f.MediationPrivacyMissing) + ". Do-not-sell / mixed-audience signals would not reach it."));
                else if (f.MediationPrivacyNetworks.Count > 0)
                    items.Add(Ok(AREA_CONSENT, "Mediation privacy", "Privacy APIs found: " + string.Join(", ", f.MediationPrivacyNetworks) + ". Also add each network to AdMob's EU and US-state ad partner lists."));
                foreach (SketchMediationAdapterReview a in f.MediationAdapters)
                {
                    string label = "Adapter privacy: " + a.Network;
                    string detail = a.Version + " - " + a.Reason;
                    if (a.Status == SketchAdapterPrivacyStatus.Ok) items.Add(Ok(AREA_CONSENT, label, detail));
                    else items.Add(new SketchCheckItem(AREA_CONSENT, label,
                        a.Status == SketchAdapterPrivacyStatus.TooOld ? SketchCheckStatus.Error : SketchCheckStatus.Warning, detail));
                }
                if (string.IsNullOrWhiteSpace(s.PrivacyPolicyUrl))
                    items.Add(new SketchCheckItem(AREA_CONSENT, "Privacy policy URL", SketchCheckStatus.Error,
                        "Empty. Google Play and AdMob require a privacy policy for apps with ads; use the same URL as Play Console > App content."));
                string manifest = f.AndroidManifestXml ?? string.Empty;
                if (manifest.Contains("ACCESS_FINE_LOCATION") || manifest.Contains("ACCESS_COARSE_LOCATION") || manifest.Contains("ACCESS_BACKGROUND_LOCATION"))
                    items.Add(new SketchCheckItem(AREA_CONSENT, "Location permission", SketchCheckStatus.Warning,
                        "The Android manifest asks for location. Ads do not need it; remove it unless gameplay does, and declare it in Data safety."));
                if (s.RequireAgeSignal && f.FirebaseAnalyticsInstalled && s.FirebaseAnalytics && f.AndroidManifestXml != null &&
                    !manifest.Contains("google_analytics_default_ad_personalization"))
                    items.Add(new SketchCheckItem(AREA_CONSENT, "Firebase ad signals default", SketchCheckStatus.Warning,
                        "Players may be minors: add <meta-data> google_analytics_default_allow_ad_personalization_signals, google_analytics_default_ad_user_data and google_analytics_default_ad_personalization = false to the manifest <application>; the SDK re-grants them for adults."));
            }

            // ---------------- Remote defaults ----------------
            if (s.RemoteDefaults != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (SketchRemoteDefault d in s.RemoteDefaults)
                {
                    string key = d?.Key?.Trim();
                    if (string.IsNullOrEmpty(key)) { items.Add(new SketchCheckItem(AREA_GENERAL, "Remote default key", SketchCheckStatus.Warning, "A remote default has an empty key.")); continue; }
                    if (!seen.Add(key)) items.Add(new SketchCheckItem(AREA_GENERAL, "Remote default key", SketchCheckStatus.Warning, "Duplicate remote default '" + key + "'."));
                }
            }
            return items;
        }

        static SketchCheckItem Ok(string area, string label, string hint) => new SketchCheckItem(area, label, SketchCheckStatus.Ok, hint);

        static void CheckPlatform(List<SketchCheckItem> items, string name, SketchAdUnitIds ids)
        {
            ids = ids ?? new SketchAdUnitIds();
            var units = new[]
            {
                ("Banner", ids.Banner), ("Interstitial", ids.Interstitial), ("Rewarded", ids.Rewarded),
                ("App Open", ids.AppOpen), ("Native", ids.Native), ("Rewarded Interstitial", ids.RewardedInterstitial),
            };
            bool any = !string.IsNullOrWhiteSpace(ids.AppId);
            foreach (var unit in units) any |= !string.IsNullOrWhiteSpace(unit.Item2);
            if (!any)
            {
                items.Add(new SketchCheckItem(AREA_ADS, name + " ad ids", SketchCheckStatus.Info, "Not set up (fine for a game that does not ship on " + name + ")."));
                return;
            }

            string appId = ids.AppId?.Trim();
            if (string.IsNullOrEmpty(appId)) items.Add(new SketchCheckItem(AREA_ADS, name + " App ID", SketchCheckStatus.Error, "Empty: the app crashes on launch. Format ca-app-pub-XXXX~YYYY."));
            else if (!IsAppId(appId)) items.Add(new SketchCheckItem(AREA_ADS, name + " App ID", SketchCheckStatus.Error, "'" + appId + "' is not an App ID (needs '~', an ad unit uses '/')."));
            else if (SketchAdMobTestIds.IsTestId(appId)) items.Add(new SketchCheckItem(AREA_ADS, name + " App ID", SketchCheckStatus.Warning, "Google TEST App ID: replace with yours before release."));
            else items.Add(Ok(AREA_ADS, name + " App ID", appId));

            foreach (var unit in units)
            {
                string id = unit.Item2?.Trim();
                string label = name + " " + unit.Item1;
                if (string.IsNullOrEmpty(id)) items.Add(new SketchCheckItem(AREA_ADS, label, SketchCheckStatus.Warning, "Empty: this format stays off."));
                else if (!IsAdUnitId(id)) items.Add(new SketchCheckItem(AREA_ADS, label, SketchCheckStatus.Error, "'" + id + "' is not an ad unit id (ca-app-pub-XXXX/YYYY)."));
                else if (SketchAdMobTestIds.IsTestId(id)) items.Add(new SketchCheckItem(AREA_ADS, label, SketchCheckStatus.Warning, "Google TEST unit: release earns nothing."));
                else items.Add(Ok(AREA_ADS, label, id));
            }
        }

        public static bool IsAppId(string id) => !string.IsNullOrWhiteSpace(id) && id.Trim().StartsWith("ca-app-pub-", StringComparison.Ordinal) && id.Contains("~") && !id.Contains("/");
        public static bool IsAdUnitId(string id) => !string.IsNullOrWhiteSpace(id) && id.Trim().StartsWith("ca-app-pub-", StringComparison.Ordinal) && id.Contains("/") && !id.Contains("~");
        public static bool ManifestHasBilling(string xml) => !string.IsNullOrEmpty(xml) && xml.Contains("com.android.vending.BILLING");

        /// <summary>True when the first enabled build scene is a splash scene (file name contains "Splash").</summary>
        public static bool IsSplashFirst(string[] enabledScenes)
        {
            if (enabledScenes == null || enabledScenes.Length == 0 || string.IsNullOrEmpty(enabledScenes[0])) return false;
            string path = enabledScenes[0].Replace('\\', '/');
            string file = path.Substring(path.LastIndexOf('/') + 1);
            return file.IndexOf("Splash", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Keeps a short prefix (up to the first '_') and the last 4 chars visible; everything else becomes dots.</summary>
        public static string MaskSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret)) return string.Empty;
            if (secret.Length <= 8) return new string('•', secret.Length);
            int underscore = secret.IndexOf('_');
            string prefix = underscore > 0 && underscore <= 6 ? secret.Substring(0, underscore + 1) : string.Empty;
            return prefix + new string('•', 6) + secret.Substring(secret.Length - 4);
        }

        public static int Count(List<SketchCheckItem> items, SketchCheckStatus status)
        {
            int n = 0;
            foreach (SketchCheckItem item in items) if (item.Status == status) n++;
            return n;
        }
    }
}
