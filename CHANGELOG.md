# Changelog

## 1.5.14
- SketchNativeFit.TopBand: portrait top band above a given Y (loading bar) with equal padding; zero rect when too low.
- SketchAds.MoveNative / SketchSdk.MoveNative (ISketchNativeMoveCapability): move or resize the native that is on screen without a new impression. Android custom layout updates its window; the Editor fake moves its panel. SketchAds.IsNativeShowing.
- Android native close: the close control always takes its own touches. During the countdown a tap on it does nothing instead of falling through to the ad (AdMob native policy: the dismiss control must not cause invalid clicks).

## 1.5.13
- Removed the built-in birth-year age screen (SketchAgeGate) and the Show Default Age Gate setting: the SDK never shows a popup asking the player anything except the legally required UMP consent form in regulated regions. Require Age Signal stays opt-in for games that ask the age themselves and call SketchAudience.SetBracket.

## 1.5.12
- Manager > Ads: the Unity Ads adapter row installs com.google.ads.mobile.mediation.unity (the old id ...mediation.unityads does not exist on OpenUPM); new InMobi and BIGO Ads rows.

## 1.5.11
- Setup checklist (Sketch SDK Manager > Consent) reviews every installed AdMob mediation adapter from its Dependencies.xml: Error when older than the version that forwards GDPR (TCF), TFUA and COPPA (Liftoff 7.7.4.2, Unity Ads 4.19.0.1, InMobi 11.3.0.1, BIGO 5.9.0.1), Warning for networks the SDK has not reviewed.
- Policy rows: missing privacy policy URL (Error), location permission in the Android manifest (Warning), Require Age Signal without the Firebase ad-signal manifest defaults (Warning).
- iOS reads the GPP keys from NSUserDefaults, so US-state opt-outs reach mediation networks on iOS too.

## 1.5.10
- US-state opt-out forwarding: SketchGpp decodes the GPP string UMP stores (IABGPP_HDR_GppString, IABGPP_GppSID) for US National (v1, v2, v3 header-style) and state sections 8-27; GPC = opt-out; unknown section, version or unreadable string fails safe to opted out.
- An opted-out US user (any age) gets do-not-sell on Liftoff, Unity Ads and BIGO and contextual-only Unity Ads, before MobileAds.Initialize and again after the privacy options form changes consent.
- SketchGppStore reads the keys from Android default SharedPreferences (iOS: not yet).

## 1.5.9
- Mediation privacy as data (SketchMediationPrivacy): calls AdMob adapters do not forward, made before MobileAds.Initialize. Minors / unknown age: Liftoff SetCCPAStatus(false), Unity Ads privacy.consent=false, BIGO SetCcpaConsent(false). Unity Ads Mixed Audience: user.nonbehavioral per player (true for minors, false for adults). InMobi needs no call (adapter forwards TFCD/TFUA/COPPA, reads the IAB US Privacy string).
- A failing call on an installed adapter logs an error and sends mediation_privacy_failed; missing adapters are skipped. link.xml keeps the Unity Ads and BIGO adapter APIs.
- Setup checklist: "Age signals" row, and "Mediation privacy" is an Error when an installed adapter lost a privacy API.

## 1.5.8
- Age signals for mixed-audience games (Settings > Require Age Signal): consent and ads wait until the age bracket is known; an unknown age is treated as a minor, never as an adult.
- Neutral birth-year age screen (SketchAgeGate) and SketchAudience API (SetBracket, UseStore for the game save, Changed).
- Age-derived AdMob tags: under 13 TFCD + TFUA + G; 13-15 TFUA; minors npa with a separate Minor Max Content Rating (default T); UMP gets the matching TFUA.
- Minors: Firebase Consent Mode ad_user_data / ad_personalization denied; Liftoff (AdMob mediation) SetCCPAStatus(false) by reflection, adapters kept by link.xml.

## 1.5.7
- AdMob load failures log one [SketchAds][LoadFailed] line per request: Google error constant, domain, message, cause, response id, every mediation adapter tried (source, class, latency, error) and a hint, plus a per-format loaded/failed tally (SketchAds.LoadSummary).
- ad_load_failed analytics now carries response_id and a compact adapters list; the Android custom native loader sends the same detail.
- Effective AdMob request configuration is logged at init.
- Development builds: hold four fingers for two seconds to open AdMob Ad Inspector (SketchAds.OpenAdInspector). Compiled out of release builds.

