# SDK banner occupancy report — checkpoint

## Status

SDK change implemented and source-verified. Root review is pending; no commit was created.

## Evidence / constraints

- SDK repo has no root `AGENTS.md`, `docs/agent-handoff.md`, or `docs/feedback-notes.md`; repository `git status` already contained unrelated untracked `docs/verification-2026-11-12.md.meta` and `tools.meta`, preserved.
- Current provider reports `_banner != null` as banner-ready, while `BannerHeightPixels` returns an estimated Editor/device height after request before a successful load. `LoadBanner` callbacks set loaded state and measured height; hide clears requested state.
- `SketchSdk` binds one provider with generation-protected initialization but has no banner layout notification capability. Existing dependency-free tests use one `TestProvider` in `Tests~/SketchSdk.Policy.Tests/Program.cs`.
- Plan: `docs/superpowers/plans/2026-11-13-sdk-banner-occupancy.md`.

## Ruling

Use a separate optional event interface so existing banner providers remain source-compatible. AdMob reports zero until an actual measured banner is loaded and requested visible; legacy providers retain their existing finite positive height polling while initialized, ready and requested. Keep `IsBannerReady` as banner-object readiness to preserve show/retry capability.

## Skills

Read `ads-layer-generic`, `coding-discipline-karpathy`, Superpowers `test-driven-development`, and `writing-plans`. Superpowers TDD requires observing a behavioral RED before production implementation.

## Test checkpoint (2026-11-13)

- Added provider-neutral test seam and behavioral coverage for zero-before-init/loading, loaded shown height, duplicate suppression, hide, provider replacement/stale callback rejection, and invalid dimensions.
- RED was observed at `loaded visible banner reports measured height` with the compiling temporary facade seam. After implementation, the same harness is green; see verification below.

## Implementation and verification checkpoint (2026-11-13)

- Added `ISketchBannerLayoutEvents`; facade subscribes only to the active provider generation, validates finite positive height, deduplicates transitions, forwards changes, hides/detaches the previous banner on replacement/reset, clears state on reset, and ignores stale provider events.
- AdMob provider now returns measured `BannerView.GetHeightInPixels()` only while initialized, requested-visible, loaded, finite and positive. No Editor/DPI fallback remains. Banner callbacks capture the BannerView and request generation. Removed duplicate `DeviceScaleProvider` assignment.
- `dotnet run --project Tests~/SketchSdk.Policy.Tests/SketchSdk.Policy.Tests.csproj`: **PASS, 69 assertions**. This includes the earlier behavioral RED at `loaded visible banner reports measured height`, then green transition checks for loading, load failure, shown measured size and resize, hide, invalid values, provider replacement, stale provider events, reset and deduplication.
- `dotnet run --project Tests~/AnalyticsBridge.Tests/AnalyticsBridge.Tests.csproj`: **PASS, 9 assertions**. The no-dependency companion project is a library: `dotnet run` is not supported; `dotnet build Tests~/AnalyticsBridge.Tests/AnalyticsBridge.NoDependency.Tests.csproj --no-restore` **succeeded, 0 warnings/errors**.
- `dotnet build Tests~/AdMob.SourceCompile/SketchSdk.AdMob.SourceCompile.csproj --no-restore /p:UnityManagedPath='C:/Program Files/Unity/Hub/Editor/6000.3.22f1/Editor/Data/Managed/UnityEngine' /p:GmaManagedPath='C:/Unity/GitProject/dig-for-egg/dig-for-egg-unity/Assets/GoogleMobileAds'`: **succeeded, 0 errors** against real Unity CoreModule and GMA 11.6.0 managed assemblies; 10 existing obsolete age-treatment API warnings remain.
- `git diff --check` passed. No native ad requests, Unity project assets, or game files were changed. No Unity Editor/device runtime validation was run.

## Review note

The provider publishes dimension changes on each successful banner-loaded callback; this source does not independently trigger adaptive-banner reload on orientation change. The game adapter/layout owner must continue to react to screen geometry changes, and a real device check should confirm whether the installed AdMob plugin reports a new measured height for adaptive resize. No native behavior is claimed from source compilation alone.

## Review rework (2026-11-13)

- Root review identified that Google Mobile Ads callbacks may arrive on a worker thread. In the AdMob provider, the complete banner callback action (captured-banner/generation check, `GetHeightInPixels`, provider state mutation, telemetry, and occupancy event) now runs through existing `OnMainThread`, which uses `MobileAdsEventExecutor.ExecuteInUpdate`. Paid and other banner callbacks also perform their stale-banner guard inside the dispatched action.
- Restored the legacy `ISketchBannerAdsProvider` path: `SketchSdk.BannerHeightPixels` polls its finite positive property when initialized, ready and requested visible even without `ISketchBannerLayoutEvents`. Legacy providers do not provide asynchronous change notification. README/API docs now state this.
- Public layout subscribers run individually; failures are logged and later listeners continue. Delivery stops after a subscriber reconfigures the provider or otherwise changes the current occupancy.
- Added tests for a captured queued stale provider callback, legacy polling and invalid legacy heights, a throwing subscriber followed by a healthy subscriber, and provider replacement during event delivery. RED observed before this rework at the legacy-provider assertion. Latest core harness: **PASS, 77 assertions**.
- Latest GMA 11.6.0 / Unity CoreModule source compile: **0 errors**, 10 pre-existing age-treatment obsolescence warnings. No native ad requests, Unity Editor calls, or device tests.
- Review items addressed in source; root rerereview is pending. No commit was created.

## Root acceptance checkpoint (2026-11-13)

- Independent second review: APPROVE WITH LIMITS; previous threading, legacy querying and listener-isolation findings resolved. Root inspected the changed callback and facade paths and independently reran the core harness: 77 assertions passed.
- Imported local package0.2.0 into the actual Unity6000.3.22f1 game through project-validated MCP; package import/domain reload completed successfully and Unity generated the new interface `.meta`.
- Actual Android C# player compilation completed with49 assemblies. This excludes a native APK/device ad test; the conditional AdMob provider is separately compiled against real GMA11.6 assemblies by the source-compile project.
- The first temporary MCP compile snippet used `Length` on a ReadOnlyCollection and failed before execution; corrected to `Count`, then passed. Archived tooling error evidence in the game's `home-sdk-banner-triaged-console.json`; this was not a package source failure.
- Remaining limitations: real native ad callbacks/layout/rotation and physical-device behavior unverified. No production ad credentials/config assets or game save data were added to this package.
