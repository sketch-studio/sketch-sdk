using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sketch.Sdk.Editor;
using UnityEngine;

namespace Sketch.Sdk.Tests
{
    public sealed class SketchSetupChecklistTests
    {
        SketchSdkSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SketchSdkSettings>();
            _settings.Android = new SketchAdUnitIds
            {
                AppId = "ca-app-pub-1~2", Banner = "ca-app-pub-1/1", Interstitial = "ca-app-pub-1/2", Rewarded = "ca-app-pub-1/3",
                AppOpen = "ca-app-pub-1/4", Native = "ca-app-pub-1/5", RewardedInterstitial = "ca-app-pub-1/6",
            };
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_settings);

        static SketchSetupFacts GoodFacts() => new SketchSetupFacts
        {
            SettingsLoadableAtRuntime = true,
            GoogleMobileAdsInstalled = true, UmpInstalled = true, AdMobDefineActive = true,
            FirebaseAnalyticsInstalled = true, FirebaseRemoteConfigInstalled = true, UnityIapInstalled = true,
            IapProductCatalogJson = true, SketchIapCatalogs = 1, SketchIapProducts = 6,
            GoogleServicesJson = true, FirebaseAndroidLib = true,
            AndroidManifestXml = "<manifest><uses-permission android:name=\"com.android.vending.BILLING\" /></manifest>",
            EnabledScenes = new[] { "Assets/SketchSdk/Splash/SketchSplash.unity", "Assets/_Game/Scenes/Game.unity" },
        };

        static SketchCheckItem Find(List<SketchCheckItem> items, string label)
        {
            foreach (SketchCheckItem item in items) if (item.Label == label) return item;
            Assert.Fail("No check named " + label);
            return default;
        }

