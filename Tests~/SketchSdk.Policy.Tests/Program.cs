using Sketch.Sdk;
using Sketch.Sdk.Samples;
using System.Reflection;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}

Check(SketchInterstitialPacing.Allowed(5, double.NegativeInfinity, 30), "first show should pass");
Check(!SketchInterstitialPacing.Allowed(129.9, 100, 30), "interval boundary must block");
Check(SketchInterstitialPacing.Allowed(130, 100, 30), "interval boundary must pass");
Check(SketchInterstitialPacing.Allowed(100, 100, 0), "zero interval should pass");
Check(SketchAdMobConfig.IsUsableId("ca-app-pub-3940256099942544/5224354917"), "Google test unit ids remain usable");
Check(!SketchAdMobConfig.IsUsableId(" "), "empty ids are rejected");

int shown = 0, closed = 0, clicked = 0;
var gate = new SketchNativeCallbacks.Gate(() => shown++, () => clicked++, _ => closed++);
gate.ProviderCallbacks.Shown();
gate.ProviderCallbacks.Closed(SketchNativeCloseReason.SdkClosed);
Check(shown == 0 && closed == 0, "callbacks must queue before accept");
gate.Accept();
gate.Accept();
Check(shown == 1 && closed == 1, "accept flushes pending provider callbacks exactly once");
gate.ProviderCallbacks.Shown();
gate.ProviderCallbacks.Clicked();
gate.ProviderCallbacks.Closed(SketchNativeCloseReason.SdkClosed);
gate.ProviderCallbacks.Closed(SketchNativeCloseReason.SdkClosed);
Check(shown == 1 && clicked == 1 && closed == 1, "duplicate show and close callbacks are ignored");

var oldNativeAd = new object();
var currentNativeAd = new object();
var oldBinding = new SketchNativeAdBinding(oldNativeAd, 1);
Check(oldBinding.Matches(oldNativeAd, oldNativeAd, 1), "active native ad generation accepts its event");
Check(!oldBinding.Matches(oldNativeAd, currentNativeAd, 2), "destroyed native ad cannot target replacement generation");
Check(!oldBinding.Matches(currentNativeAd, currentNativeAd, 1), "generation mismatch rejects event for same request slot");
Check(!SketchAdMobConfig.HasAnyAdUnit(new SketchAdMobConfig()), "application id alone is not an ad unit");
var blocked = new TestProvider(false);
SketchSdk.Initialize(blocked);
int blockedRewardCallbacks = 0, blockedCloseCallbacks = 0;
Check(!SketchSdk.ShowRewarded(_ => blockedRewardCallbacks++), "not-ready rewarded show should reject");
Check(blockedRewardCallbacks == 0, "rejected rewarded show must not invoke callbacks");
Check(!SketchSdk.ShowInterstitial(() => blockedCloseCallbacks++), "not-ready interstitial show should reject");
Check(!SketchSdk.ShowAppOpen(() => blockedCloseCallbacks++), "not-ready app-open show should reject");
Check(blockedCloseCallbacks == 0, "rejected fullscreen shows must not invoke callbacks");

var duplicate = new TestProvider(true) { DuplicateRewardCallbacks = true, DuplicateCloseCallbacks = true };
SketchSdk.Initialize(duplicate);
int rewardCallbacks = 0, closeCallbacks = 0;
Check(SketchSdk.ShowRewarded(_ => rewardCallbacks++), "ready rewarded show should start");
Check(rewardCallbacks == 1, "duplicate provider reward callbacks are coalesced");
Check(SketchSdk.ShowInterstitial(() => closeCallbacks++), "ready interstitial show should start");
Check(closeCallbacks == 1, "duplicate interstitial close callbacks are coalesced");
Check(SketchSdk.ShowAppOpen(() => closeCallbacks++), "ready app-open show should start");
Check(closeCallbacks == 2, "duplicate app-open close callbacks are coalesced");

