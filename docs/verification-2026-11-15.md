# Sketch SDK 1.0.0 verification (2026-11-15)

| Check | Result |
|---|---|
| Unity 6000.3.22f1 compile: SketchSdk.Runtime, Editor, AdMob (GMA 11.6 + UMP), tests | 0 errors |
| Unity 2022.3.62f3 compile: Runtime, Editor, AdMob, Firebase (Firebase 14.1.0 tgz) | 0 errors |
| EditMode tests (Unity 6 / 2022.3) | 9/9 pass |
| PlayMode tests: real bootstrap with Editor fake provider (Unity 6 / 2022.3) | 8/8 pass |
| End-to-end vs sketch-server `buildApp` (real Fastify routes + zod, in-memory stores): sessions 201, events/batch 202, revenue/ads 201, revenue/iap 202, GET /config applied (configId Default, remote pacing key) | 2/2 pass |
| Auto bootstrap from Resources/SketchSdkSettings with server credentials | pass |
| Android IL2CPP ARM64 development APK with SDK + GMA 11.6 + UMP | build succeeded (26 MB) |
| dotnet pure harness | 86 assertions pass |
| Independent code review | 14 findings (0 high), all fixed and re-verified |

Not verified: physical device (no phone or emulator attached): real ad fill, UMP form, app open on resume, banner layout, paid events. Run the README "Before you publish" device checklist on the first game.
