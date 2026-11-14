# Sketch SDK 1.0 - full "Bounce-like" package (AdMob only)

**Goal (OR-001):** one git URL in Package Manager -> open `Sketch SDK > Settings` -> fill AdMob ids (+ optional analytics server key) -> ads, consent, analytics, ad revenue and IAP capture work with no game-side plumbing beyond `SketchAds.ShowRewarded(placement, cb)` style calls.

**Reference:** Bounce SDK (break-and-steal `Assets/BounceSdk`, package `com.bounce.sdk` v1.0.2) for API surface and rules; MAX/Adjust parts dropped (owner has AdMob only).

## Rulings
- R1 Keep namespaces `Sketch.Sdk` and asmdef `SketchSdk.Runtime` / `SketchSdk.AdMob` (pixel-smash pins commit b48f300; nothing breaks). Package id becomes `com.sketch.sdk` v1.0.0 (consumers update the manifest key once).
- R2 Settings live in a consumer asset `Assets/SketchSdk/Resources/SketchSdkSettings.asset`, created by the Editor window; never in the package, never keys in code.
- R3 No runtime manager GameObject: lifecycle via `Application.focusChanged` + `RuntimeInitializeOnLoadMethod`; main-thread hop via GMA `MobileAdsEventExecutor`.
- R4 AdMob module auto-enabled: Editor detects GoogleMobileAds assembly and adds `SKETCHSDK_ADMOB` define (also when GMA is installed as .unitypackage under Assets). GMA install button adds OpenUPM scoped registry `com.google.ads.mobile`.
- R5 Consent = Google UMP (shipped inside the GMA plugin). Every consent outcome converges on one guarded ads init.
- R6 Editor / no-provider builds use `SketchEditorFakeProvider`: all formats ready, rewarded returns true (house rule).
- R7 Policy (interstitial cooldown, first-inter delay, app-open on resume + cooldown, remove-ads) is pure C# in core and configured from settings; game may override at runtime.
- R8 Analytics: provider-neutral `SketchAnalytics.Log(name, params)` with pluggable sinks. sketch-server client bridge auto-registers when `com.sketch.analytics` is installed (versionDefines). Firebase optional sink when Firebase Analytics is installed.
- R9 IAP: SDK captures receipts/revenue only (`SketchIap.ReportPurchase`), never grants entitlements.

## Tasks
- [x] T1 Study Bounce + server + Dig (subagents) -> evidence notes
- [x] T2 Core: settings SO, policy, fake provider, `SketchAds` game facade, analytics hub, auto bootstrap
- [x] T3 AdMob: settings mapping, UMP consent, app-open on resume, test-id mode
- [x] T4 Editor: settings window, define sync, GMA app-id sync, build validator, install GMA button
- [x] T5 Analytics sinks (server bridge, Firebase optional), IAP capture
- [x] T6 Tests: dotnet policy harness green, GMA source compile, Unity consumer project compile (core only + with GMA)
- [x] T7 Docs: README quick start (VN+EN), CHANGELOG, samples; commit + push

## Rulings added during execution
- R10 `sketch-server/unity-sdk` (com.sketch.analytics) posts to routes that do not exist; the old bridge was removed and the SDK ships its own `/api/v1` client (contract: sketch-server docs/sdk-contract.md).
- R11 Revenue: AdMob value is sent as `revenueUsd` with the real currency code; milestones count USD only (no invented FX conversion).
- R12 401/403 are retried (fixed key still delivers the queue); other 4xx payload rejections are dropped.
