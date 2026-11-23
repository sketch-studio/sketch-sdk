using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Sketch.Sdk.Tests
{
    /// <summary>Runs the real bootstrap in Play Mode with the Editor fake provider (no settings asset needed).</summary>
    public sealed class SketchSdkPlayModeTests
    {
        static IEnumerator WaitReady()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!SketchSdkApp.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(SketchSdkApp.IsReady, "SDK did not become ready");
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_BecomesReady_WithFakeProvider()
        {
            yield return WaitReady();
            Assert.IsTrue(SketchSdk.IsInitialized);
            Assert.IsInstanceOf<SketchEditorFakeProvider>(SketchSdk.Provider);
            Assert.IsTrue(SketchAds.IsRewardedReady);
            Assert.IsTrue(SketchRemoteConfig.IsReady);
            Assert.IsFalse(string.IsNullOrEmpty(SketchIdentity.InstallationId));
            Assert.GreaterOrEqual(SketchIdentity.Session, 1);
        }

        [UnityTest]
        public IEnumerator Rewarded_GrantsExactlyOnce_AndRestoresAudio()
        {
            yield return WaitReady();
            int calls = 0;
            bool granted = false;
            var events = new List<string>();
            SketchAnalytics.EventLogged += (name, p) => { if (name.StartsWith("ad_reward")) events.Add(name + ":" + p["placement"]); };
            AudioListener.pause = false;
            bool started = SketchAds.ShowRewarded("test_double", ok => { calls++; granted = ok; });
            Assert.IsTrue(started);
            Assert.IsTrue(AudioListener.pause, "audio pauses during the ad");
            yield return Frames(5);
            Assert.AreEqual(1, calls);
            Assert.IsTrue(granted);
            Assert.IsFalse(AudioListener.pause, "audio restored after the ad");
            CollectionAssert.Contains(events, "ad_reward_granted:test_double");
        }

        [UnityTest]
        public IEnumerator Rewarded_NoFill_ReturnsFalseOnce()
        {
            yield return WaitReady();
            SketchEditorFakeProvider.SimulateNoFill = true;
            try
            {
                int calls = 0;
                bool granted = true;
                Assert.IsFalse(SketchAds.ShowRewarded("nofill", ok => { calls++; granted = ok; }));
                yield return Frames(3);
                Assert.AreEqual(1, calls);
                Assert.IsFalse(granted);
            }
            finally { SketchEditorFakeProvider.SimulateNoFill = false; }
        }

        [UnityTest]
        public IEnumerator RewardedInterstitial_GrantsExactlyOnce_AndRestoresAudio()
        {
            yield return WaitReady();
            Assert.IsTrue(SketchAds.IsRewardedInterstitialReady, "fake provider supports rewarded interstitial");
            int calls = 0, finished = 0;
            bool granted = false;
            var events = new List<string>();
            SketchAnalytics.EventLogged += (name, p) => { if (name.StartsWith("ad_reward")) events.Add(name + ":" + p["format"] + ":" + p["placement"]); };
            void OnFinished(string placement, bool ok) => finished++;
            SketchAds.RewardedInterstitialFinished += OnFinished;
            try
            {
                AudioListener.pause = false;
                bool started = SketchAds.ShowRewardedInterstitial("ri_bonus", ok => { calls++; granted = ok; });
                Assert.IsTrue(started);
                Assert.IsTrue(AudioListener.pause, "audio pauses during the ad");
                yield return Frames(5);
                Assert.AreEqual(1, calls);
                Assert.AreEqual(1, finished);
                Assert.IsTrue(granted);
                Assert.IsFalse(AudioListener.pause, "audio restored after the ad");
                CollectionAssert.Contains(events, "ad_reward_granted:rewarded_interstitial:ri_bonus");
            }
            finally { SketchAds.RewardedInterstitialFinished -= OnFinished; }
        }

        [UnityTest]
        public IEnumerator RewardedInterstitial_NoFill_ReturnsFalseOnce()
        {
            yield return WaitReady();
            SketchEditorFakeProvider.SimulateNoFill = true;
            try
            {
                int calls = 0;
                bool granted = true;
                Assert.IsFalse(SketchAds.ShowRewardedInterstitial("nofill", ok => { calls++; granted = ok; }));
                yield return Frames(3);
                Assert.AreEqual(1, calls);
                Assert.IsFalse(granted);
            }
            finally { SketchEditorFakeProvider.SimulateNoFill = false; }
        }

        [UnityTest]
        public IEnumerator RewardedInterstitial_Skipped_ReportsFalse()
        {
            yield return WaitReady();
            SketchEditorFakeProvider.SimulateRewardSkipped = true;
            try
            {
                int calls = 0;
                bool granted = true;
                Assert.IsTrue(SketchAds.ShowRewardedInterstitial("skip", ok => { calls++; granted = ok; }));
                yield return Frames(3);
                Assert.AreEqual(1, calls);
                Assert.IsFalse(granted);
            }
            finally { SketchEditorFakeProvider.SimulateRewardSkipped = false; }
        }

        [UnityTest]
        public IEnumerator AdsOffVersion_BlocksInterstitialAndRewardedInterstitial_KeepsRewarded()
        {
            yield return WaitReady();
            SketchAdsPolicySettings saved = SketchAds.Policy.Settings;
            string savedVersion = SketchAds.Policy.AppVersion;
            bool savedAdsRemoved = SketchAds.Policy.AdsRemoved;
            try
            {
                SketchAds.Policy.AdsRemoved = false;
                SketchAds.Policy.AppVersion = Application.version;
                SketchAds.Policy.Settings = new SketchAdsPolicySettings
                {
                    InterstitialStartDelaySeconds = 0f, InterstitialCooldownSeconds = 0f, AdsOffVersions = "0.0.0-other, " + Application.version,
                };
                int closed = 0;
                Assert.IsFalse(SketchAds.ShowInterstitial("x", () => closed++));
                Assert.AreEqual("ads_off_version", SketchAds.Policy.LastBlockReason);
                Assert.AreEqual(1, closed, "onClosed still fires once");
                Assert.IsFalse(SketchAds.IsRewardedInterstitialReady);
                bool riResult = true;
                Assert.IsFalse(SketchAds.ShowRewardedInterstitial("ri_off", ok => riResult = ok));
                Assert.IsFalse(riResult);
                Assert.IsFalse(SketchAds.IsNativeReady);
                bool granted = false;
                Assert.IsTrue(SketchAds.ShowRewarded("reward_on", ok => granted = ok), "rewarded keeps working for an ads-off version");
                yield return Frames(3);
                Assert.IsTrue(granted);
            }
            finally
            {
                SketchAds.Policy.Settings = saved;
                SketchAds.Policy.AppVersion = savedVersion;
                SketchAds.Policy.AdsRemoved = savedAdsRemoved;
            }
        }

        [UnityTest]
        public IEnumerator Interstitial_FollowsPacing_AndAlwaysCallsOnClosed()
        {
            yield return WaitReady();
            SketchAdsPolicySettings saved = SketchAds.Policy.Settings;
            bool savedAdsRemoved = SketchAds.Policy.AdsRemoved;
            try
            {
                SketchAds.Policy.AdsRemoved = false;
                SketchAds.Policy.Settings = new SketchAdsPolicySettings { InterstitialStartDelaySeconds = 1e9f, InterstitialCooldownSeconds = 60f };
                int closed = 0;
                Assert.IsFalse(SketchAds.ShowInterstitial("level_end", () => closed++), "start delay blocks");
                Assert.AreEqual(1, closed, "onClosed fires even when blocked");

                SketchAds.Policy.Settings = new SketchAdsPolicySettings { InterstitialStartDelaySeconds = 0f, InterstitialCooldownSeconds = 60f };
                SketchAds.Policy.OnFullscreenClosed(double.NegativeInfinity);
                SketchAds.Policy.ResetInterstitialCooldown(SketchIdentity.NowUnix - 1000);
                closed = 0;
                Assert.IsTrue(SketchAds.ShowInterstitial("level_end", () => closed++));
                yield return Frames(3);
                Assert.AreEqual(1, closed);
                Assert.IsFalse(SketchAds.ShowInterstitial("level_end", () => closed++), "cooldown blocks the next one");
                Assert.AreEqual(2, closed);
                Assert.IsTrue(SketchAds.ShowInterstitial("level_end", () => closed++, skipCooldown: true));
                yield return Frames(3);
                Assert.AreEqual(3, closed);
            }
            finally
            {
                SketchAds.Policy.Settings = saved;
                SketchAds.Policy.AdsRemoved = savedAdsRemoved;
            }
        }

        [UnityTest]
        public IEnumerator RemoveAds_BlocksInterstitialAndBanner_KeepsRewarded()
        {
            yield return WaitReady();
            SketchAdsPolicySettings saved = SketchAds.Policy.Settings;
            bool savedAdsRemoved = SketchAds.Policy.AdsRemoved;
            try
            {
                SketchAds.Policy.Settings = new SketchAdsPolicySettings { InterstitialStartDelaySeconds = 0f, InterstitialCooldownSeconds = 0f };
                SketchAds.Policy.AdsRemoved = true;
                Assert.IsFalse(SketchAds.ShowInterstitial("x"));
                SketchAds.ShowBanner();
                yield return Frames(2);
                Assert.AreEqual(0f, SketchAds.BannerHeightPixels);
                bool granted = false;
                Assert.IsTrue(SketchAds.ShowRewarded("still_works", ok => granted = ok));
                yield return Frames(3);
                Assert.IsTrue(granted);
                bool riGranted = false;
                Assert.IsTrue(SketchAds.ShowRewardedInterstitial("ri_still_works", ok => riGranted = ok), "rewarded interstitial keeps working under remove-ads");
                yield return Frames(3);
                Assert.IsTrue(riGranted);
            }
            finally
            {
                SketchAds.Policy.AdsRemoved = savedAdsRemoved;
                SketchAds.HideBanner();
                SketchAds.Policy.Settings = saved;
            }
        }

        [UnityTest]
        public IEnumerator Banner_ShowsSimulatedHeight_AndHides()
        {
            yield return WaitReady();
            float reported = -1f;
            void OnHeight(float h) => reported = h;
            SketchAds.BannerHeightChanged += OnHeight;
            try
            {
                SketchAds.ShowBanner();
                yield return Frames(2);
                Assert.Greater(SketchAds.BannerHeightPixels, 0f);
                Assert.Greater(reported, 0f);
                SketchAds.HideBanner();
                yield return Frames(2);
                Assert.AreEqual(0f, SketchAds.BannerHeightPixels);
                Assert.AreEqual(0f, reported);
            }
            finally { SketchAds.BannerHeightChanged -= OnHeight; SketchAds.HideBanner(); }
        }

        [UnityTest]
        public IEnumerator Loop_PostFromWorkerThread_RunsOnMainThread()
        {
            yield return WaitReady();
            bool ranOnMain = false;
            var worker = new Thread(() => SketchSdkLoop.Post(() => ranOnMain = SketchSdkLoop.IsMainThread));
            worker.Start();
            worker.Join();
            yield return Frames(2);
            Assert.IsTrue(ranOnMain);
        }

        [UnityTest]
        public IEnumerator Analytics_Log_ReachesSinks_WithSanitizedName()
        {
            yield return WaitReady();
            string seen = null;
            SketchAnalytics.EventLogged += (name, p) => { if (name.StartsWith("level")) seen = name + "|" + p["level"]; };
            SketchAnalytics.Log("level-complete!", "level", 7);
            yield return Frames(1);
            Assert.AreEqual("level_complete_|7", seen);
        }
    }
}
