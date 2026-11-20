# Sketch SDK

> **Documentation / Tài liệu:** open the [bilingual product site](docs/index.html) for the complete feature map, architecture, API entry points and integration flow.

One Unity package for mobile monetization, modeled on Bounce SDK but **AdMob only** (no AppLovin MAX):

- **Ads (Google AdMob):** banner, interstitial, rewarded, rewarded interstitial, app open, native-in-rect. Auto reload with back-off, main-thread callbacks, late-reward grace, test ad units in development builds.
- **Pacing (Bounce rules):** interstitial start delay since install, cooldown after any fullscreen ad, session gate, per-placement rules, app open on resume (never right after another ad), remove-ads (rewarded keeps working). All overridable from remote config without a new build.
- **Consent:** Google UMP (GDPR / US states) before ads, privacy-options button API. Ads are requested only when UMP allows it; if the form could not be answered (offline, dismissed) ads start after the privacy form or on the next resume, and the game never waits on it.
- **Sketch Server:** analytics events, sessions, ad revenue (per paid impression), IAP revenue, remote config + A/B `configId`. Offline queue on disk, batched, retried.
- **Firebase Analytics (optional):** used automatically when Firebase Analytics is installed (UPM or .unitypackage); add google-services.json / GoogleService-Info.plist from the Firebase console.
- **Editor:** settings window, fake ads in Play Mode (everything ready, rewarded grants), automatic `SKETCHSDK_ADMOB` define, App ID sync into the Google Mobile Ads settings, release-build gate.

Requires Unity 2021.3+ (tested on Unity 6000.3). Ads need the Google Mobile Ads Unity plugin (11.x tested).

## 1. Install (new project, 5 minutes)

1. **Package Manager > + > Add package from git URL**:
   ```
   https://github.com/CodeSketch-dev/sketch-sdk.git
   ```
   Pin a version for production: `https://github.com/CodeSketch-dev/sketch-sdk.git#v1.0.0`.
2. The **Sketch SDK** window opens (menu **Sketch SDK > Settings**). If "Google Mobile Ads: missing", press **Install** (adds OpenUPM `com.google.ads.mobile`) or import the `.unitypackage` from [googleads-mobile-unity releases](https://github.com/googleads/googleads-mobile-unity/releases).
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

// Remove-ads purchase (persisted). Blocks banner, interstitial, app open, native. Rewarded stays.
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

