using System;
using NUnit.Framework;
using UnityEngine;

namespace Sketch.Sdk.Tests
{
    // OR-883: a fullscreen ad pauses all game audio (music + stingers) and restores the previous state on close.
    public sealed class SketchAdAudioPauseTests
    {
        sealed class DeferredProvider : ISketchAdsProvider
        {
            public Action<bool> PendingRewarded;
            public void Initialize(Action<bool> onInitialized) => onInitialized?.Invoke(true);
            public bool IsReady(SketchAdFormat format) => true;
            public void ShowRewarded(Action<bool> onComplete) => PendingRewarded = onComplete;
            public void ShowInterstitial(Action onClosed) => onClosed?.Invoke();
            public void ShowAppOpen(Action onClosed) => onClosed?.Invoke();
        }

        bool _pause;

        [SetUp] public void SetUp() => _pause = AudioListener.pause;
        [TearDown] public void TearDown() => AudioListener.pause = _pause;

        [Test]
        public void Rewarded_PausesAudioWhileShowing_AndRestoresOnClose()
        {
            var provider = new DeferredProvider();
            SketchSdk.Initialize(provider);
            AudioListener.pause = false;
            int results = 0;
            Assert.IsTrue(SketchAds.ShowRewarded("or883_audio", _ => results++));
            Assert.IsTrue(AudioListener.pause, "audio pauses during the ad");
            Assert.NotNull(provider.PendingRewarded);
            provider.PendingRewarded(true);
            Assert.IsFalse(AudioListener.pause, "audio resumes after the ad");
            Assert.AreEqual(1, results);
        }

        [Test]
        public void Rewarded_KeepsAPausedStatePaused()
        {
            var provider = new DeferredProvider();
            SketchSdk.Initialize(provider);
            AudioListener.pause = true;
            Assert.IsTrue(SketchAds.ShowRewarded("or883_audio", null));
            provider.PendingRewarded(false);
            Assert.IsTrue(AudioListener.pause, "an already paused game stays paused");
        }
    }
}