## 1.5.6
- Detect manually imported AdMob mediation adapters before installing a duplicate through Package Manager.
- Cover concurrent consent refresh and duplicate provider callbacks in PlayMode.

## 1.5.5
- Allow games to preserve the interstitial cooldown when a rewarded ad closes. App-open spacing still accounts for fullscreen ads.

## 1.5.4
- Expose Unity IAP entitlement checks for game-owned refund handling.
- Keep mediation adapter removal confirmation in the Manager UI.

## 1.5.3
- Optional legacy FCM registration tokens for existing game backends.
- Register a custom consent provider with standalone ads or use the built-in UMP adapter with AdMob.
- Refresh consent from the game through the same flow as automatic retry.

## 1.5.2
- Preload, show, hide and release AdMob banner slots individually or as a group. Show and hide also accept the configured slot index.
- Verify that the app-open count survives reloading the game's store and resets at the configured threshold.
- Keep remove-ads PlayMode checks isolated from the saved entitlement.

## 1.5.1
- Named AdMob banners support separate platform IDs, size, position and adaptive width.
- Optional app-open cadence and presentation delay. The game can supply durable storage for the counter.
- Batch installer takes a specific tag and backs up conflicting files outside the imported Assets tree.

## 1.5.0
- Optional standalone LevelPlay, AppLovin MAX and Liftoff Monetize providers, selected per game and configured per platform.
- Per-network controls in SDK Manager for AdMob mediation adapter installation, updates and removal.
- Optional Firebase Messaging, iOS tracking authorization, native iOS reviews and local notifications, and Google Play receipt validation.
- Git installer includes SDK plugins and preserves game settings, splash hooks and the generated splash scene.
- Verified through Unity Editor tests and Android Player script compilation. Native device delivery and full MMP2 behavior comparison are still in progress.

## 1.4.6
- Sync of fixes made in a game's embedded copy back to the source repo (from now on SDK changes are made here and consumed via Package Manager).
- Native in-rect close: configurable countdown `SketchSdkSettings.NativeCloseDelaySeconds` (remote `sketch_native_close_delay`, 0 = tappable at once), `SketchNativeClose.CountdownSeconds`/`ResolveCountdown`; Editor fake view and AdMob provider use it.
- Analytics: Development Builds send to `SketchSdkSettings.DevServerUrl` only and never to Firebase (`SketchAnalyticsRouting.ServerUrl`/`ExternalSinksAllowed`, OR-873).
- Consent: `SketchSdkSettings.PrivacyPolicyUrl`, `SketchConsent.HasPrivacyEntry`, `SketchConsent.OpenPrivacy` (UMP privacy options where required, else the policy page).
- Editor fake store: transaction ids unique per session (no "Transaction ID was already used" pending purchases).
- Splash builder: CanvasScaler Expand, match width.
- Tests: ad audio pause, analytics routing, native close countdown.

## 1.4.5
- Editor never sends analytics, revenue or IAP to the server or Firebase (owner OR-818): `#if UNITY_EDITOR` only the debug sink runs and always prints `[SketchSdk][Editor only, not sent]` per event; device builds log normally. Test real logging with a device build.
- Server `appVersion` is now the bundle version code (1, 2, 3...) via `SketchAppVersion.Code` (OR-820), baked into `SketchSdkSettings.BuildVersionCode` by `SketchBuildVersionBaker` before every build; `sketch_session_start` carries `app_version` (code), `version_name` and `sdk_version`. Dashboard From/To Version filter uses plain integers.