var deferred = new TestProvider(true) { HoldFullscreen = true };
SketchSdk.Initialize(deferred);
int deferredRewardCallbacks = 0, deferredCloseCallbacks = 0;
Check(SketchSdk.ShowRewarded(_ => deferredRewardCallbacks++), "first deferred fullscreen show should reserve the slot");
Check(!SketchSdk.ShowAppOpen(() => deferredCloseCallbacks++), "app-open cannot overlap pending rewarded");
Check(!SketchSdk.ShowInterstitial(() => deferredCloseCallbacks++), "interstitial cannot overlap pending rewarded");
Check(deferredRewardCallbacks == 0 && deferredCloseCallbacks == 0, "rejected overlapping formats have no callbacks");
deferred.FinishReward(false);
Check(deferredRewardCallbacks == 1, "reward failure completes and releases the slot");
Check(SketchSdk.ShowAppOpen(() => deferredCloseCallbacks++), "app-open can start after failed reward completes");
Check(!SketchSdk.ShowRewarded(_ => deferredRewardCallbacks++), "reward cannot overlap pending app-open");
deferred.FinishClose();
Check(deferredCloseCallbacks == 1, "app-open close releases the slot once");
Check(SketchSdk.ShowInterstitial(() => deferredCloseCallbacks++), "interstitial can start after app-open completes");
deferred.FinishClose();
Check(deferredCloseCallbacks == 2, "interstitial close callback completes once");
var deferredReplacement = new TestProvider(true);
int deferredReplacementCalls = 0;
SketchSdk.Initialize(deferred, result => { if (!result) deferredReplacementCalls++; });
Check(SketchSdk.ShowInterstitial(() => closeCallbacks++), "fullscreen operation starts before replacement test");
SketchSdk.Initialize(deferredReplacement, result => { if (!result) deferredReplacementCalls++; });
Check(ReferenceEquals(SketchSdk.Provider, deferred) && deferredReplacementCalls == 1,
    "provider replacement is rejected while fullscreen operation is active");
deferred.FinishClose();
SketchSdk.Initialize(deferredReplacement);
Check(ReferenceEquals(SketchSdk.Provider, deferredReplacement), "provider replacement succeeds after fullscreen operation closes");
var deferredNativeReplacement = new TestProvider(true) { HoldNative = true };
SketchSdk.Initialize(deferredNativeReplacement);
Check(SketchSdk.ShowNativeInRect(new SketchNativeRequest("native", new UnityEngine.Rect(0, 0, 100, 100))), "native operation starts before replacement test");
int nativeReplacementCalls = 0;
var afterNativeProvider = new TestProvider(true);
SketchSdk.Initialize(afterNativeProvider, result => { if (!result) nativeReplacementCalls++; });
Check(ReferenceEquals(SketchSdk.Provider, deferredNativeReplacement) && nativeReplacementCalls == 1,
    "provider replacement is rejected while native operation is active");
SketchSdk.HideNative();
SketchSdk.Initialize(afterNativeProvider);
Check(ReferenceEquals(SketchSdk.Provider, afterNativeProvider), "provider replacement succeeds after native operation closes");
var sameProviderPending = new TestProvider(true) { HoldInitialization = true };
int firstInitCallback = 0, secondInitCallback = 0;
SketchSdk.Initialize(sameProviderPending, _ => firstInitCallback++);
SketchSdk.Initialize(sameProviderPending, _ => secondInitCallback++);
sameProviderPending.FinishInitialization(true);
Check(firstInitCallback == 1 && secondInitCallback == 1, "all concurrent same-provider init callbacks complete once");

var replacedPending = new TestProvider(true) { HoldInitialization = true };
var replacementImmediate = new TestProvider(true);
int oldGenerationCount = 0, newGenerationCount = 0;
bool oldGenerationResult = true, newGenerationResult = false;
SketchSdk.Initialize(replacedPending, result => { oldGenerationCount++; oldGenerationResult = result; });
SketchSdk.Initialize(replacementImmediate, result => { newGenerationCount++; newGenerationResult = result; });
Check(oldGenerationCount == 1 && !oldGenerationResult && newGenerationCount == 1 && newGenerationResult,
    "provider replacement settles old waiters false and new waiters true");
replacedPending.FinishInitialization(true);
Check(oldGenerationCount == 1 && !oldGenerationResult && newGenerationCount == 1 && newGenerationResult,
    "late old-provider result is ignored after replacement");
var throwingNative = new TestProvider(true) { ThrowNativeAfterShown = true };
SketchSdk.Initialize(throwingNative);
int rejectedNativeCallbacks = 0;
Check(!SketchSdk.ShowNativeInRect(new SketchNativeRequest("native", new UnityEngine.Rect(0, 0, 100, 100)),
    new SketchNativeCallbacks { OnShown = () => rejectedNativeCallbacks++, OnClosed = _ => rejectedNativeCallbacks++ }),
    "native show that throws is rejected");
Check(rejectedNativeCallbacks == 0, "rejected native show flushes no provider callbacks");

