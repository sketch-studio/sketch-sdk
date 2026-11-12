# Optional Sketch Analytics bridge

`SketchSdk.Analytics` is a source integration for `com.sketch.analytics` 1.0.0. It is compiled only when that package is installed and `SKETCHSDK_ANALYTICS` is defined. The game bootstrap constructs `SketchAnalyticsBridge` with its existing `Sketch.Analytics.AnalyticsClient` and injects the same instance as `ISketchAdsTelemetry` and `ISketchIapTelemetry`. The bridge does not find or create the client.

The server client owns consent, queueing and delivery. Call `SetConsent` on the client before provider startup. Its `Track`, `TrackAdRevenue` and `CaptureIap` methods reject capture when consent is not granted; the bridge does not cache or retry rejected calls. Denied consent clearing and offline queue behavior must be verified in the consumer's chosen server SDK version.

Placement fallback names are stable format names: `rewarded`, `interstitial`, `app_open`, `banner`, `native`, and `unknown`. Games may pass their own placement through `SketchAdTelemetryEvent.Placement` or `SketchAdRevenue.Placement`; the bridge does not invent game IDs.

Revenue is forwarded once per `TrackRevenue` call using one `Track` queue write. USD uses the server's `ad_revenue` event name and required properties, including `revenueUsd = ValueMicros / 1,000,000`, plus `rawValueMicros`, `currency`, `responseId`, and `adSource`. `AdSource` maps to source `Network`; the provider's `Network` value (`admob`) maps to `Mediation`. This keeps the server SDK's one-save `Track` path and avoids an extra metadata write. Other currencies use one `ad_revenue_raw` generic event with raw micros and original currency; this distinct name cannot be mistaken for the server's USD revenue taxonomy, and no exchange rate or USD value is fabricated. Do not separately forward the same revenue through another consumer adapter.

IAP records map to `CaptureIap(IapCapture)`, which the server SDK documents as analytics plus capture-only receipt submission. It does not grant entitlements. Pass only completed store transactions; preserve product, platform, order, receipt, price and currency from the trusted store callback. Receipt validation and granting remain server/game responsibilities. The current `SketchIapCapture` contract has no transaction deduplication key policy beyond `OrderId`; validate deduplication in the consumer/backend integration.

## Validation scope

`dotnet run --project Tests~/AnalyticsBridge.Tests/AnalyticsBridge.Tests.csproj` checks mappings against source-shaped public contract stubs. It sends no requests and contains no credentials. This does not verify Unity asmdef resolution, real package import, consent-denial queue clearing, backend schemas, receipt verification, or live server behavior; those require the consumer Unity project and a controlled integration environment.
