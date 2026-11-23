using System.Collections.Generic;
using NUnit.Framework;

namespace Sketch.Sdk.Tests
{
    // 1.4.2: the remove-ads purchase blocks only the formats listed in SketchAdsPolicySettings.RemoveAdsBlocks.
    public sealed class SketchRemoveAdsScopeTests
    {
        static SketchAdsPolicy Removed(SketchRemoveAdsScope scope)
        {
            var settings = new SketchAdsPolicySettings { InterstitialStartDelaySeconds = 0f, InterstitialCooldownSeconds = 0f, RemoveAdsBlocks = scope };
            return new SketchAdsPolicy(settings, 0d) { AdsRemoved = true };
        }

        [Test]
        public void Default_IsUnchanged_InterAppOpenBannerNativeBlocked_RewardedAndRiKept()
        {
            Assert.AreEqual(SketchRemoveAdsScope.Default, new SketchAdsPolicySettings().RemoveAdsBlocks);
            var policy = Removed(SketchRemoveAdsScope.Default);
            Assert.IsFalse(policy.CanShowInterstitial("p", 100));
            Assert.IsFalse(policy.CanShowAppOpen(100, true, 100));
            Assert.IsFalse(policy.CanShowBanner(100));
            Assert.IsFalse(policy.CanShowNative());
            Assert.IsTrue(policy.CanShowRewardedInterstitial());
            Assert.IsTrue(policy.CanShowRewarded());
        }

        [Test]
        public void InterstitialFlag_BlocksOnlyInterstitial()
        {
            var policy = Removed(SketchRemoveAdsScope.Interstitial);
            Assert.IsFalse(policy.CanShowInterstitial("p", 100));
            Assert.AreEqual("ads_removed", policy.LastBlockReason);
            Assert.IsTrue(policy.CanShowBanner(100), "banner kept");
            Assert.IsTrue(policy.CanShowNative(), "native kept");
            Assert.IsTrue(policy.CanShowAppOpen(100, true, 100), "app open kept");
            Assert.IsTrue(policy.CanShowRewardedInterstitial(), "RI kept");
            Assert.IsTrue(policy.CanShowRewarded());
        }

        [Test]
        public void EachFlag_BlocksItsFormat()
        {
            Assert.IsFalse(Removed(SketchRemoveAdsScope.AppOpen).CanShowAppOpen(100, true, 100));
            Assert.IsTrue(Removed(SketchRemoveAdsScope.AppOpen).CanShowInterstitial("p", 100));
            Assert.IsFalse(Removed(SketchRemoveAdsScope.Banner).CanShowBanner(100));
            Assert.IsTrue(Removed(SketchRemoveAdsScope.Banner).CanShowNative());
            Assert.IsFalse(Removed(SketchRemoveAdsScope.Native).CanShowNative());
            Assert.IsTrue(Removed(SketchRemoveAdsScope.Native).CanShowBanner(100));
            Assert.IsFalse(Removed(SketchRemoveAdsScope.RewardedInterstitial).CanShowRewardedInterstitial());
            Assert.IsTrue(Removed(SketchRemoveAdsScope.RewardedInterstitial).CanShowInterstitial("p", 100));
            Assert.IsTrue(Removed(SketchRemoveAdsScope.All).CanShowRewarded(), "rewarded is never blocked");
        }

        [Test]
        public void IsRemoved_RequiresPurchaseAndFlag()
        {
            var policy = Removed(SketchRemoveAdsScope.Interstitial);
            Assert.IsTrue(policy.IsRemoved(SketchRemoveAdsScope.Interstitial));
            Assert.IsFalse(policy.IsRemoved(SketchRemoveAdsScope.Banner));
            policy.AdsRemoved = false;
            Assert.IsFalse(policy.IsRemoved(SketchRemoveAdsScope.Interstitial));
            Assert.IsTrue(policy.CanShowInterstitial("p", 100));
        }

        [Test]
        public void RemoteApply_KeepsTheScope()
        {
            var values = new SketchConfigValues();
            values.SetRemote(new Dictionary<string, object> { { SketchAdsRemoteKeys.INTER_COOLDOWN, 10 } }, "cfg");
            var source = new SketchAdsPolicySettings { RemoveAdsBlocks = SketchRemoveAdsScope.Interstitial | SketchRemoveAdsScope.RewardedInterstitial };
            Assert.AreEqual(source.RemoveAdsBlocks, SketchAdsRemoteKeys.Apply(source, values).RemoveAdsBlocks);
        }

        [Test]
        public void AdsOffVersion_CachedMatch_FollowsListChanges()
        {
            var policy = new SketchAdsPolicy(new SketchAdsPolicySettings { AdsOffVersions = "1.0" }, 0d) { AppVersion = "1.0" };
            Assert.IsTrue(policy.IsAdsOffForVersion);
            Assert.IsTrue(policy.IsAdsOffForVersion);
            policy.Settings.AdsOffVersions = "2.0";
            Assert.IsFalse(policy.IsAdsOffForVersion);
            policy.AppVersion = "2.0";
            Assert.IsTrue(policy.IsAdsOffForVersion);
        }
    }
}