var pendingNative = new TestProvider(true) { HoldNative = true };
SketchSdk.Initialize(pendingNative);
SketchSdk.NativeAttachTimeoutSeconds = 10f;
UnityEngine.Time.realtimeSinceStartupAsDouble = 0;
int nativeClosed = 0;
var nativeRequest = new SketchNativeRequest("native", new UnityEngine.Rect(0, 0, 100, 100)) { AttachTimeoutSeconds = 0.1f };
Check(SketchSdk.ShowNativeInRect(nativeRequest, new SketchNativeCallbacks { OnClosed = _ => nativeClosed++ }), "native request starts");
UnityEngine.Time.realtimeSinceStartupAsDouble = 0.2;
UnityEngine.Application.RaiseBeforeRender();
Check(nativeClosed == 1, "native request-specific attach timeout overrides global timeout");
var pending = new TestProvider(true) { HoldInitialization = true };
SketchSdk.Initialize(pending);
var replacement = new TestProvider(true);
SketchSdk.Initialize(replacement);
pending.FinishInitialization(false);
Check(SketchSdk.IsInitialized && ReferenceEquals(SketchSdk.Provider, replacement), "stale provider initialization cannot overwrite replacement state");
Check(firstInitCallback == 1 && secondInitCallback == 1, "completed init callbacks are not repeated after provider replacement");

var bannerProvider = new BannerTestProvider();
int bannerEvents = 0;
float lastBannerHeight = -1f;
Action<float> bannerListener = height => { bannerEvents++; lastBannerHeight = height; };
SketchSdk.BannerLayoutChanged += bannerListener;
Check(SketchSdk.BannerHeightPixels == 0f, "banner occupancy is zero before initialization");
SketchSdk.Initialize(bannerProvider);
Check(SketchSdk.BannerHeightPixels == 0f, "banner occupancy is zero when not requested");
SketchSdk.ShowBanner();
Check(SketchSdk.BannerHeightPixels == 0f, "banner occupancy is zero while requested but loading");
bannerProvider.SetLoadedHeight(88f);
Check(SketchSdk.BannerHeightPixels == 88f, "loaded visible banner reports measured height");
Check(bannerEvents == 1 && lastBannerHeight == 88f, "first positive occupancy is forwarded once");
bannerProvider.SetLoadedHeight(88f);
Check(bannerEvents == 1, "duplicate occupancy is suppressed");
bannerProvider.SetLoadedHeight(0f);
Check(SketchSdk.BannerHeightPixels == 0f && bannerEvents == 2 && lastBannerHeight == 0f,
    "load failure clears visible occupancy and publishes zero once");
bannerProvider.SetLoadedHeight(88f);
Check(SketchSdk.BannerHeightPixels == 88f && bannerEvents == 3, "a later successful load restores measured occupancy");
SketchSdk.HideBanner();
Check(SketchSdk.BannerHeightPixels == 0f && bannerEvents == 4 && lastBannerHeight == 0f,
    "hide clears occupancy and publishes zero once");
SketchSdk.ShowBanner();
bannerProvider.SetLoadedHeight(88f);
Action<float> queuedStaleBannerCallback = bannerProvider.CaptureLayoutCallback();
var bannerReplacement = new BannerTestProvider();
SketchSdk.Initialize(bannerReplacement);
Check(SketchSdk.BannerHeightPixels == 0f && lastBannerHeight == 0f, "provider replacement clears prior occupancy");
int eventsAfterReplacement = bannerEvents;
queuedStaleBannerCallback?.Invoke(120f);
Check(SketchSdk.BannerHeightPixels == 0f && bannerEvents == eventsAfterReplacement,
    "queued stale provider callback cannot change replacement occupancy");
bannerProvider.SetLoadedHeight(120f);
Check(SketchSdk.BannerHeightPixels == 0f && bannerEvents == eventsAfterReplacement,
    "stale provider callback cannot change replacement occupancy");
SketchSdk.ShowBanner();
bannerReplacement.SetLoadedHeight(float.NaN);
Check(SketchSdk.BannerHeightPixels == 0f, "non-finite banner measurement is rejected");
bannerReplacement.SetLoadedHeight(float.PositiveInfinity);
Check(SketchSdk.BannerHeightPixels == 0f, "infinite banner measurement is rejected");
bannerReplacement.SetLoadedHeight(0f);
Check(SketchSdk.BannerHeightPixels == 0f, "zero banner measurement is rejected");
bannerReplacement.SetLoadedHeight(-2f);
Check(SketchSdk.BannerHeightPixels == 0f, "negative banner measurement is rejected");
bannerReplacement.SetLoadedHeight(64f);
Check(SketchSdk.BannerHeightPixels == 64f, "valid measurement after invalid values is accepted");
bannerReplacement.SetLoadedHeight(76f);
Check(SketchSdk.BannerHeightPixels == 76f && lastBannerHeight == 76f,
    "updated measured size replaces prior visible occupancy");
