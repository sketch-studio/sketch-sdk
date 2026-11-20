# Changelog

## 1.2.2 - 2026-11-21

- Added AdMob **rewarded interstitial**: `SketchAds.ShowRewardedInterstitial(placement, onResult)`, `SketchAds.IsRewardedInterstitialReady`, `SketchAds.RewardedInterstitialFinished`, `SketchAdFormat.RewardedInterstitial` (appended). Optional provider capability `ISketchRewardedInterstitialProvider` (implemented by the AdMob provider and the Editor fake); `ISketchAdsProvider` is unchanged and a provider without the capability returns false and invokes nothing. Same reload back-off, late-reward grace, fullscreen lock, main-thread paid revenue and `rewarded_interstitial` telemetry as rewarded. Per-platform `RewardedInterstitial` ad unit field in Settings (Google test units in development builds). Keeps working under remove-ads. Google requires an intro screen with an opt-out before it is shown.
- Added remote switch `sketch_ads_off_version` (Bounce `upAppVersion`): comma-separated app versions (exact, trimmed match against `Application.version`) that show no banner, interstitial, app open, native or rewarded interstitial; rewarded keeps working. Block reason `ads_off_version`. Pure rule `SketchAdsPolicy.IsVersionListed` / `AppVersion`, plus `CanShowNative`, `CanShowRewardedInterstitial`, `CanShowRewarded`.
- Default interstitial spacing `InterstitialCooldownSeconds` 60 -> 30 s (Bounce). Existing Settings assets keep their serialized value; change it there or with `sketch_inter_cooldown`.
- Removed the unreleased interstitial session/daily caps (`InterstitialSessionCap`, `InterstitialDailyCap`, `sketch_inter_session_cap`, `sketch_inter_daily_cap`) by owner decision.

## 1.2.1 - 2026-11-19

- Added Unity metadata for packaged documentation assets so immutable Git package imports stay Console-clean.

## 1.2.0 - 2026-11-19

- Rebuilt the Sketch SDK Settings window with Unity UI Toolkit for a cleaner responsive editor workflow.
- Preserved the existing settings inspector, bilingual toggle, status cards, Save & Verify, asset selection and documentation actions.

## 1.0.0 - 2026-11-15

Full "Bounce-like" SDK, AdMob only.

- Package renamed `com.sketch.sdk.ads` -> `com.sketch.sdk` (update the manifest key; namespaces and assembly names unchanged).
- `SketchSdkSettings` asset + **Sketch SDK > Settings** window, Save & Verify, App Id sync into Google Mobile Ads settings, release build gate, OpenUPM installer for Google Mobile Ads, automatic `SKETCHSDK_ADMOB` define (also in batch mode).
- `SketchSdkApp` auto bootstrap (BeforeSceneLoad): identity, analytics, remote config, UMP consent, ads; each step isolated, ready watchdog.
- `SketchAds` game facade: placements, Bounce pacing (`SketchAdsPolicy`), remove-ads, app open on resume / cold start, sticky banner, audio pause, `FullscreenAdChanged`, `RewardedFinished`.
- AdMob provider: events on the Unity main thread, reload back-off for rewarded / interstitial / banner, late-reward grace, UMP consent provider.
- `SketchServerClient` for sketch-server `/api/v1` (events batch, sessions, ad revenue, IAP revenue, remote config) with a persistent retry queue; `SketchRemoteConfig` with cache and remote pacing keys; `SketchAnalytics` multi-sink hub with revenue milestones; optional Firebase Analytics sink.
- `SketchIap` funnel reporting, `SketchRating`, `SketchSdkLoop` (PlayerLoop tick + main-thread dispatcher), Editor fake provider.
- Removed the bridge to the obsolete `com.sketch.analytics` client (its endpoints do not exist on the server).
- Quick Start sample: splash loader, AdsHelper adapter, on-device test panel.

## 0.2.0

AdMob provider-neutral facade extracted from Dig Egg; banner occupancy events.
