# Sketch Ads SDK

A source UPM package for a provider-neutral ads facade and an opt-in Google Mobile Ads provider. The package contains no game policy, serialized assets, third-party binaries, or service credentials.

## Package boundary

- `SketchSdk.Initialize(provider, callback)` is the explicit bootstrap entry point. No provider or ready fake is constructed implicitly.
- `ISketchAdsProvider` owns rewarded, interstitial and app-open mechanics.
- Banner and native ads are optional roles (`ISketchBannerAdsProvider`, `ISketchNativeAdsProvider`). In-ad close support is a separate capability (`ISketchAdCloseCapability`).
- Placement, consent, remove-ads and reward decisions remain in the game's decision layer.
- `ISketchAdsTelemetry` carries ad lifecycle events and raw AdMob paid values (micros plus currency) to a game-owned adapter. `ISketchIapTelemetry` carries successful store captures to a server adapter. The package does not grant entitlements.

## Install

Add this repository/folder as a Unity Package Manager package. Install the Google Mobile Ads Unity plugin separately if AdMob is wanted. The provider assembly is opt-in: add scripting define `SKETCHSDK_ADMOB` and ensure the installed plugin exposes the assemblies referenced by `Runtime/Providers/AdMob/SketchSdk.AdMob.asmdef`.

The extracted provider source came from the Dig Egg project using Google Mobile Ads Unity plugin **11.6.0** (`GoogleMobileAds_version-11.6.0_manifest.txt`). It was not validated against a 9.3 plugin. No vendor DLLs or SDK files are copied here.

## Bootstrap sketch

Map values from a game-owned ScriptableObject into the plain runtime config. Keep consent and placement decisions in the game adapter.

```csharp
var config = new SketchAdMobConfig
{
    RewardedId = rewardedIdForPlatform,
    InterstitialId = interstitialIdForPlatform,
    BannerId = bannerIdForPlatform,
    AppOpenId = appOpenIdForPlatform,
    NativeId = nativeIdForPlatform,
    TestDeviceIds = testDeviceIds,
    NonPersonalized = consentRequiresNonPersonalizedAds,
};

// Keep interstitial spacing in the game policy layer; call ShowInterstitial only when its gate allows it.
SketchSdk.Initialize(new SketchSdkAdMobProvider(config, gameAdsTelemetry), initialized =>
{
    // Continue the game's own startup flow.
});
```

Configure Google Mobile Ads Android/iOS application IDs through the installed plugin's Editor settings and generated build metadata. `SketchAdMobConfig` accepts ad-unit IDs only; an application ID by itself does not make runtime initialization usable.

`gameAdsTelemetry` must implement the provider-neutral event contract. The `SketchSdkConfig` ScriptableObject and its `.meta` file stay in the game project; map its existing fields into `SketchAdMobConfig`, preserving the asset GUID and serialized field names during migration. Do not copy the asset into this package.

## Analytics and IAP staging

The package exposes `ISketchAdsTelemetry` and `ISketchIapTelemetry` but intentionally does not reference Bounce or `Sketch.Analytics`. Add consumer-side adapters in the game that owns those dependencies. Ad lifecycle events can map to `Sketch.Analytics.AnalyticsClient.Track`. The server SDK's `TrackAdRevenue` contract labels its amount `RevenueUsd`, while AdMob provides `ValueMicros` and a currency code; preserve both raw values through `Track` unless a verified conversion is available. Do not silently treat a non-USD amount as USD or emit duplicate revenue events.

After a successful store transaction, the game may map `SketchIapCapture` to the server SDK's `IapCapture` and call `AnalyticsClient.CaptureIap`. That is capture/receipt submission only: entitlement decisions remain in the store/game flow and server-side receipt verification. Implement and verify the server and Bounce adapters, consent-denied behavior, deduplication, IAP capture and paid-event mapping as a separate consumer integration stage. This package does not claim those integrations are finished.

## Verification

Run the package's dependency-free core checks with:

```powershell
dotnet run --project Tests~/SketchSdk.Policy.Tests/SketchSdk.Policy.Tests.csproj
```

The test harness links core C# sources and tiny Unity API stubs; it does not prove Unity import, actual editor compilation, native plugin linkage, AdMob behavior, consent, or device callbacks. Those gates require a Unity consumer project with the documented Google Mobile Ads plugin and real-device checks.