int eventsBeforeReset = bannerEvents;
typeof(SketchSdk).GetMethod("ResetStatics", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
Check(SketchSdk.BannerHeightPixels == 0f && bannerEvents == eventsBeforeReset + 1 && lastBannerHeight == 0f,
    "subsystem reset hides occupancy and publishes zero once");
int eventsAfterReset = bannerEvents;
bannerReplacement.SetLoadedHeight(90f);
Check(bannerEvents == eventsAfterReset, "reset detaches provider and facade listeners");
var afterResetProvider = new BannerTestProvider();
SketchSdk.Initialize(afterResetProvider);
SketchSdk.ShowBanner();
afterResetProvider.SetLoadedHeight(90f);
Check(SketchSdk.BannerHeightPixels == 90f && bannerEvents == eventsAfterReset,
    "post-reset provider works without retaining stale listeners");

var legacyBannerProvider = new LegacyBannerTestProvider();
SketchSdk.Initialize(legacyBannerProvider);
SketchSdk.ShowBanner();
legacyBannerProvider.SetHeight(52f);
Check(SketchSdk.BannerHeightPixels == 52f,
    "legacy banner provider retains queried height without the optional notifier");
legacyBannerProvider.SetHeight(float.NaN);
Check(SketchSdk.BannerHeightPixels == 0f, "legacy non-finite height is rejected");
legacyBannerProvider.SetHeight(float.PositiveInfinity);
Check(SketchSdk.BannerHeightPixels == 0f, "legacy infinite height is rejected");
legacyBannerProvider.SetHeight(-1f);
Check(SketchSdk.BannerHeightPixels == 0f, "legacy negative height is rejected");
SketchSdk.HideBanner();
Check(SketchSdk.BannerHeightPixels == 0f, "legacy provider hide clears queried height");

var throwingSubscriberProvider = new BannerTestProvider();
SketchSdk.Initialize(throwingSubscriberProvider);
SketchSdk.ShowBanner();
int healthySubscriberCalls = 0;
Action<float> throwingSubscriber = _ => throw new InvalidOperationException("subscriber failure");
Action<float> healthySubscriber = _ => healthySubscriberCalls++;
SketchSdk.BannerLayoutChanged += throwingSubscriber;
SketchSdk.BannerLayoutChanged += healthySubscriber;
try { throwingSubscriberProvider.SetLoadedHeight(44f); }
catch (InvalidOperationException) { }
Check(healthySubscriberCalls == 1, "throwing banner subscriber does not block later listeners");
SketchSdk.BannerLayoutChanged -= throwingSubscriber;
SketchSdk.BannerLayoutChanged -= healthySubscriber;

var reentrantProvider = new BannerTestProvider();
SketchSdk.Initialize(reentrantProvider);
SketchSdk.ShowBanner();
var reentrantReplacement = new BannerTestProvider();
int reconfigureHealthyCalls = 0;
int staleLaterCalls = 0;
Action<float> reconfiguringSubscriber = height => { if (height == 33f) SketchSdk.Initialize(reentrantReplacement); };
Action<float> healthyReconfiguringSubscriber = height => { if (height == 33f) reconfigureHealthyCalls++; };
Action<float> staleLaterSubscriber = height => { if (height == 33f) staleLaterCalls++; };
SketchSdk.BannerLayoutChanged += healthyReconfiguringSubscriber;
SketchSdk.BannerLayoutChanged += reconfiguringSubscriber;
SketchSdk.BannerLayoutChanged += staleLaterSubscriber;
reentrantProvider.SetLoadedHeight(33f);
Check(reconfigureHealthyCalls == 1 && staleLaterCalls == 0 && SketchSdk.BannerHeightPixels == 0f,
    "provider replacement during notification stops stale event delivery");
SketchSdk.BannerLayoutChanged -= healthyReconfiguringSubscriber;
SketchSdk.BannerLayoutChanged -= reconfiguringSubscriber;
SketchSdk.BannerLayoutChanged -= staleLaterSubscriber;

Console.WriteLine($"PASS: {assertions} assertions");

sealed class BannerTestProvider : ISketchAdsProvider, ISketchBannerAdsProvider, ISketchBannerLayoutEvents
{
    public event Action<float> BannerLayoutChanged;
    bool _requested;
    float _height;
    public void Initialize(Action<bool> callback) => callback?.Invoke(true);
    public bool IsReady(SketchAdFormat format) => true;
    public void ShowRewarded(Action<bool> callback) => callback?.Invoke(true);
    public void ShowInterstitial(Action callback) => callback?.Invoke();
    public void ShowAppOpen(Action callback) => callback?.Invoke();
    public bool IsBannerReady => true;
    public float BannerHeightPixels => _requested && _height > 0 ? _height : 0f;
    public void ShowBanner() { _requested = true; BannerLayoutChanged?.Invoke(BannerHeightPixels); }
    public void HideBanner() { _requested = false; BannerLayoutChanged?.Invoke(0f); }
    public void SetLoadedHeight(float height) { _height = height; if (_requested) BannerLayoutChanged?.Invoke(BannerHeightPixels); }
    public Action<float> CaptureLayoutCallback() => BannerLayoutChanged;
}

sealed class LegacyBannerTestProvider : ISketchAdsProvider, ISketchBannerAdsProvider
{
    bool _requested;
    float _height;
    public void Initialize(Action<bool> callback) => callback?.Invoke(true);
    public bool IsReady(SketchAdFormat format) => true;
    public void ShowRewarded(Action<bool> callback) => callback?.Invoke(true);
    public void ShowInterstitial(Action callback) => callback?.Invoke();
    public void ShowAppOpen(Action callback) => callback?.Invoke();
    public bool IsBannerReady => true;
    public float BannerHeightPixels => _requested ? _height : 0f;
    public void ShowBanner() => _requested = true;
    public void HideBanner() => _requested = false;
    public void SetHeight(float height) => _height = height;
}

sealed class TestProvider : ISketchAdsProvider, ISketchBannerAdsProvider, ISketchNativeAdsProvider, ISketchAdCloseCapability
{
    readonly bool _ready;
    Action<bool> _initialization;
    public bool DuplicateRewardCallbacks { get; set; }
    public bool DuplicateCloseCallbacks { get; set; }
    public bool HoldInitialization { get; set; }
    public bool HoldFullscreen { get; set; }
    public bool HoldNative { get; set; }
    public bool ThrowNativeAfterShown { get; set; }
    SketchNativeCallbacks _nativeCallbacks;
    Action<bool> _rewardCompletion;
    Action _closeCompletion;
    public TestProvider(bool ready) => _ready = ready;
    public void Initialize(Action<bool> callback) { if (HoldInitialization) _initialization = callback; else callback?.Invoke(true); }
    public void FinishInitialization(bool success) => _initialization?.Invoke(success);
    public bool IsReady(SketchAdFormat format) => _ready;
    public void ShowRewarded(Action<bool> callback) { if (HoldFullscreen) { _rewardCompletion = callback; return; } callback?.Invoke(true); if (DuplicateRewardCallbacks) callback?.Invoke(true); }
    public void FinishReward(bool result) { var callback = _rewardCompletion; _rewardCompletion = null; callback?.Invoke(result); }
    public void ShowInterstitial(Action callback) { if (HoldFullscreen) { _closeCompletion = callback; return; } callback?.Invoke(); if (DuplicateCloseCallbacks) callback?.Invoke(); }
    public void ShowAppOpen(Action callback) { if (HoldFullscreen) { _closeCompletion = callback; return; } callback?.Invoke(); if (DuplicateCloseCallbacks) callback?.Invoke(); }
    public void FinishClose() { var callback = _closeCompletion; _closeCompletion = null; callback?.Invoke(); }
    public bool IsBannerReady => _ready;
    public float BannerHeightPixels => 0;
    public void ShowBanner() { }
    public void HideBanner() { }
    public bool IsNativeReady => _ready;
    public bool ShowNativeInRect(SketchNativeRequest request, SketchNativeCallbacks callbacks) { if (ThrowNativeAfterShown) { callbacks.Shown(); throw new InvalidOperationException("provider failure"); } if (HoldNative) { _nativeCallbacks = callbacks; return true; } return false; }
    public void HideNative() { var callbacks = _nativeCallbacks; _nativeCallbacks = null; callbacks?.Closed(SketchNativeCloseReason.Teardown); }
    public bool SupportsInAdClose => false;
}
