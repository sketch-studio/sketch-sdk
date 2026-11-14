# Changelog

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