        [Test]
        public void FullyConfiguredGame_HasNoErrorsOrWarnings()
        {
            var items = SketchSetupChecklist.Build(_settings, GoodFacts());
            Assert.IsNotEmpty(items);
            var bad = items.Where(i => i.Status == SketchCheckStatus.Error || i.Status == SketchCheckStatus.Warning).Select(i => i.Label + ": " + i.Hint).ToList();
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void AppIdWithSlash_IsError()
        {
            _settings.Android.AppId = "ca-app-pub-1/2";
            Assert.AreEqual(SketchCheckStatus.Error, Find(SketchSetupChecklist.Build(_settings, GoodFacts()), "Android App ID").Status);
        }

        [Test]
        public void BlankAdUnit_IsWarning_BadUnit_IsError()
        {
            _settings.Android.Banner = "";
            _settings.Android.Native = "ca-app-pub-1~5";
            var items = SketchSetupChecklist.Build(_settings, GoodFacts());
            Assert.AreEqual(SketchCheckStatus.Warning, Find(items, "Android Banner").Status);
            Assert.AreEqual(SketchCheckStatus.Error, Find(items, "Android Native").Status);
        }

        [Test]
        public void EmptyIosPlatform_IsOneInfoRow()
        {
            var ios = SketchSetupChecklist.Build(_settings, GoodFacts()).Where(i => i.Label.StartsWith("iOS")).ToList();
            Assert.AreEqual(1, ios.Count);
            Assert.AreEqual(SketchCheckStatus.Info, ios[0].Status);
        }

        [Test]
        public void ForceTestAds_IsError()
        {
            _settings.ForceTestAds = true;
            Assert.AreEqual(SketchCheckStatus.Error, Find(SketchSetupChecklist.Build(_settings, GoodFacts()), "Test ads off in release").Status);
        }

        [Test]
        public void MissingBillingPermission_IsErrorWhenCatalogHasProducts()
        {
            var facts = GoodFacts();
            facts.AndroidManifestXml = "<manifest></manifest>";
            Assert.AreEqual(SketchCheckStatus.Error, Find(SketchSetupChecklist.Build(_settings, facts), "BILLING permission").Status);
        }

        [Test]
        public void SplashNotFirst_IsWarning()
        {
            var facts = GoodFacts();
            facts.EnabledScenes = new[] { "Assets/_Game/Scenes/Game.unity", "Assets/SketchSdk/Splash/SketchSplash.unity" };
            Assert.AreEqual(SketchCheckStatus.Warning, Find(SketchSetupChecklist.Build(_settings, facts), "Splash at build index 0").Status);
        }

        [Test]
        public void Server_OffIsInfo_EnabledWithoutKeyIsError_CompleteIsOk()
        {
            Assert.AreEqual(SketchCheckStatus.Info, Find(SketchSetupChecklist.Build(_settings, GoodFacts()), "Sketch Server").Status);
            _settings.ServerEnabled = true;
            _settings.ServerAppId = "app";
            Assert.AreEqual(SketchCheckStatus.Error, Find(SketchSetupChecklist.Build(_settings, GoodFacts()), "Sketch Server").Status);
            _settings.ServerApiKey = "sgk_x";
            Assert.AreEqual(SketchCheckStatus.Ok, Find(SketchSetupChecklist.Build(_settings, GoodFacts()), "Sketch Server").Status);
        }

        [Test]
        public void FirebaseOnWithoutGoogleServices_IsError()
        {
            var facts = GoodFacts();
            facts.GoogleServicesJson = false;
            Assert.AreEqual(SketchCheckStatus.Error, Find(SketchSetupChecklist.Build(_settings, facts), "google-services.json").Status);
        }

        [Test]
        public void Helpers_ClassifyIdsManifestSplashAndMaskSecrets()
        {
            Assert.IsTrue(SketchSetupChecklist.IsAppId("ca-app-pub-1~2"));
            Assert.IsFalse(SketchSetupChecklist.IsAppId("ca-app-pub-1/2"));
            Assert.IsTrue(SketchSetupChecklist.IsAdUnitId("ca-app-pub-1/2"));
            Assert.IsFalse(SketchSetupChecklist.IsAdUnitId("ca-app-pub-1~2"));
            Assert.IsTrue(SketchSetupChecklist.ManifestHasBilling("<uses-permission android:name=\"com.android.vending.BILLING\"/>"));
            Assert.IsFalse(SketchSetupChecklist.ManifestHasBilling(null));
            Assert.IsTrue(SketchSetupChecklist.IsSplashFirst(new[] { "Assets/X/SketchSplash.unity" }));
            Assert.IsFalse(SketchSetupChecklist.IsSplashFirst(new string[0]));
            Assert.AreEqual("", SketchSetupChecklist.MaskSecret(""));
            Assert.AreEqual("sgk_••••••cdef", SketchSetupChecklist.MaskSecret("sgk_0123456789abcdef"));
            Assert.AreEqual("••••", SketchSetupChecklist.MaskSecret("abcd"));
        }

        [Test]
        public void RemoteKeys_ListEveryKeyWithLocalDefault()
        {
            _settings.Pacing.InterstitialCooldownSeconds = 30f;
            _settings.Pacing.InterstitialStartDelaySeconds = 0f;
            var keys = SketchRemoteKeyCatalog.Build(_settings);
            Assert.AreEqual("30", keys.First(k => k.Key == SketchAdsRemoteKeys.INTER_COOLDOWN).DefaultValue);
            Assert.AreEqual("0", keys.First(k => k.Key == SketchAdsRemoteKeys.INTER_START_DELAY).DefaultValue);
            foreach (FieldInfo f in typeof(SketchAdsRemoteKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
                Assert.IsTrue(keys.Any(k => k.Key == (string)f.GetValue(null)), "missing " + f.Name);
            Assert.IsTrue(keys.Any(k => k.Key == SketchNotificationsRemoteKeys.ENABLED));
            Assert.IsTrue(keys.Any(k => k.Key == SketchAppUpdatePolicy.FORCE_MIN_VERSION_CODE_KEY));
            StringAssert.Contains("\"sketch_inter_cooldown\": 30", SketchRemoteKeyCatalog.DefaultsJson(keys));
        }

        [TestCase(SketchAdsBackend.LevelPlay)]
        [TestCase(SketchAdsBackend.Max)]
        [TestCase(SketchAdsBackend.Liftoff)]
        public void StandaloneProviderDoesNotRequireAdMob(SketchAdsBackend backend)
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<SketchSdkSettings>();
            try
            {
                settings.AdsBackend = backend;
                var facts = new SketchSetupFacts { LevelPlayInstalled = true, MaxInstalled = true, LiftoffInstalled = true };
                var checks = SketchSetupChecklist.Build(settings, facts);
                Assert.IsTrue(checks.Exists(item => item.Label == backend + " provider" && item.Status == SketchCheckStatus.Ok));
                Assert.IsFalse(checks.Exists(item => item.Label == "Google Mobile Ads plugin"));
            }
            finally { UnityEngine.Object.DestroyImmediate(settings); }
        }

        [Test]
        public void SettingsWindow_DrawsEverySerializedField()
        {
            var drawn = new HashSet<string>(SketchSdkSettingsWindow.DRAWN_FIELDS);
            foreach (FieldInfo f in typeof(SketchSdkSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.IsTrue(drawn.Contains(f.Name), "Settings window does not draw " + f.Name);
            foreach (FieldInfo f in typeof(SketchAdsPolicySettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
                Assert.IsTrue(drawn.Contains("Pacing." + f.Name), "Settings window does not draw Pacing." + f.Name);
        }
    }
}
