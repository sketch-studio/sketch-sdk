# Sketch SDK

> **Documentation / Tài liệu:** open the [bilingual product site](docs/index.html) for the complete feature map, architecture, API entry points and integration flow.

One Unity package for mobile monetization, modeled on Bounce SDK but **AdMob only** (no AppLovin MAX):

- **Ads (Google AdMob):** banner, interstitial, rewarded, rewarded interstitial, app open, native-in-rect. Auto reload with back-off, main-thread callbacks, late-reward grace, test ad units in development builds.
- **Pacing (Bounce rules):** interstitial start delay since install, cooldown after any fullscreen ad, session gate, per-placement rules, app open on resume (never right after another ad), remove-ads with a configurable scope (`Pacing.RemoveAdsBlocks`, default: interstitial, app open, banner and native off; rewarded always kept). All overridable from remote config without a new build.
- **Consent:** Google UMP (GDPR / US states) before ads, privacy-options button API. Ads are requested only when UMP allows it; if the form could not be answered (offline, dismissed) ads start after the privacy form or on the next resume, and the game never waits on it.
- **Sketch Server:** analytics events, sessions, ad revenue (per paid impression), IAP revenue, remote config + A/B `configId`. Offline queue on disk, batched, retried.
- **Firebase Analytics (optional):** used automatically when Firebase Analytics is installed (UPM or .unitypackage); add google-services.json / GoogleService-Info.plist from the Firebase console.
- **Editor:** settings window, fake ads in Play Mode (everything ready, rewarded grants), automatic `SKETCHSDK_ADMOB` define, App ID sync into the Google Mobile Ads settings, release-build gate.

Requires Unity 2021.3+ (tested on Unity 6000.3). Ads need the Google Mobile Ads Unity plugin (11.x tested).

## 0. Install like Bounce (recommended, v1.3.0+)

Add only the small installer package; it copies the SDK into `Assets/SketchSdk` and keeps it updated:

1. **Package Manager > + > Add package from git URL**:
   ```
   https://github.com/sketch-studio/sketch-sdk.git?path=/Installer#v1.3.0
   ```
2. On load the installer reads the newest `vX.Y.Z` tag (`git ls-remote`, background thread) and compares it with line 1 of `Assets/SketchSdk/.sketch-install.txt`. A dialog offers **Install** or **Update X -> Y**; *Later* is remembered for this editor session.
3. Install downloads the tag archive, copies `Runtime`, `Editor`, `Tests`, `Splash`, `README.md`, `CHANGELOG.md`, `package.json` (with their `.meta`, so GUIDs stay stable) into `Assets/SketchSdk`, and writes the manifest (stamp + every owned file + SHA1).
4. Safe updates: owned files you edited locally are listed and backed up to `Assets/SketchSdk~backup/<timestamp>/` before they are replaced. Only owned files the new version dropped are deleted. Your files, `SketchSdkSettings` and `SketchSplashHook` are never touched.
5. After the copy: Asset refresh, an old `com.sketch.sdk` entry is removed from `Packages/manifest.json`, packages resolve, the splash scene is built if missing (**Sketch SDK > Build Splash Scene**) and set as build index 0 (other scenes keep their order).

Menus: **Sketch SDK > Check For Updates**, **Reinstall Current**, **Install Version...** (pick any tag), **Build Splash Scene**.

### Splash

`Assets/SketchSdk/Splash/SketchSplash.unity` (build index 0): brand, SDK start, loading bar, then the next scene. It waits for consent, remote config, your preloads and ready checks (each with a timeout) and the minimum time, never longer than the hard cap (4 s by default), and shows the cold-start app open through `SketchAds.ShowColdStartAppOpen`. Customize it **only** through `Assets/Resources/SketchSplashHook.asset` (next scene, timings, brand sprite + size/position, background sprite (aspect-filled on tall screens), title, bar track/fill sprites (9-slice caps kept at every fill), bar size/position/colour, optional label prefab under the bar, preloads). Never edit the SDK scene: it is rebuilt on update. Code hooks: implement `ISketchSplashPreload` (`IEnumerator Preload()`) or call `SketchSplashRegistry.Register(...)` / `RegisterReadyCheck(Func<bool>)` from a `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` method.

