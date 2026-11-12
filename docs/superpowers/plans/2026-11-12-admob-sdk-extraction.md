# AdMob SDK Extraction Plan

**Goal:** Create an independently consumable Unity UPM package for the existing AdMob ad mechanics, neutral of Dig Egg and Bounce types.

**Architecture:** Keep the provider contract, facade, native policy/utilities, and telemetry contracts in `Sketch.Sdk`. Put real Google Mobile Ads calls in a separate opt-in asmdef that depends on the consumer's already-installed Google Mobile Ads plugin. Games map their existing assets and policy through adapters. Server analytics and IAP remain optional consumer adapters.

**Tech Stack:** Unity C#, UPM package, .NET console tests for pure logic.

**Spec:** `docs/reports/home-sdk-extraction-audit.md` in the Pixel Smash workspace; owner-approved scope in the parent task.

## Global Constraints
- No changes outside this repository except the explicitly owned audit report.
- Do not copy serialized assets, credentials, scenes, prefabs, or Google SDK binaries.
- Keep AdMob as the sole production ads provider in this package.
- Do not remove telemetry callbacks or claim IAP capture is complete; document consumer/server bridge work.

## Review Focus
- AdMob provider must have no game namespace/type references: source scan gate.
- Callback completion remains once-only: deterministic test.
- Interstitial cooldown helper stays in the consumer sample and accepts the app config interval.
- Package compiles without the optional AdMob plugin: core-only validation where available.
- Existing SketchSdkConfig asset migration is documented without source mutation.

## Files
- `Runtime/Ads/SketchAdFormat.cs`, `ISketchAdsProvider.cs`, `SketchSdk.cs`, `SketchInterstitialPacing.cs`: shared facade/contracts/policy.
- `Runtime/Ads/SketchNative*.cs`: native layout/policy/cadence and callback contracts from source.
- `Runtime/Providers/AdMob/SketchSdkAdMobProvider.cs`: adapted real AdMob provider.
- `Runtime/Telemetry/SketchAdsTelemetry.cs`: provider-neutral ad lifecycle/revenue sink.
- `Tests/SketchSdk.Policy.Tests`: executable pure logic regression tests.
- `package.json`, `README.md`, asmdefs: package and optional provider wiring.

## Tasks
- [x] Add failing pacing, rejection, once-only callback and provider-replacement tests; confirm red for rejected callback behavior.
- [x] Add reusable pure contracts and verify green (21 assertions). Interstitial spacing is no longer enforced by runtime SDK.
- [x] Adapt provider config/request settings/telemetry to neutral package types; removed policy and game-type coupling.
- [x] Add UPM metadata, assembly definitions, consumer migration and IAP/server staging docs.
- [ ] Run pure tests, source scans and package metadata validation; Unity consumer/provider compile remains pending.
