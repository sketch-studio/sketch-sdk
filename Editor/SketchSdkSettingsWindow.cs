using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Sketch.Sdk.Editor
{
    /// <summary>
    /// Sketch SDK Manager: one window, one tab per area (Bounce Manager style). Every field edits the settings asset through
    /// SerializedObject (Undo, dirty), the Overview tab shows a green/yellow/red setup checklist with a fix hint per row.
    /// </summary>
    public sealed class SketchSdkSettingsWindow : EditorWindow
    {
        /// <summary>Every serialized settings field the window draws (a test keeps it complete).</summary>
        public static readonly string[] DRAWN_FIELDS =
        {
            "AutoInitialize", "VerboseLogs", "Android", "Ios", "TestAdsInDevelopmentBuild", "ForceTestAds", "TestDeviceIds",
            "AdsBackend", "LevelPlayAndroid", "LevelPlayIos", "MaxAndroid", "MaxIos", "LiftoffAndroid", "LiftoffIos",
            "IosTrackingUsageDescription", "PrivacyPolicyUrl",
            "ChildDirected", "UnderAgeOfConsent", "MaxContentRating", "NativeAttachTimeoutSeconds", "NativeCloseDelaySeconds", "Pacing",
            "Pacing.InterstitialStartDelaySeconds", "Pacing.InterstitialCooldownSeconds", "Pacing.InterstitialMinSession",
            "Pacing.AdsOffVersions", "Pacing.RemoveAdsBlocks", "Pacing.Placements", "Pacing.BannerStartDelaySeconds",
            "Pacing.AppOpenOnResume", "Pacing.AppOpenOnColdStart", "Pacing.AppOpenStartDelaySeconds", "Pacing.AppOpenMinBackgroundSeconds",
            "Pacing.AppOpenAfterFullscreenSeconds", "RequestConsent", "ConsentDebugGeography", "ServerEnabled", "ServerUrl", "DevServerUrl", "ServerAppId",
            "ServerApiKey", "ServerFlushIntervalSeconds", "ServerRemoteConfig", "RemoteConfigTimeoutSeconds", "FirebaseAnalytics",
            "FirebaseLogAdImpression", "RemoteDefaults", "RevenueMilestonesUsd", "IosAppStoreId", "BuildVersionCode",
        };

        public const string DASHBOARD_URL = "https://dash.sketchapi.win";
        const string ADMOB_URL = "https://apps.admob.com";
        const string FIREBASE_URL = "https://console.firebase.google.com";

        enum Tab { Overview, Ads, Iap, Analytics, RemoteConfig, Server, Splash, Consent }
        static readonly string[] TabNames = { "Overview", "Ads", "IAP", "Analytics", "Remote Config", "Server", "Splash", "Consent" };
        static readonly string[] TabAreas =
        {
            null, SketchSetupChecklist.AREA_ADS, SketchSetupChecklist.AREA_IAP, SketchSetupChecklist.AREA_FIREBASE, null,
            SketchSetupChecklist.AREA_SERVER, SketchSetupChecklist.AREA_SPLASH, SketchSetupChecklist.AREA_CONSENT,
        };

        [SerializeField] int _tab;
        [SerializeField] int _platform;
        [SerializeField] Vector2 _scroll;
        [SerializeField] string _keyFilter = string.Empty;

        SketchSdkSettings _settings;
        SerializedObject _so;
        List<SketchCheckItem> _checks = new List<SketchCheckItem>();
        SketchSetupFacts _facts = new SketchSetupFacts();
        List<SketchIapCatalog> _catalogs = new List<SketchIapCatalog>();
        bool _showApiKey;
        string _status = string.Empty;
        UnityEditor.Editor _catalogEditor;
        UnityEditor.Editor _hookEditor;
        Dictionary<string, string> _overrides;
        List<SketchRemoteKeyDef> _keys;

        // ---------------- menus ----------------

        [MenuItem("Sketch SDK/Manager", priority = 0)]
        public static void Open() => OpenTab(-1);

        [MenuItem("Sketch SDK/Settings", priority = 1)]
        static void OpenSettings() => OpenTab(-1);

        /// <summary>Opens the Manager on a tab (0 Overview .. 7 Consent); -1 keeps the last tab.</summary>
        public static SketchSdkSettingsWindow OpenTab(int tab)
        {
            var window = GetWindow<SketchSdkSettingsWindow>(false, "Sketch SDK");
            window.minSize = new Vector2(700, 620);
            if (tab >= 0 && tab < TabNames.Length) window._tab = tab;
            window.Refresh();
            window.Show();
            window.Repaint();
            return window;
        }

        [MenuItem("Sketch SDK/Select Settings Asset", priority = 2)]
        static void SelectAsset()
        {
            SketchSdkSettings settings = SketchSdkEditorUtil.FindOrCreateSettings();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Sketch SDK/Install Google Mobile Ads", priority = 20)]
        static void InstallGma() => SketchGmaInstaller.Install();

        [MenuItem("Sketch SDK/Documentation", priority = 40)]
        static void OpenDocs() => Application.OpenURL("https://github.com/sketch-studio/sketch-sdk#documentation");

        // ---------------- lifecycle ----------------

        void OnEnable()
        {
            titleContent = new GUIContent("Sketch SDK");
            Refresh();
        }

        void OnFocus() => Refresh();

        void OnDisable()
        {
            if (_catalogEditor != null) DestroyImmediate(_catalogEditor);
            if (_hookEditor != null) DestroyImmediate(_hookEditor);
        }

        void OnDestroy()
        {
            if (_settings != null && EditorUtility.IsDirty(_settings)) AssetDatabase.SaveAssetIfDirty(_settings);
        }

        void Refresh()
        {
            _settings = SketchSdkEditorUtil.FindOrCreateSettings();
            _so = _settings != null ? new SerializedObject(_settings) : null;
            _facts = SketchSetupScanner.Scan(_settings);
            _catalogs = SketchSetupScanner.FindCatalogs();
            _checks = SketchSetupChecklist.Build(_settings, _facts);
            _keys = SketchRemoteKeyCatalog.Build(_settings);
            _overrides = SketchRemoteOverrides.Load();
        }

        // ---------------- layout ----------------

        void OnGUI()
        {
            if (_settings == null || _so == null || _so.targetObject == null) Refresh();
            if (_so == null) { EditorGUILayout.HelpBox("Sketch SDK settings asset could not be created.", MessageType.Error); return; }
            _so.Update();

            DrawHeader();
            DrawTabs();

            float savedLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.36f, 190f, 280f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(6);
            switch ((Tab)_tab)
            {
                case Tab.Overview: DrawOverview(); break;
                case Tab.Ads: DrawAds(); break;
                case Tab.Iap: DrawIap(); break;
                case Tab.Analytics: DrawAnalytics(); break;
                case Tab.RemoteConfig: DrawRemoteConfig(); break;
                case Tab.Server: DrawServer(); break;
                case Tab.Splash: DrawSplash(); break;
                case Tab.Consent: DrawConsent(); break;
            }
            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();
            EditorGUIUtility.labelWidth = savedLabel;

            if (_so.ApplyModifiedProperties()) { _checks = SketchSetupChecklist.Build(_settings, _facts); _keys = SketchRemoteKeyCatalog.Build(_settings); }
            DrawFooter();
        }

        void DrawHeader()
        {
            var bar = EditorGUILayout.BeginHorizontal(SketchEditorStyles.HeaderBar);
            EditorGUI.DrawRect(bar, SketchEditorStyles.HeaderColor);
            GUILayout.Label("Sketch SDK Manager", SketchEditorStyles.Title);
            GUILayout.Label("v" + SketchSdkVersion.VERSION, SketchEditorStyles.VersionPill, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();
            int errors = SketchSetupChecklist.Count(_checks, SketchCheckStatus.Error);
            int warnings = SketchSetupChecklist.Count(_checks, SketchCheckStatus.Warning);
            if (errors == 0 && warnings == 0) SketchEditorStyles.Badge(SketchCheckStatus.Ok, "READY", 70);
            if (errors > 0) SketchEditorStyles.Badge(SketchCheckStatus.Error, errors + (errors == 1 ? " ERROR" : " ERRORS"), 86);
            if (warnings > 0) SketchEditorStyles.Badge(SketchCheckStatus.Warning, warnings + (warnings == 1 ? " WARNING" : " WARNINGS"), 104);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            SketchEditorStyles.PathLink("Settings", AssetDatabase.GetAssetPath(_settings));
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        void DrawTabs()
        {
            var labels = new GUIContent[TabNames.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                SketchCheckStatus worst = WorstStatus(TabAreas[i]);
                string mark = TabAreas[i] == null ? string.Empty : worst == SketchCheckStatus.Error ? "  ●" : worst == SketchCheckStatus.Warning ? "  ○" : string.Empty;
                labels[i] = new GUIContent(TabNames[i] + mark, worst == SketchCheckStatus.Error ? "This tab has errors" : worst == SketchCheckStatus.Warning ? "This tab has warnings" : TabNames[i]);
            }
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            int picked = GUILayout.Toolbar(_tab, labels, GUILayout.Height(26));
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            if (picked != _tab) { _tab = picked; _scroll = Vector2.zero; GUI.FocusControl(null); }
        }

        void DrawFooter()
        {
            SketchEditorStyles.Separator();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            if (GUILayout.Button(new GUIContent("Save & Verify", "Save the asset, sync defines and App IDs, re-run every check."), SketchEditorStyles.PrimaryButton, GUILayout.Width(150), GUILayout.Height(28))) SaveAndVerify();
            if (GUILayout.Button("Select Asset", GUILayout.Width(100), GUILayout.Height(28))) SelectAsset();
            if (GUILayout.Button("Refresh", GUILayout.Width(80), GUILayout.Height(28))) Refresh();
            GUILayout.Label(_status, SketchEditorStyles.WrapMini, GUILayout.Height(28));
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6);
        }

        SketchCheckStatus WorstStatus(string area)
        {
            SketchCheckStatus worst = SketchCheckStatus.Ok;
            if (area == null) return worst;
            foreach (SketchCheckItem item in _checks)
                if (item.Area == area && item.Status > worst) worst = item.Status;
            return worst;
        }

        // ---------------- tabs ----------------

        void DrawOverview()
        {
            if (SketchEditorStyles.Section("ov_sdk", "SDK"))
            {
                SketchEditorStyles.InfoRow("Version", SketchSdkVersion.VERSION);
                SketchEditorStyles.InfoRow("Unity", Application.unityVersion);
                SketchEditorStyles.InfoRow("Build target", EditorUserBuildSettings.activeBuildTarget.ToString());
                SketchEditorStyles.InfoRow(SketchSdkEditorUtil.ADMOB_DEFINE, SketchSdkEditorUtil.AdMobDefineActive ? "on (Android + iOS)" : "off");
                SketchEditorStyles.InfoRow(SketchSdkEditorUtil.FIREBASE_DEFINE, SketchSdkEditorUtil.IsFirebaseAnalyticsInstalled ? "on (plugin found)" : "off");
                GUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("README", GUILayout.Width(90))) Ping("Assets/SketchSdk/README.md");
                if (GUILayout.Button("CHANGELOG", GUILayout.Width(100))) Ping("Assets/SketchSdk/CHANGELOG.md");
                if (GUILayout.Button("Sketch dashboard", GUILayout.Width(130))) Application.OpenURL(DASHBOARD_URL);
                if (GUILayout.Button("AdMob", GUILayout.Width(70))) Application.OpenURL(ADMOB_URL);
                if (GUILayout.Button("Firebase", GUILayout.Width(80))) Application.OpenURL(FIREBASE_URL);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                SketchEditorStyles.EndSection();
            }

            int ok = SketchSetupChecklist.Count(_checks, SketchCheckStatus.Ok);
            int info = SketchSetupChecklist.Count(_checks, SketchCheckStatus.Info);
            int warn = SketchSetupChecklist.Count(_checks, SketchCheckStatus.Warning);
            int err = SketchSetupChecklist.Count(_checks, SketchCheckStatus.Error);
            if (SketchEditorStyles.Section("ov_checks", $"Setup checklist   {ok} OK  ·  {info} info  ·  {warn} warnings  ·  {err} errors"))
            {
                string area = null;
                foreach (SketchCheckItem item in _checks)
                {
                    if (item.Area != area)
                    {
                        area = item.Area;
                        GUILayout.Space(area == _checks[0].Area ? 0 : 6);
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label(area, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        int tab = Array.IndexOf(TabAreas, area);
                        if (tab > 0 && GUILayout.Button("Open tab", EditorStyles.miniButton, GUILayout.Width(70))) { _tab = tab; _scroll = Vector2.zero; }
                        EditorGUILayout.EndHorizontal();
                    }
                    SketchEditorStyles.CheckRow(item);
                }
                SketchEditorStyles.EndSection();
            }
        }

        void DrawAds()
        {
            DrawChecks(SketchSetupChecklist.AREA_ADS, "ads_status");

            if (SketchEditorStyles.Section("ads_provider", "Ads provider"))
            {
                Field("AdsBackend", "Provider", "Choose which SDK serves ads in this game.");
                _platform = GUILayout.Toolbar(_platform, new[] { "Android", "iOS" }, GUILayout.Height(22));
                string platformSuffix = _platform == 0 ? "Android" : "Ios";
                switch ((SketchAdsBackend)_so.FindProperty("AdsBackend").intValue)
                {
                    case SketchAdsBackend.LevelPlay:
                        EditorGUILayout.PropertyField(_so.FindProperty("LevelPlay" + platformSuffix), true);
                        break;
                    case SketchAdsBackend.Max:
                        SketchEditorStyles.Help("Set your SDK key in AppLovin Integration Manager.");
                        EditorGUILayout.PropertyField(_so.FindProperty("Max" + platformSuffix), true);
                        break;
                    case SketchAdsBackend.Liftoff:
                        string root = "Liftoff" + platformSuffix;
                        Field(root + ".AppId", "App ID", "Liftoff Monetize application ID.");
                        Field(root + ".Banner", "Banner placement", "Bottom banner placement ID.");
                        Field(root + ".Interstitial", "Interstitial placement", "Interstitial placement ID.");
                        Field(root + ".Rewarded", "Rewarded placement", "Rewarded placement ID.");
                        break;
                }
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("ads_ids", "AdMob ids"))
            {
                SketchEditorStyles.Help("App ID has '~' (ca-app-pub-XXXX~YYYY). Ad unit ids have '/' (ca-app-pub-XXXX/YYYY). Leave a format empty to turn it off.");
                _platform = GUILayout.Toolbar(_platform, new[] { "Android", "iOS" }, GUILayout.Height(22));
                GUILayout.Space(4);
                string root = _platform == 0 ? "Android" : "Ios";
                IdField(root + ".AppId", "App ID", true, "From AdMob > Apps > App settings. Written into Google Mobile Ads settings on Save.");
                IdField(root + ".Banner", "Banner", false, "Bottom banner.");
                IdField(root + ".Interstitial", "Interstitial", false, "Fullscreen ad between levels.");
                IdField(root + ".Rewarded", "Rewarded", false, "Watch an ad for a reward. Never blocked by remove-ads.");
                IdField(root + ".AppOpen", "App Open", false, "Shown on cold start (splash) and on resume.");
                IdField(root + ".Native", "Native", false, "Native ad inside a popup rect.");
                IdField(root + ".RewardedInterstitial", "Rewarded Interstitial", false, "Fullscreen reward ad; needs an intro screen with opt-out.");
                GUILayout.Space(2);
                if (GUILayout.Button("Open AdMob console", GUILayout.Width(150))) Application.OpenURL(ADMOB_URL);
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("ads_mediation", "AdMob mediation adapters"))
            {
                SketchEditorStyles.Help("Install the networks used by this game. Updating an adapter can update shared dependencies such as Google Mobile Ads.");
                var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                DrawMediationAdapter(packages, "Liftoff Monetize", "liftoffmonetize");
                DrawMediationAdapter(packages, "Unity Ads", "unityads");
                DrawMediationAdapter(packages, "Mintegral", "mintegral");
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("ads_test", "Test ads"))
            {
                Field("TestAdsInDevelopmentBuild", "Test ads in dev builds", "Editor and development builds use Google's test units (safe for your AdMob account). Release always uses real ids.");
                Field("ForceTestAds", "Force test ads everywhere", "Also in release. Internal test builds ONLY: a release with this on earns nothing.");
                if (_settings.ForceTestAds) EditorGUILayout.HelpBox("Force Test Ads is ON. Turn it off before a store build.", MessageType.Error);
                Field("TestDeviceIds", "Test device ids", "Hashed ids printed in logcat by the Google Mobile Ads SDK; these phones get test ads.");
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("ads_inter", "Interstitial pacing"))
            {
                Field("Pacing.InterstitialStartDelaySeconds", "Start delay (s)", "No interstitial until this many seconds after the FIRST app open (install).", SketchAdsRemoteKeys.INTER_START_DELAY);
                Field("Pacing.InterstitialCooldownSeconds", "Cooldown (s)", "Minimum seconds between an interstitial and the previous fullscreen ad.", SketchAdsRemoteKeys.INTER_COOLDOWN);
                Field("Pacing.InterstitialMinSession", "From session", "Session number from which interstitials may show (1 = first session).", SketchAdsRemoteKeys.INTER_MIN_SESSION);
                Field("Pacing.Placements", "Placement rules", "Per placement: on/off, from session, own cooldown (-1 = global).", SketchAdsRemoteKeys.PLACEMENTS);
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("ads_banner", "Banner and app open"))
            {
                Field("Pacing.BannerStartDelaySeconds", "Banner start delay (s)", "Banner stays hidden until this many seconds after the first app open.", SketchAdsRemoteKeys.BANNER_START_DELAY);
                Field("Pacing.AppOpenOnColdStart", "App open on cold start", "Show an app-open ad once at launch (splash waits for it).", SketchAdsRemoteKeys.APP_OPEN_COLD_START);
                Field("Pacing.AppOpenOnResume", "App open on resume", "Show an app-open ad when the player comes back to the app.", SketchAdsRemoteKeys.APP_OPEN_RESUME);
                Field("Pacing.AppOpenStartDelaySeconds", "App open start delay (s)", "No app-open ad until this many seconds after the first app open.", SketchAdsRemoteKeys.APP_OPEN_START_DELAY);
                Field("Pacing.AppOpenMinBackgroundSeconds", "Min background time (s)", "The app must stay in background this long before a resume ad.", SketchAdsRemoteKeys.APP_OPEN_MIN_BACKGROUND);
                Field("Pacing.AppOpenAfterFullscreenSeconds", "Quiet after fullscreen (s)", "No app-open ad this many seconds after any fullscreen ad closes.", SketchAdsRemoteKeys.APP_OPEN_AFTER_FULLSCREEN);
                Field("NativeAttachTimeoutSeconds", "Native attach timeout (s)", "A native ad that does not attach in time closes as LoadFailed (0 = no limit).");
                Field("NativeCloseDelaySeconds", "Native close delay (s)", "Seconds before the native in-rect close (X) can be tapped (0 = at once, max 30).", SketchAdsRemoteKeys.NATIVE_CLOSE_DELAY);
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("ads_off", "Ads off and remove-ads"))
            {
                Field("Pacing.AdsOffVersions", "Ads-off versions", "Comma-separated app versions (e.g. 1.3,1.4) with banner, interstitial, app open and native off. Rewarded keeps working.", SketchAdsRemoteKeys.ADS_OFF_VERSION);
                GUILayout.Space(4);
                GUILayout.Label("A remove-ads purchase blocks:", EditorStyles.boldLabel);
                SerializedProperty scope = _so.FindProperty("Pacing.RemoveAdsBlocks");
                int value = scope.intValue;
                int next = value;
                next = FlagToggle(next, SketchRemoveAdsScope.Interstitial, "Interstitial");
                next = FlagToggle(next, SketchRemoveAdsScope.AppOpen, "App open");
                next = FlagToggle(next, SketchRemoveAdsScope.Banner, "Banner");
                next = FlagToggle(next, SketchRemoveAdsScope.Native, "Native");
                next = FlagToggle(next, SketchRemoveAdsScope.RewardedInterstitial, "Rewarded interstitial");
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ToggleLeft("Rewarded (never blocked: the player asked for it)", false);
                if (next != value) scope.intValue = next;
                SketchEditorStyles.Help("Value " + value + " = " + (SketchRemoveAdsScope)value + ". Kept on remote config updates.");
                SketchEditorStyles.EndSection();
            }
        }


        static void DrawMediationAdapter(UnityEditor.PackageManager.PackageInfo[] packages, string label, string suffix)
        {
            string packageName = "com.google.ads.mobile.mediation." + suffix;
            var package = Array.Find(packages, item => item.name == packageName);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, package?.version ?? "Not installed");
            using (new EditorGUI.DisabledScope(SketchGmaInstaller.IsBusy || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button(package == null ? "Install" : "Update", GUILayout.Width(70)))
                    SketchGmaInstaller.ChangeMediationAdapter(packageName, false);
                using (new EditorGUI.DisabledScope(package == null || !package.isDirectDependency))
                    if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        SketchGmaInstaller.ChangeMediationAdapter(packageName, true);
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawIap()
        {
            DrawChecks(SketchSetupChecklist.AREA_IAP, "iap_status");
            if (SketchEditorStyles.Section("iap_files", "Files"))
            {
                SketchEditorStyles.PathLink("Unity IAP catalog", File.Exists(SketchSetupScanner.IAP_JSON_PATH) ? SketchSetupScanner.IAP_JSON_PATH : "(missing)");
                SketchEditorStyles.PathLink("Android manifest", File.Exists(SketchSetupScanner.MANIFEST_PATH) ? SketchSetupScanner.MANIFEST_PATH : "(missing)");
                SketchEditorStyles.EndSection();
            }
            List<SketchIapCatalog> catalogs = _catalogs;
            catalogs.RemoveAll(c => c == null);
            if (SketchEditorStyles.Section("iap_catalog", "Product catalog (SketchIapCatalog)"))
            {
                if (catalogs.Count == 0)
                {
                    SketchEditorStyles.Help("No catalog yet. Create one with Assets > Create > Sketch SDK > IAP Catalog, then pass it to SketchStore.Init.");
                }
                else
                {
                    foreach (SketchIapCatalog catalog in catalogs)
                        SketchEditorStyles.PathLink(catalog.Products.Count + " products", AssetDatabase.GetAssetPath(catalog));
                    GUILayout.Space(4);
                    SketchEditorStyles.ProductTable(catalogs[0]);
                    if (catalogs.Count > 1) EditorGUILayout.HelpBox("Several catalogs found; the game passes one to SketchStore.Init. Showing the first.", MessageType.Info);
                    GUILayout.Space(4);
                    UnityEditor.Editor.CreateCachedEditor(catalogs[0], null, ref _catalogEditor);
                    _catalogEditor.OnInspectorGUI();
                }
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("iap_store", "Store"))
            {
                Field("IosAppStoreId", "iOS App Store id", "Numeric id used by SketchRating.OpenStorePage on iOS.");
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("iap_code", "Code"))
            {
                SketchEditorStyles.Code("SketchStore.Purchase(\"product_id\", result => { /* result.Status */ }, \"shop\");\nSketchStore.Restore();\nstring price = SketchStore.Price(\"product_id\");\nbool owned = SketchStore.IsOwned(\"remove_ads\");");
                SketchEditorStyles.EndSection();
            }
        }

        void DrawAnalytics()
        {
            DrawChecks(SketchSetupChecklist.AREA_FIREBASE, "an_status");
            if (SketchEditorStyles.Section("an_files", "Firebase files"))
            {
                string json = SketchSetupScanner.FindGoogleServicesJson();
                SketchEditorStyles.PathLink("google-services.json", json ?? "(missing)");
                SketchEditorStyles.PathLink("FirebaseApp.androidlib", Directory.Exists(SketchSetupScanner.FIREBASE_ANDROIDLIB_PATH) ? SketchSetupScanner.FIREBASE_ANDROIDLIB_PATH : "(missing)");
                if (GUILayout.Button("Open Firebase console", GUILayout.Width(160))) Application.OpenURL(FIREBASE_URL);
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("an_fb", "Firebase"))
            {
                Field("FirebaseAnalytics", "Send events to Firebase", "Every SketchAnalytics event also goes to Firebase Analytics (when the plugin is installed).");
                Field("FirebaseLogAdImpression", "Log ad_impression", "Log ad revenue (value + currency) on every AdMob paid event. Turn off if AdMob is linked to Firebase.");
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("an_rev", "Revenue milestones"))
            {
                Field("RevenueMilestonesUsd", "Milestones (USD)", "Lifetime ad + IAP revenue steps; each logs reach_rev_<n> once (Google Ads tROAS).");
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("an_general", "General"))
            {
                Field("AutoInitialize", "Auto initialize", "Start the SDK before the first scene. Off = call SketchSdkApp.Initialize yourself.");
                Field("VerboseLogs", "Verbose logs", "Extra [SketchSdk] logs in Editor and development builds.");
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("an_code", "Code"))
            {
                SketchEditorStyles.Code("SketchAnalytics.Log(\"level_complete\", \"level\", 5, \"mode\", \"normal\");");
                SketchEditorStyles.EndSection();
            }
        }

        void DrawRemoteConfig()
        {
            if (SketchEditorStyles.Section("rc_source", "Where values come from"))
            {
                SketchEditorStyles.Help("Highest wins:  1. Editor override (this tab, Editor only)  2. Sketch server  3. Firebase Remote Config  4. Defaults (game keys, Remote defaults list)  5. Code fallback.");
                SketchSetupFacts facts = _facts;
                SketchEditorStyles.CheckRow(new SketchCheckItem("", "Firebase Remote Config", facts.FirebaseRemoteConfigInstalled ? SketchCheckStatus.Ok : SketchCheckStatus.Info, facts.FirebaseRemoteConfigInstalled ? "Plugin installed: Firebase values are used." : "Plugin not installed: Firebase layer skipped."));
                SketchEditorStyles.CheckRow(new SketchCheckItem("", "Sketch server config", _settings.HasServerCredentials && _settings.ServerRemoteConfig ? SketchCheckStatus.Ok : SketchCheckStatus.Info, _settings.HasServerCredentials ? (_settings.ServerRemoteConfig ? "Fetched on launch (A/B tests)." : "Server on, remote config fetch off.") : "Server off (Server tab)."));
                Field("ServerRemoteConfig", "Fetch from Sketch server", "GET /config on launch and stamp configId on events (A/B tests).");
                Field("RemoteConfigTimeoutSeconds", "Fetch timeout (s)", "The splash waits at most this long for remote config.");
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("rc_keys", "Keys (" + _keys.Count + ")"))
            {
                EditorGUILayout.BeginHorizontal();
                _keyFilter = EditorGUILayout.TextField("Filter", _keyFilter ?? string.Empty);
                if (GUILayout.Button("Clear", GUILayout.Width(60))) { _keyFilter = string.Empty; GUI.FocusControl(null); }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4);
                SketchEditorStyles.KeyHeader();
                bool changed = false;
                foreach (SketchRemoteKeyDef key in _keys)
                {
                    if (!string.IsNullOrEmpty(_keyFilter) && key.Key.IndexOf(_keyFilter, StringComparison.OrdinalIgnoreCase) < 0 && key.Description.IndexOf(_keyFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    _overrides.TryGetValue(key.Key, out string current);
                    string edited = SketchEditorStyles.KeyRow(key, current ?? string.Empty);
                    if (edited != (current ?? string.Empty))
                    {
                        if (string.IsNullOrEmpty(edited)) _overrides.Remove(key.Key); else _overrides[key.Key] = edited;
                        changed = true;
                    }
                }
                if (changed) SketchRemoteOverrides.Save(_overrides);
                GUILayout.Space(6);
                SketchEditorStyles.Help(_overrides.Count == 0
                    ? "Override column: type a value to test it in the Editor (never in builds). Empty = no override."
                    : _overrides.Count + " Editor override(s) active. They apply on the next Play, or now with Apply.");
                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                    if (GUILayout.Button(new GUIContent("Apply now", "Push the overrides into the running game (Play Mode)."), GUILayout.Width(90))) SketchRemoteConfig.ApplyEditorOverrides();
                using (new EditorGUI.DisabledScope(_overrides.Count == 0))
                    if (GUILayout.Button("Clear overrides", GUILayout.Width(110))) { _overrides.Clear(); SketchRemoteOverrides.Save(_overrides); GUI.FocusControl(null); if (EditorApplication.isPlaying) SketchRemoteConfig.ApplyEditorOverrides(); }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("rc_export", "Export defaults"))
            {
                SketchEditorStyles.Help("Firebase template: Firebase console > Remote Config > ⋮ > Publish from a file. Defaults XML: Android res/xml. Server JSON: Sketch dashboard > Remote config.");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy Firebase template", GUILayout.Width(170))) Copy(SketchRemoteExport.FirebaseTemplateJson(_keys), "Firebase template copied.");
                if (GUILayout.Button("Save Firebase template...", GUILayout.Width(180))) SaveText("remoteconfig.template", "json", SketchRemoteExport.FirebaseTemplateJson(_keys));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy server JSON", GUILayout.Width(170))) Copy(SketchRemoteKeyCatalog.DefaultsJson(_keys), "Server defaults JSON copied.");
                if (GUILayout.Button("Save defaults XML...", GUILayout.Width(180))) SaveText("remote_config_defaults", "xml", SketchRemoteExport.DefaultsXml(_keys));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("rc_game", "Game keys (SketchRemoteConfig.Dev.cs)"))
            {
                if (SketchDevScaffold.Exists)
                {
                    SketchEditorStyles.PathLink("Game file", SketchDevScaffold.DEV_FILE);
                    SketchEditorStyles.Help(SketchRemoteConfig.GameKeys.Count + " game key(s). Add keys in RegisterGameKeys; this file is yours, SDK updates never touch it.");
                    if (GUILayout.Button("Open file", GUILayout.Width(90))) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SketchDevScaffold.DEV_FILE));
                }
                else
                {
                    SketchEditorStyles.Help("No game key file yet.");
                    if (GUILayout.Button("Create SketchRemoteConfig.Dev.cs", GUILayout.Width(230))) SketchDevScaffold.EnsureCreated();
                }
                SketchEditorStyles.Code("// Assets/SketchDev/SketchRemoteConfig.Dev.cs\nstatic partial void RegisterGameKeys(List<SketchRemoteKeyDef> keys)\n{\n    keys.Add(SketchRemoteKeyDef.Int(\"start_lives\", 3, \"Lives at the start of a run.\"));\n}\npublic static int StartLives => GetInt(\"start_lives\", 3);");
                SketchEditorStyles.EndSection();
            }

            if (SketchEditorStyles.Section("rc_defaults", "Remote defaults (free-form)"))
            {
                Field("RemoteDefaults", "Remote defaults", "Extra key/value defaults used until the server answers (number, true/false, text or JSON). Prefer typed game keys.");
                SketchEditorStyles.EndSection();
            }
        }

        void DrawServer()
        {
            DrawChecks(SketchSetupChecklist.AREA_SERVER, "sv_status");
            if (SketchEditorStyles.Section("sv_main", "Sketch Server"))
            {
                Field("ServerEnabled", "Enabled", "Send analytics, revenue and remote config requests to the Sketch server.");
                using (new EditorGUI.DisabledScope(!_settings.ServerEnabled))
                {
                    Field("ServerUrl", "Server URL", "Default https://api.sketchapi.win/api/v1. Must be https on Android.");
                    Field("DevServerUrl", "Dev server URL (Development Builds)", "Development Builds send analytics here instead of production and never to Firebase. Empty = they send nothing.");
                    Field("ServerAppId", "App Id", "Shown in the Sketch dashboard (Admin > Apps).");
                    ApiKeyField();
                    Field("ServerFlushIntervalSeconds", "Upload every (s)", "Seconds between event uploads.");
                }
                using (new EditorGUI.DisabledScope(true))
                {
                    Field("BuildVersionCode", "Version sent (bundle code)", "Written automatically before each build from Player Settings bundle version code (now " + PlayerSettings.Android.bundleVersionCode + "). The dashboard From/To Version filter uses this number.");
                }
                GUILayout.Space(4);
                if (GUILayout.Button("Open dashboard", GUILayout.Width(130))) Application.OpenURL(DASHBOARD_URL);
                SketchEditorStyles.EndSection();
            }
        }

        void ApiKeyField()
        {
            SerializedProperty key = _so.FindProperty("ServerApiKey");
            EditorGUILayout.BeginHorizontal();
            if (_showApiKey) EditorGUILayout.PropertyField(key, new GUIContent("API key", "App API key (sgk_...) shown once when the app was created."));
            else
            {
                EditorGUILayout.PrefixLabel(new GUIContent("API key", "Hidden. Press Show to edit."));
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.TextField(string.IsNullOrEmpty(key.stringValue) ? "(empty)" : SketchSetupChecklist.MaskSecret(key.stringValue));
            }
            if (GUILayout.Button(_showApiKey ? "Hide" : "Show", EditorStyles.miniButtonLeft, GUILayout.Width(48))) { _showApiKey = !_showApiKey; GUI.FocusControl(null); }
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(key.stringValue)))
                if (GUILayout.Button("Copy", EditorStyles.miniButtonRight, GUILayout.Width(48))) Copy(key.stringValue, "API key copied.");
            EditorGUILayout.EndHorizontal();
            SketchEditorStyles.FieldHelp("App API key (sgk_...) shown once when the app was created. Kept hidden on screen.");
        }

        void DrawSplash()
        {
            DrawChecks(SketchSetupChecklist.AREA_SPLASH, "sp_status");
            if (SketchEditorStyles.Section("sp_scenes", "Build scenes"))
            {
                EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
                int index = 0;
                foreach (EditorBuildSettingsScene scene in scenes)
                {
                    if (!scene.enabled) continue;
                    SketchEditorStyles.InfoRow("Index " + index++, scene.path);
                }
                if (index == 0) SketchEditorStyles.Help("No enabled scenes in Build Settings.");
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("sp_hook", "Splash look (SketchSplashHook)"))
            {
                const string hookPath = "Assets/Resources/SketchSplashHook.asset";
                var hook = AssetDatabase.LoadAssetAtPath<ScriptableObject>(hookPath);
                SketchEditorStyles.PathLink("Hook asset", hook != null ? hookPath : "(missing: Build Splash Scene creates it)");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Build Splash Scene", "Rebuilds the SDK splash scene from the hook and puts it at build index 0."), GUILayout.Width(150))
                    && EditorUtility.DisplayDialog("Build Splash Scene", "Rebuild Assets/SketchSdk/Splash/SketchSplash.unity from the hook and set it as build index 0?", "Build", "Cancel"))
                    EditorApplication.ExecuteMenuItem("Sketch SDK/Build Splash Scene");
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                if (hook != null)
                {
                    GUILayout.Space(4);
                    UnityEditor.Editor.CreateCachedEditor(hook, null, ref _hookEditor);
                    _hookEditor.OnInspectorGUI();
                }
                SketchEditorStyles.EndSection();
            }
        }

        void DrawConsent()
        {
            DrawChecks(SketchSetupChecklist.AREA_CONSENT, "cs_status");
            if (SketchEditorStyles.Section("cs_ump", "Consent (Google UMP)"))
            {
                Field("RequestConsent", "Request consent", "Ask GDPR / US-state consent before ads. Create the message in AdMob > Privacy & messaging.");
                Field("IosTrackingUsageDescription", "iOS tracking purpose", "Leave empty when the mediation CMP handles ATT.");
                Field("PrivacyPolicyUrl", "Privacy policy URL", "Used by the game's privacy entry.");
                Field("ConsentDebugGeography", "Debug geography", "Pretend the device is in this region to test the form (development builds only).");
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("cs_tags", "Child safety tags"))
            {
                Field("ChildDirected", "Child directed", "COPPA tag. True only for apps for children.");
                Field("UnderAgeOfConsent", "Under age of consent", "GDPR tag for users under the age of consent.");
                Field("MaxContentRating", "Max ad content rating", "Highest ad content rating allowed.");
                SketchEditorStyles.EndSection();
            }
            if (SketchEditorStyles.Section("cs_code", "Code"))
            {
                SketchEditorStyles.Code("if (SketchConsent.IsPrivacyOptionsRequired)\n    SketchConsent.ShowPrivacyOptions();   // Settings > Privacy button");
                SketchEditorStyles.EndSection();
            }
        }

        // ---------------- helpers ----------------

        void DrawChecks(string area, string key)
        {
            if (!SketchEditorStyles.Section(key, "Status")) return;
            foreach (SketchCheckItem item in _checks) if (item.Area == area) SketchEditorStyles.CheckRow(item);
            SketchEditorStyles.EndSection();
        }

        void Field(string path, string label, string help, string remoteKey = null)
        {
            SerializedProperty property = _so.FindProperty(path);
            if (property == null) { EditorGUILayout.HelpBox("Missing field " + path, MessageType.Warning); return; }
            SketchEditorStyles.PlainField(property, new GUIContent(label, help));
            SketchEditorStyles.FieldHelp(help, remoteKey);
        }

        void IdField(string path, string label, bool appId, string help)
        {
            SerializedProperty property = _so.FindProperty(path);
            string value = property.stringValue?.Trim();
            SketchCheckStatus status = string.IsNullOrEmpty(value) ? (appId ? SketchCheckStatus.Error : SketchCheckStatus.Info)
                : (appId ? SketchSetupChecklist.IsAppId(value) : SketchSetupChecklist.IsAdUnitId(value)) ? (SketchAdMobTestIds.IsTestId(value) ? SketchCheckStatus.Warning : SketchCheckStatus.Ok)
                : SketchCheckStatus.Error;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(property, new GUIContent(label, help));
            SketchEditorStyles.Badge(status, status == SketchCheckStatus.Info ? "OFF" : null, 52);
            EditorGUILayout.EndHorizontal();
            SketchEditorStyles.FieldHelp(help);
        }

        static int FlagToggle(int value, SketchRemoveAdsScope flag, string label)
        {
            bool on = (value & (int)flag) != 0;
            bool next = EditorGUILayout.ToggleLeft(label, on);
            return next == on ? value : next ? value | (int)flag : value & ~(int)flag;
        }

        void SaveAndVerify()
        {
            _so.ApplyModifiedProperties();
            _settings.Android.Trim();
            _settings.Ios.Trim();
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssetIfDirty(_settings);
            SketchSdkEditorUtil.SyncAdMobDefine();
            SketchSdkEditorUtil.SyncFirebaseDefine();
            string sync = SketchSdkEditorUtil.SyncAppIds(_settings);
            Refresh();
            int errors = SketchSetupChecklist.Count(_checks, SketchCheckStatus.Error);
            int warnings = SketchSetupChecklist.Count(_checks, SketchCheckStatus.Warning);
            _status = $"Saved. {sync} {errors} error(s), {warnings} warning(s).";
            Debug.Log("[SketchSdk] " + _status);
        }

        void Copy(string text, string message)
        {
            EditorGUIUtility.systemCopyBuffer = text;
            _status = message;
        }

        void SaveText(string name, string extension, string text)
        {
            string path = EditorUtility.SaveFilePanel("Save " + name, "", name, extension);
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, text);
            _status = "Saved " + path;
        }

        static void Ping(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null) return;
            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(asset);
        }
    }
}