## 1. Install (new project, 5 minutes)

1. **Package Manager > + > Add package from git URL**:
   ```
   https://github.com/CodeSketch-dev/sketch-sdk.git
   ```
   Pin a version for production: `https://github.com/CodeSketch-dev/sketch-sdk.git#v1.0.0`.
2. The **Sketch SDK** window opens (menu **Sketch SDK > Manager**: tabs Overview with a green/red setup checklist, Ads, IAP, Analytics, Remote Config, Server, Splash, Consent). If the checklist shows "Google Mobile Ads plugin" in red, use **Sketch SDK > Install Google Mobile Ads** (adds OpenUPM `com.google.ads.mobile`) or import the `.unitypackage` from [googleads-mobile-unity releases](https://github.com/googleads/googleads-mobile-unity/releases).
3. Fill your AdMob ids (AdMob console > Apps):
   - **App Id**: `ca-app-pub-XXXX~YYYY` (Android and iOS separately).
   - **Ad unit ids** `ca-app-pub-XXXX/ZZZZ` for the formats you use. Leave a format blank to turn it off.
4. Optional: **Sketch Server**: tick *Server Enabled*, fill *Server App Id* and *Server Api Key* (dashboard Admin > Apps).
5. Press **Save & Verify**: writes the App Id into Google Mobile Ads settings, sets the define, lists anything missing.
6. Android: *Player Settings > Publishing* enable *Custom Main Gradle Template* (+ properties/settings templates) and run *Assets > External Dependency Manager > Android Resolver > Force Resolve* once (standard Google Mobile Ads setup).

That is all. The SDK starts by itself before the first scene (consent -> ads -> analytics -> remote config).

The settings asset lives in your game at `Assets/SketchSdk/Resources/SketchSdkSettings.asset`. The package never contains ids or keys.

## 2. Use it in the game

```csharp
using Sketch.Sdk;

// Interstitial at a natural break. onClosed ALWAYS runs once (ad or not), so continue the flow there.
SketchAds.ShowInterstitial("level_end", () => LoadNextLevel());

// Rewarded: granted == true only when the reward was earned.
SketchAds.ShowRewarded("double_coins", granted =>
{
    if (granted) coins *= 2;
    else ShowToast("No ad available, try again later");
});
bool canOfferReward = SketchAds.IsRewardedReady;   // enable/disable the reward button

// Rewarded interstitial (natural break, no opt-in button). Google policy: show your own intro screen
// that says what the reward is and lets the player opt out (skip) BEFORE calling this.
if (SketchAds.IsRewardedInterstitialReady)
    ShowIntroWithSkip(onWatch: () => SketchAds.ShowRewardedInterstitial("level_bonus", granted => { if (granted) coins += 50; }));

// Banner (sticky until hidden). Lift bottom UI by the real height:
SketchAds.ShowBanner();
SketchAds.BannerHeightChanged += px => bottomBar.anchoredPosition = new Vector2(0, px / canvas.scaleFactor);

// Remove-ads purchase (persisted). Blocks the formats in SketchSdkSettings Pacing.RemoveAdsBlocks
// (default banner, interstitial, app open, native). Rewarded stays. Query one format: SketchAds.IsAdsRemovedFor(SketchRemoveAdsScope.Interstitial).
SketchAds.SetAdsRemoved(true);

// Pause gameplay while a fullscreen ad is on screen (audio is paused for you).
SketchAds.FullscreenAdChanged += showing => Time.timeScale = showing ? 0 : 1;

// Analytics (Sketch Server + Firebase when present)
SketchAnalytics.Log("level_complete", "level", 12, "time", 41.5f);

// Remote config (server value, else the default typed in Settings)
int lives = SketchRemoteConfig.GetInt("start_lives", 3);
SketchRemoteConfig.Updated += ApplyConfig;

// IAP: after the store confirmed the purchase (the SDK never grants items)
SketchIap.ReportPurchaseStart("gems_100", "shop");      // on buy tap (also blocks the app-open ad on return)
SketchIap.ReportPurchase(new SketchPurchase { ProductId = "gems_100", TransactionId = order.Id,
    Price = 0.99, Currency = "USD", Store = "GooglePlay", Receipt = receipt });

// Settings screen: Google requires a privacy button for EEA/UK users
privacyButton.SetActive(SketchConsent.IsPrivacyOptionsRequired);
privacyButton.onClick.AddListener(() => SketchConsent.ShowPrivacyOptions());

// Rate us
SketchRating.OpenStorePage();
```

Keep one game-side adapter (`AdsHelper`) as the only file that calls `SketchAds`; see the **Quick Start** sample (Package Manager > Sketch SDK > Samples), which also has a splash loader with cold-start app open and an on-device test panel.

### Splash and cold-start app open

`SketchSdkApp.WhenReady(...)` fires after consent + ads init (max ~10 s). The sample `SketchSplashLoader` waits for it and for remote config, calls `SketchAds.ShowColdStartAppOpen(4f, next)` (only shows when *App Open On Cold Start* is on), then loads scene 1.

## IAP

`SketchStore` runs every purchase; the game only grants items. Create the catalog with **Create > Sketch SDK > IAP Catalog** (id, type, Google / Apple store id when different, payout tag, fallback price, *Removes Ads*).

```csharp
// Boot, after the save loaded. entitlements = the game's DataBlock-backed ISketchEntitlementStore.
SketchAds.UseAdsRemovedStore(myAdsRemovedStore);          // optional: remove-ads flag in the game save, not PlayerPrefs
SketchStore.Init(catalog, entitlements, grant =>
{
    // Grant durably and return true. Write the item AND the entitlement record in the same save flush.
    // Return false to leave the order unconfirmed: the store re-delivers it (or call SketchStore.RetryPending()).
    return wallet.TryGrant(grant.Product.PayoutTag, grant.Quantity, "iap:" + grant.TransactionId);
});

priceLabel.text = SketchStore.Price("gems_500");             // localized, else the catalog fallback price
SketchStore.Purchase("gems_500", r =>
{
    if (r.IsSuccess) ShowThanks();                            // Success or AlreadyOwned
    else if (r.Status == SketchIapResultStatus.Deferred) ShowToast("Waiting for approval");
}, "shop");
SketchStore.Granted += (product, transactionId, isRestore) => RefreshShop();   // every grant, also deferred / re-delivered / restored
restoreButton.onClick.AddListener(() => SketchStore.Restore());                 // required on iOS
bool vip = SketchStore.IsOwned("vip");
```

- Grant order: paid order -> fulfillment returns true -> entitlement record -> store confirmation. A transaction is never granted twice; a non-consumable is re-granted on restore only when the local save lost it.
- *Removes Ads* products call `SketchAds.SetAdsRemoved(true)` on grant, restore and init.
- Funnel events go through `SketchIap` (`iap_start`, `iap_success` + revenue, `iap_failed`); restores are not reported as revenue.
- Editor: `SketchEditorFakeStore` is used automatically. `((SketchEditorFakeStore)SketchStore.Provider).Outcome = SketchFakeStoreOutcome.Cancel` exercises the failure paths (`Fail`, `Cancel`, `DeferredThenSuccess`, `SimulatePricesUnavailable`). `SketchStoreProviders.UseRealStoreInEditor = true` uses Unity IAP instead.
- Devices: install `com.unity.purchasing` 5.5+; assembly `SketchSdk.UnityIap` compiles automatically and registers `SketchUnityIapProvider`.

## Retention: notifications, review, in-app update

All three are no-ops when their optional package is missing (Editor uses logging fakes). Packages: `com.unity.mobile.notifications` (Unity registry), `com.google.play.review`, `com.google.play.appupdate` (Google archive / OpenUPM, with `com.google.play.common` + `com.google.play.core`). Each provider assembly turns on by itself when its package is installed.

```csharp
// Boot (once): the re-engagement set. Scheduled when the app goes to background, cancelled on resume.
SketchNotifications.Configure(new[]
{
    new SketchNotificationSpec("d1", "Come back!", "Your daily reward is ready.", TimeSpan.FromHours(24), "icon_0"),
    new SketchNotificationSpec("d3", "We miss you", "New levels are waiting.", TimeSpan.FromDays(3), "icon_0"),
});
// After the first win (never at boot): Android 13+ permission dialog, once per install.
SketchNotifications.RequestPermissionOnce(granted => { });

// Each win, then at a happy moment (or from the Good branch of the game's own rate popup):
SketchReview.RecordWin();
SketchReview.TryRequest("level_win", shown => { });

// First scene: Play in-app update (flexible; immediate when forced remotely).
SketchAppUpdate.OnFlexibleDownloaded(complete => restartButton.Show(complete));
SketchAppUpdate.CheckOnStart();
```

- Quiet hours 22:00-08:00 local: a fire time inside them moves to 08:00 (`SketchNotifications.Schedule = new SketchNotificationSchedule(start, end)` to change). Icons are the ids from Project Settings > Mobile Notifications.
- Review gate defaults (`SketchReview.Settings`): 3 sessions, 3 wins, once per app version, 30 days cooldown, store-page fallback on.
- Remote keys: `sketch_notifications_enabled` (false = cancel + schedule nothing), `sketch_force_update_min_version_code` (installs below this versionCode get the immediate update).
- State keys are SDK-internal PlayerPrefs (`sketchsdk.review_*`, `sketchsdk.notif_permission_asked`); assign `SketchReview.Store` / `SketchNotifications.Store` to keep them in the game save.

## Cross-promotion

Cross-promotion is provider-neutral and driven by the existing Sketch Server remote-config payload. A game supplies an `ISketchCrossPromoCatalog` and a DataBlock-backed `ISketchCrossPromoState` to `SketchCrossPromo`. The SDK rotates valid HTTPS store links by session/open thresholds and emits `cross_promo_impression` and `cross_promo_click`; it never owns wallet, entitlement, persistence, UI, or global time scale.
## 3. Ad pacing and remote config keys

Defaults (Bounce values) are in Settings > Pacing. Override any of them per A/B variant from the Sketch dashboard config:

| Key | Type | Meaning |
|---|---|---|
| `sketch_inter_start_delay` | seconds | no interstitial until this long after install (default 60) |
| `sketch_inter_cooldown` | seconds | gap after any fullscreen ad (default 30) |
| `sketch_ads_off_version` | string | comma-separated app versions (exact match with `Application.version`, e.g. `1.4.0,1.4.1`) that show no banner, interstitial, app open, native or rewarded interstitial; rewarded keeps working. Block reason `ads_off_version`. Empty = off |
| `sketch_inter_min_session` | int | first session allowed to show interstitials |
| `sketch_banner_start_delay` | seconds | banner hidden until this long after install |
| `sketch_app_open_resume` | bool | app open when returning from background |
| `sketch_app_open_cold_start` | bool | app open at launch (splash) |
| `sketch_app_open_start_delay` | seconds | no app open until this long after install |
| `sketch_app_open_min_background` | seconds | minimum time in background before resume app open |
| `sketch_app_open_after_fullscreen` | seconds | no app open this soon after another fullscreen ad (default 30) |
| `sketch_ads_placements` | JSON array | `[{"placement":"level_end","enabled":true,"min_session":2,"cooldown":45}]` |

`SketchAds.ShowInterstitial(placement, onClosed, skipCooldown: true)` ignores only the cooldown. `SketchAds.Policy.LastBlockReason` tells why an ad was skipped.


### Remote Config tab and game keys

Menu **Sketch SDK > Manager > Remote Config** lists every key (SDK keys above, your game keys, free-form Settings defaults) with type, default and description, lets you type an **Editor-only override** per key (never shipped), and exports the defaults as a Firebase Remote Config template, an Android defaults XML or the Sketch server JSON.

Declare game keys in `Assets/SketchDev/SketchRemoteConfig.Dev.cs` (created once by the SDK, yours, never overwritten on update):

```csharp
public static partial class SketchRemoteConfig
{
    static partial void RegisterGameKeys(List<SketchRemoteKeyDef> keys)
    {
        keys.Add(SketchRemoteKeyDef.Int("start_lives", 3, "Lives at the start of a run."));
    }
    public static int StartLives => GetInt("start_lives", 3);
}
```

## 4. Events sent automatically

`sketch_session_start`, `sketch_ads_init`, `sketch_remote_config`, `consent_result`, `ad_loaded`, `ad_load_failed`, `ad_impression`, `ad_clicked`, `ad_opened`, `ad_closed`, `ad_show_failed` (each with `format`, `placement`), `ad_not_ready`, `ad_reward_granted`, `ad_reward_skipped`, `iap_start`, `iap_success`, `iap_failed`, `ads_removed`, `reach_rev_<x>` (lifetime revenue milestones in USD, set in Settings). Ad revenue goes to `POST /revenue/ads` per paid impression, IAP to `POST /revenue/iap`. Keep the AdMob account currency in USD: for another currency the SDK sends `revenueUsd: 0` with the local value in `metadata.local_value` / `local_currency` (no guessed exchange rate).

## 5. Testing

- **Editor Play Mode:** fake provider; every format is ready and rewarded grants. `SketchEditorFakeProvider.SimulateNoFill` / `SimulateRewardSkipped` exercise the failure paths.
- **Development builds:** Google test ad units are used automatically (*Test Ads In Development Build*). Add your device's hashed id (printed in logcat) to *Test Device Ids* for real units on your phone.
- **Consent:** *Consent Debug Geography = EEA* in a development build shows the form outside Europe; `SketchConsent.ResetForTesting()` clears it.
- **Release check:** building Android/iOS without Development fails when the App Id is missing or an id is malformed.

Package tests (add `"testables": ["com.sketch.sdk"]` to the project manifest): 8 EditMode + 8 PlayMode. Pure-logic harness: `dotnet run --project Tests~/SketchSdk.Policy.Tests/SketchSdk.Policy.Tests.csproj`.

## Architecture

```
Game (decides WHEN)  ->  AdsHelper (game adapter, only SDK caller)  ->  SketchAds / SketchAnalytics / SketchRemoteConfig (facades)
                                                                        -> SketchAdsPolicy (pure pacing rules)
                                                                        -> SketchSdk (provider facade: once-only callbacks, fullscreen lock)
                                                                        -> ISketchAdsProvider: SketchSdkAdMobProvider | SketchEditorFakeProvider
SketchSdkApp bootstrap: identity -> analytics sinks (SketchServerClient, Firebase) -> remote config -> UMP consent -> ads
```

Assemblies: `SketchSdk.Runtime` (core, no dependencies), `SketchSdk.AdMob` (needs `SKETCHSDK_ADMOB`, set automatically), `SketchSdk.Firebase` (auto when `com.google.firebase.analytics` is installed), `SketchSdk.Editor`.

No runtime manager GameObject is created: the SDK ticks through a PlayerLoop system.

## Before you publish

- Real ids in Settings, *Force Test Ads* off, build without Development.
- AdMob: app-ads.txt on your developer website, Privacy & messaging GDPR message published (the UMP form is configured there).
- Play Console: Data safety form declares advertising id + analytics. The Google Mobile Ads plugin adds the `AD_ID` permission.
- Test on a real device: consent form, rewarded grant, interstitial close, app open on resume, banner layout.

