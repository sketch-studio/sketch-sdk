# Initial extraction verification

This is a verified development checkpoint, not a native-ad or store-release certification.

## Passed

- Dependency-free core harness: 49 assertions covering admission, accepted completion, rejection without callbacks, duplicate/stale callbacks, provider replacement, native attachment timeout and initialization waiters.
- Analytics mapping harness: 9 assertions, including one USD event with raw metadata and top-level placement, one raw non-USD event without invented USD conversion, and receipt forwarding without entitlement grants.
- Google Mobile Ads Unity 11.6.0 source API compile: 0 errors against its real managed assemblies and Unity 6000.3.22f1 CoreModule. Ten existing obsolete-treatment API warnings remain.
- Unity Editor package import and core assembly compilation. Optional AdMob assembly was enabled and loaded in a consumer using temporary original GMA DLLs.
- Unity Android player script compilation produced 50 assemblies including `SketchSdk.Runtime.dll` and `SketchSdk.AdMob.dll`. The provider asmdef references only GoogleMobileAds.dll, GoogleMobileAds.Core.dll and GoogleMobileAds.Common.dll; the Editor-only GoogleMobileAds.Unity.dll is not a player dependency.
- Optional analytics bridge compiled against an exact local snapshot of the existing Sketch Analytics 1.0.0 client (server repository commit `9ee160d776c26ded988f3b593ebe1017c1f1e687`). A Play test with that real client enqueued exactly one USD and one non-USD event; denied consent cleared/dropped telemetry. Preferences and queue data were restored. Network work was prevented; no receipt was submitted.
- Consumer regression checkpoint: 45 ECS integration cases and 39 EditMode tests, including all 5,932 recovered graph nodes. These consumer tests do not constitute SDK device coverage.

## Configuration and migration boundary

No game config asset, ad identifier, service key, prefab, scene or third-party binary is distributed in this package. Keep existing per-game config scripts/assets and their metadata in the consumer project. Set native AdMob app IDs through the Google Mobile Ads Editor settings; runtime ad-unit configuration does not replace native build metadata.

This is not a drop-in import while legacy copies of `Sketch.Sdk` native helper types remain in Assets. Inventory and migrate duplicate source assemblies and the old game-coupled facade together, retaining the game config type, GUIDs and serialized fields. Keep placement/consent/remove-ads/cadence policy game-side. The original source project has not been migrated or modified by this extraction checkpoint.

## Still required

- Full Google Mobile Ads plugin/native dependency installation, Android APK/AAB build and physical-device ads, consent and lifecycle tests. Temporary managed-DLL compile fixtures are not a full plugin installation.
- Real IAP purchasing, restore, receipt validation and entitlement tests in a consumer. `ISketchIapTelemetry` only captures a store result supplied by that consumer.
- Backend HTTP delivery, authentication, persistence and receipt-capture verification. The configured server Git URL returned Repository not found during this session; the local client snapshot was used only for compatibility testing.
- Game-side bootstrap, actual placements and migration of the legacy consumer. No production configuration is overwritten automatically.

The consumer's Hot Reload vendor package emitted an APIUpdater error during temporary DLL import. Both Editor SDK type loading and Android C# script compilation subsequently succeeded. The error was preserved; temporary DLLs and test defines were removed/restored. It is not evidence of a native build or device pass.