## 1.4.4
- **Sketch SDK Manager** window (menu Sketch SDK > Manager, Settings kept as alias), Bounce Manager style: tabs Overview, Ads, IAP, Analytics, Remote Config, Server, Splash, Consent. Every settings field is drawn with a short help line and its remote key; edits go through SerializedObject (Undo, nothing written when only viewed). Header shows version and READY / error / warning badges, tabs with problems are marked.
- Setup checklist (`SketchSetupChecklist.Build(settings, facts)`, pure, tested; facts from `SketchSetupScanner`): settings location, Google Mobile Ads + UMP, SKETCHSDK_ADMOB, App ID and every ad unit per platform (format, Google test ids), test ads off in release, SketchIapCatalog (+ validation), Unity IAP package, `IAPProductCatalog.json`, BILLING permission, Firebase plugin, `google-services.json`, `FirebaseApp.androidlib`, splash at build index 0, Sketch server credentials, consent. Each row OK / INFO / WARN / ERROR with a one-line fix.
- Ads tab: Android / iOS id editor with a per-field badge, pacing with remote key hints, remove-ads scope as checkboxes (rewarded never blocked). IAP tab: product table. Server tab: API key masked with Show / Copy, Open dashboard. Splash tab: build scenes, hook inspector, Build Splash Scene.
- Remote Config tab: every key (SDK + game + free-form defaults) with type, default, description and an Editor-only override column (top layer `SketchConfigValues.SetOverrides`, stored per project in EditorPrefs, `SketchRemoteConfig.ApplyEditorOverrides()` applies it live in Play). Export: Firebase Remote Config template JSON (Publish from a file), Android defaults XML, Sketch server JSON.
- `SketchRemoteConfig` is `partial`: `SketchRemoteConfig.Keys.cs` lists SDK keys (`SdkKeys(settings)`, `SketchRemoteKeyDef` with type / default / description) and typed accessors (`InterstitialCooldownSeconds`, `AppOpenOnResume`, `NotificationsEnabled`, `ForceUpdateMinVersionCode`, ...). Games declare their keys in `Assets/SketchDev/SketchRemoteConfig.Dev.cs` (`RegisterGameKeys`), created ONCE with `SketchDev.asmref` (compiles into SketchSdk.Runtime) and never overwritten by SDK updates; declared defaults become config defaults.
- Tests `SketchSetupChecklistTests`, `SketchRemoteKeysTests` (incl. a guard that the Manager draws every settings field).

## 1.4.3
- Analytics routing (`SketchAnalyticsRoute`, per-event sink mask): `SketchAnalytics.Log` reaches every sink except Firebase; Firebase receives revenue only (`ad_impression` from every AdMob paid event, `iap_start`, `iap_success`, `reach_rev_*`). `SketchAnalytics.LogTo(route, ...)` for explicit routes; sinks declare their route with `ISketchRoutedSink` (server, Firebase, debug), undeclared sinks count as `Other`.
- `SketchAnalyticsSchema`: Firebase name/param limits validator, GA4 `ad_impression` params (ad_platform, ad_source, ad_format, ad_unit_name, currency, value, placement), `iap_success` params (product_id, value, currency, value_usd, placement).
- Bounce parity: remote key `log_enable` (default true) stops events but never revenue; ad lifecycle logs rate-limited to 10 per second; `reach_rev_*` counts ad revenue only and carries `success=1`.
- Server sink: the periodic queue save writes the file on a worker thread (quit / focus-lost saves stay synchronous); session metadata adds `country`, `locale`, `sdk_version`, `first_open`.
- Tests `SketchAnalyticsParityTests`.

## 1.4.2
- Remove-ads scope is a policy setting: `SketchAdsPolicySettings.RemoveAdsBlocks` (`SketchRemoveAdsScope` flags Interstitial, AppOpen, Banner, Native, RewardedInterstitial). Default `SketchRemoveAdsScope.Default` keeps the 1.4.1 behaviour (interstitial, app open, banner, native off; rewarded and RI kept). A game that sells "no forced ads" sets e.g. Interstitial | AppOpen | RewardedInterstitial so banner and native stay. `SketchAds.IsAdsRemovedFor(format)` / `SketchAdsPolicy.IsRemoved(format)`; `SetAdsRemoved(true)` hides the banner / native only when the scope blocks them; remote-config `Apply` keeps the scope. Rewarded is never blocked. Tests `SketchRemoveAdsScopeTests`.
- `IsAdsOffForVersion` is cached per (list, version) string so per-frame readiness checks do not allocate.
- Editor fake provider: `SimulateRewardedNoFill`, `SimulateInterstitialNoFill` for fallback paths.

## 1.4.1
- Splash look hook fields (defaults keep the old look): track/fill sprites (`SketchSlicedFillImage` keeps 9-slice caps round at every fill value), track colour, bar size/anchor/fill inset, brand size/anchor, background aspect-fill on tall screens (`AspectRatioFitter` envelope), optional label prefab in a slot under the bar (e.g. "Loading..." in the game's own font). Builder rebuilds the scene from these fields and reloads the hook after a Single-mode new scene (it was saved with an empty hook reference). EditMode tests `SketchSplashLookTests`.

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
