# SDK banner occupancy plan

## Ruling

AdMob layout occupancy is actual native pixel height only when the SDK is initialized, the banner is requested visible, and that same banner has successfully loaded a finite positive measured height. Otherwise it is zero. Remove the AdMob estimated screen/DPI fallback. Preserve banner readiness as object availability so existing show/retry flows can still request a not-yet-loaded banner.

## Ruling after review

The layout event capability remains optional and cannot gate the existing `ISketchBannerAdsProvider.BannerHeightPixels` path. Legacy providers retain their polled finite positive height while initialized, ready and requested visible; they do not signal asynchronous changes. For AdMob, marshal the complete banner callback state transition (identity/generation check, measured-height read, state mutation, telemetry and notification) through `OnMainThread`. Deliver public listeners independently, log each failure, and stop an old delivery batch if a listener reconfigures the provider or changes current occupancy.

## Boundaries

- SDK: provider-neutral optional banner layout notifier interface and facade forwarding; no game/UI policy.
- AdMob provider: actual load/show/hide/failed measured state, callback identity and generation guards, event detach/reset, no fallback estimates.
- Tests: dependency-free behavioral harness for state transitions, edge values, stale callbacks/provider replacement, legacy polling, subscriber isolation and reentrant replacement; all prior assertions remain green.

## Sequence and verification

1. Extend fake and legacy providers and add red behavior tests for loading, loaded+shown, hidden, failure, invalid dimensions, replacement, queued stale callbacks, legacy polling, throwing listeners and reentrant replacement. Run harness; confirm behavioral failures.
2. Add an optional separate banner layout event capability and facade-managed subscription tied to current provider generation. Continue polling legacy `BannerHeightPixels`; replacement/reset detaches old notifier and publishes zero.
3. Update AdMob provider to expose zero until `_initialized && _bannerRequested && _bannerLoaded && finite(height)>0`; marshal callback work to main thread, and bind callback actions to banner instance and request generation. Use measured native height only. Fix duplicate `DeviceScaleProvider` assignment as directly related constructor cleanup.
4. Run full dependency-free suite and the existing real Google Mobile Ads 11.6.0 source compile fixture. Confirm no network/native requests were added and no game files/assets changed.
5. Review diff, update README/package notes only to document the contract and optional capability; report Editor/device limitations. Do not commit before root review.

## Acceptance

- `BannerHeightPixels == 0` before initialization, when hidden, loading, failed, invalid measurement, or after provider replacement/reset.
- It equals the finite positive measured native height only for a loaded, requested-visible banner.
- Load/fail/show/hide/resize notifications belong to the current provider/banner generation only; stale callbacks cannot restore occupancy.
- Banner events run on Unity's main thread; one throwing subscriber does not prevent later listeners, and provider reconfiguration during delivery stops the obsolete event batch.
- Existing SDK core tests and actual 11.6.0 compile fixture pass; events avoid duplicate notifications.
