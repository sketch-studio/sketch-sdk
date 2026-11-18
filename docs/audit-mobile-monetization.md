# Mobile Monetization Pro v2 / Bounce parity audit

Date: 2026-11-19
Scope: source inspection only. No package removal and no production code changes in this audit.

## Evidence inspected

- `docs/reference-bounce/BOUNCE-STUDY.md` and `README.md`.
- `Runtime/Ads`, `Runtime/Consent`, `Runtime/Analytics`, `Runtime/RemoteConfig`, `Runtime/Server`, `Runtime/Providers`.
- `Tests/Editor`, `Tests/Runtime`, `Tests~/SketchSdk.Policy.Tests`.
- `Assets/Mobile Monetization Pro/Scripts` and `Documentation/Documentation_MobileMonetizationPro.pdf` (PDF text extraction unavailable in this environment).

## Parity matrix

| Capability | Mobile Monetization Pro v2 evidence | Sketch SDK status | Decision |
|---|---|---|---|
| Rewarded, interstitial, banner, app-open, native | AdMob/mediation demo scripts and UI | Implemented behind `ISketchAdsProvider`, AdMob provider, editor fake | Keep SDK path |
| Consent/privacy | Internet gate and ad initializers | UMP provider with privacy-options API and once-only init | Keep SDK path |
| Remove ads | Demo shop toggles persisted RemoveAd/DisplayAds | `SketchAds.SetAdsRemoved`, policy blocks non-rewarded formats | Verify entitlement adapter in consumer |
| Subscription/VIP | Demo-only PlayerPrefs flags | SDK intentionally captures IAP and does not grant entitlements | Add game-side entitlement adapter, do not copy PlayerPrefs |
| Coin/reward demo shop | Demo `CurrentCoins`, `BuyMoreCoins`, `ShowReward` | SDK has reward callbacks and IAP/revenue telemetry, no economy ownership | Remain game-owned DataBlock/economy |
| Cross-promotion | `CrossPromo` image/video rotation, app/session timers, click stop | No cross-promo runtime module in Sketch SDK | Add optional provider-neutral cross-promo module if owner requires this feature |
| Offline/connectivity gate | `InternetConnectivityCheck` polls each frame and toggles UI/timeScale | SDK does not own gameplay timeScale or popup | Keep in game layer via offline-gate skill, no per-frame SDK poll |
| Analytics/revenue | Demo has no robust server contract | SDK sends typed events, ad revenue, IAP, configId, queue/retry | Server contract is stronger |
| Remote config/A-B | Not present in demo scripts | SDK/server support typed defaults, configId and experiments | Keep |
| Editor/testing | Demo editor wizard and fake scenes | SDK editor settings, fake providers, policy/source tests | Keep and expand focused tests only |

## Concrete gaps

1. Cross-promo is the only material package capability absent from the SDK. It should be an optional, game-agnostic service with explicit `ICrossPromoCatalog`, impression/click events, deterministic session/open rotation, cooldown, and no persistence implementation inside the SDK. Consumer adapters must provide DataBlock-backed state.
2. SDK has no entitlement persistence or receipt validation by design. `SetAdsRemoved` must be driven by a game-side verified purchase restore flow. Do not import demo PlayerPrefs.
3. Mobile Monetization Pro's internet script is a demo anti-pattern because it polls `Update` and controls global timeScale. Do not port it. Use one authoritative offline gate in the game.
4. The PDF could not be parsed by installed tools. Claims about any additional PDF-only feature remain UNVERIFIED until a parser or manual review is available.

## Removal gate for Mobile Monetization Pro

Do not remove the package yet. Removal is allowed only after:

- cross-promo decision is recorded (implemented in SDK or explicitly out of scope),
- every production scene/prefab/script reference is zero or migrated,
- demo-only scripts/scenes are excluded from first-party runtime,
- Sketch SDK package tests and server tests pass,
- Unity compile and natural Play flow pass with empty Console,
- provenance/third-party notice is preserved in the SDK or project docs,
- dependency sweep proves no package assembly, GUID, prefab, material, font, or scene is still needed.

No parity claim is made by this audit.

## Verification evidence

- Policy harness: PASS, 86 assertions (dotnet run --project Tests~/SketchSdk.Policy.Tests).
- Server tests: PASS, 7 files / 43 tests (
pm test, Vitest 5.0.5).
- Unity compile, Play, Android and PDF-only behavior: not run in this audit.


## Correction after owner review

The Unity Asset Store feature inventory confirms the package also advertises local push notifications, remote push/FCM, Android in-app update, rating, ATT, connectivity checker, Firebase Analytics and multiple mediation adapters. The previous matrix was incomplete and must not be treated as 100% parity. Source: https://assetstore.unity.com/packages/tools/integration/mobile-monetization-pro-v2-ads-iap-in-app-purchase-more-android--309617

Notification contracts were prototyped but are currently held OPEN because the Unity Git package cache omitted the new source files during import. No notification parity claim is made until a fresh package import compiles them. Remaining modules stay OPEN until implemented and tested: local/remote push, in-app update, rating, ATT, connectivity, and platform adapters.
