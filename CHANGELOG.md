# Changelog

## 1.4.0
- Native in-rect on Android uses an own Java layout (Plugins/Android/SketchAdmobNative.java): full / half / in-rect modes, round countdown-ring close button (26 dp, 6 dp inside the top corner, 3 s ring, top-left on every 4th show), the ad stays until the player closes it.
- Editor fake native view draws the same layout and close ring; menu toggle under the Sketch SDK fake menu.
- New SketchNativeClose rules with tests; the runtime asmdef now references UnityEngine.UI.

## 1.3.3
- Splash defaults Bounce-style: minimum 5 s loading, 8 s hard cap, so the cold-start App Open has time to load and shows before the game scene.
- AppOpenOnColdStart defaults to true; interstitial start delay defaults to 30 s (time-only pacing, 30 s cooldown).

## 1.3.2
- Splash: a transparent bar colour hides the whole bar group (track too), for games whose own loading screen follows.

## 1.3.1
- Test assemblies compile only when com.unity.test-framework is installed (SKETCHSDK_TEST_FRAMEWORK).
- Installer adds com.unity.ugui when missing (splash needs it). Found by a clean-project install smoke test.

## 1.3.0 - 2026-11-21

- Added the installer package `com.sketch.sdk.installer` (`?path=/Installer`): Bounce-style copy of the SDK into `Assets/SketchSdk` with a manifest (`.sketch-install.txt`: stamp, owned files, SHA1). Update check against the newest `vX.Y.Z` tag on a background thread, Install/Update dialog with a session-scoped *Later*, hash check that lists and backs up locally edited SDK files, deletion limited to owned files the new version dropped, settings and splash hook never touched. Archive download with `git clone --depth 1` fallback. Removes an old `com.sketch.sdk` UPM entry and resolves packages. Menus **Check For Updates**, **Reinstall Current**, **Install Version...**.
- Added the SDK splash (`Splash/`, assembly `SketchSdk.Splash`): `SketchSplashController` (brand -> SDK start -> loading bar -> next scene) gated on consent, remote config, preloads, ready checks and the next scene load, each with a timeout, minimum duration and a 4 s hard cap; exceptions skip straight to the next scene. Cold-start app open via `SketchAds.ShowColdStartAppOpen`. Game customization only through the `SketchSplashHook` asset (`Assets/Resources/SketchSplashHook.asset`), `ISketchSplashPreload` and `SketchSplashRegistry`. Menu **Sketch SDK > Build Splash Scene** builds the scene idempotently and sets it as build index 0.
- Added the **IAP service** `SketchStore` (Runtime/Iap): `SketchIapCatalog` asset (Consumable / NonConsumable / Subscription, per-store ids, payout tag, fallback price, remove-ads flag, duplicate-id validation), `Purchase(id, onResult, placement)` with a once-only `SketchIapResult`, `Restore`, `IsOwned`, `Price` (localized, else catalog fallback), `RetryPending`, `Granted(product, transactionId, isRestore)`. Grant order: pending order -> game fulfillment returns true (durable) -> `ISketchEntitlementStore` record -> store confirmation; transaction-id dedup (no double grant on re-delivery), durable products re-granted on restore only when the local save lost them, remove-ads products call `SketchAds.SetAdsRemoved(true)` on grant / restore / init. Funnel through the existing `SketchIap` telemetry (start / success with receipt / failed), optional `ISketchIapTelemetry` receipt capture. `SketchEditorFakeStore` (Success, Fail, Cancel, DeferredThenSuccess; fake prices from the fallback price) is used automatically in the Editor.
- Added assembly `SketchSdk.UnityIap` (auto when `com.unity.purchasing` >= 5.5.0, define `SKETCHSDK_UNITY_IAP`): `SketchUnityIapProvider` on Unity IAP 5.5 `PurchasingController` (connect, fetch products, purchase, pending orders confirmed only after the durable grant, unfinished orders re-delivered on launch, restore + purchase history for durable products, deferred / failed / cancelled mapping, receipt and localized price in telemetry).
- Added `ISketchAdsRemovedStore` + `SketchAds.UseAdsRemovedStore(store)` so a game keeps the remove-ads flag in its own save; PlayerPrefs (`sketchsdk.remove_ads`) stays the default for backward compatibility.
- Added **local notifications** `SketchNotifications` (Runtime/Notifications): `Configure(specs)`, `RequestPermissionOnce(done)` (call at a happy moment, never at boot), re-engagement set scheduled on focus loss / quit and everything cancelled on resume. Pure rule `SketchNotificationSchedule` (last activity + delay, quiet hours 22:00-08:00 shifted to the morning, past / duplicate / invalid entries dropped, ordered). Remote kill switch `sketch_notifications_enabled`. Assembly `SketchSdk.MobileNotifications` (auto when `com.unity.mobile.notifications` >= 2.0.0, define `SKETCHSDK_NOTIFICATIONS`): Android channel, POST_NOTIFICATIONS via `PermissionRequest`, schedule, cancel; iOS no-op stub. Editor fake logs schedules.
- Added **native review** `SketchReview` (Runtime/Review): `RecordWin`, `NeverAskAgain`, `CanRequest`, `TryRequest(moment, done)` behind the pure `SketchReviewGate` (min sessions, min wins, once per app version, cooldown days, never-again); falls back to `SketchRating.OpenStorePage` when the native flow is missing or fails. State through injectable `ISketchKeyValueStore` (default `SketchPlayerPrefsStore`, SDK-internal keys `sketchsdk.review_*`). Assembly `SketchSdk.PlayReview` (auto when `com.google.play.review` >= 1.8.0, define `SKETCHSDK_PLAY_REVIEW`).
- Added **in-app updates** `SketchAppUpdate` (Runtime/AppUpdate): `CheckOnStart()` flexible update after remote config is ready, immediate update when the installed versionCode is below remote `sketch_force_update_min_version_code` (re-offered on resume when cancelled), `OnFlexibleDownloaded(complete => ...)` for a "Restart to update" button. Pure rule `SketchAppUpdatePolicy`. Assembly `SketchSdk.PlayAppUpdate` (auto when `com.google.play.appupdate` >= 1.8.0, define `SKETCHSDK_PLAY_APPUPDATE`).

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
