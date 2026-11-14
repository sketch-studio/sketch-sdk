# Bounce SDK study (2026-11-15, read-only subagent)

Source: `thunder-break-egg/break-and-steal-unity`, package `com.bounce.sdk` v1.0.2 (`Library/PackageCache/com.bounce.sdk@d0ed2c0116a8`), installed copy `Assets/BounceSdk`. That game builds `USE_MAX;USE_ADJUST;MAX_USE_ADMOB`; the pure AdMob path is `AdsAdmob.cs`.

## Install / config
- UPM package ships only an Editor installer; the SDK lives in `Source~` and is copied into `Assets/BounceSdk` with a stamp manifest (`.bounce-install.txt`) because it relies on partial classes in Assembly-CSharp. Write-once `Assets/BounceDev/*.Dev.cs` templates.
- Config SOs in `Assets/Resources/BounceSdk/` (`BounceManager` -> `BounceConfig` + `RemoteConfig`). Per-platform AdMob ids (banner, collapsible, inter, native, rewarded, app open) + `Extra` round-robin lists, `admobTestAdsOnDevelopmentBuild`, Android/iOS app ids. "Save & Verify" writes the app id into `GoogleMobileAdsSettings.asset` and manages scripting defines.

## API (BounceAdsSdk / BounceSdk)
- `ShowInterstitial(placement, onClosed, skipTime)` returns started, onClosed always fires; `ShowRewarded(onReward, placement)`; `ShowAppOpen`; `IsRewardedReady`; `CheckShowInterstitialAble(placement)`; `ShowBanner/HideBanner` (sticky wanted flag, deferred by start time); `SetRemoveAds` (rewarded still works); native in-rect.
- `LogEvent(name, params kv)`, `LogAds(type, state, placement)` (rate limited 10/s), `LogAdsRevenue(...)` (+ lifetime revenue and `reach_rev_X` milestones), `LogIAPStart`, `LogIAPSuccess` (server receipt validation first).

## Rules worth copying
- Interstitial gates: remove-ads, per-placement enable / sessionToShow / timeBetween, `timeStart` since first install (default 60), `timeBetween` (default 60) measured from the last inter, rewarded OR app open.
- App open on resume, skipped within 30 s after any fullscreen ad; cold start only on splash. Add 4 h expiry (Bounce lacks it; Sketch provider has it).
- Consent: UMP Update -> LoadAndShowConsentFormIfRequired -> ONE guarded MobileAds.Initialize for every outcome (Thunder_AdmobManager.cs:51-115). iOS: ATT before UMP.
- Reload with linear backoff (5 s x attempt, max 5), reload on close, destroy before load, paid events dispatched to main thread, Firebase `ad_impression` {ad_platform, value, currency}.
- Remote config: Firebase RC with typed defaults, per-key error tolerance, `eventLoadConfigComplete`.
- Init each module in its own try so one failure never skips ads; init flag in finally so splash never hangs.
- Editor fake ads: everything ready, rewarded grants.

## Do not copy
MAX/IronSource/Adjust/Solar/Bounce log server, hard-coded mediation consent=true, `CanRequestAds` dead end, reward on impression, logging from the paid callback thread, throwing Adjust init, global `placementTemp`, dead privacy/no-internet popups.
