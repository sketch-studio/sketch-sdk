using System.Collections.Generic;
using NUnit.Framework;

namespace Sketch.Sdk.Tests
{
    public sealed class SketchAdLoadDiagnosticsTests
    {
        [SetUp]
        public void SetUp() => SketchAdLoadDiagnostics.ResetStats();

        static SketchAdLoadFailure NoFill(string format = "interstitial") => new SketchAdLoadFailure
        {
            Format = format,
            AdUnitId = "ca-app-pub-1/2",
            Code = 3,
            Domain = "com.google.android.gms.ads",
            Message = "No fill.",
            ResponseId = "resp-1",
            Adapters = new List<SketchAdapterAttempt>
            {
                new SketchAdapterAttempt { AdSourceName = "Liftoff Monetize", AdapterClass = "com.vungle.VungleAdapter", LatencyMillis = 42, ErrorCode = 3, ErrorMessage = "No ads" },
                new SketchAdapterAttempt { AdSourceName = "AdMob Network", AdapterClass = "com.google.ads.mediation.admob.AdMobAdapter", LatencyMillis = 120, ErrorCode = 3, ErrorMessage = "No fill." },
            },
        };

        [Test]
        public void ReasonFor_AndroidCodes_NamesGoogleConstants()
        {
            Assert.AreEqual("NO_FILL", SketchAdLoadDiagnostics.ReasonFor(3, false));
            Assert.AreEqual("MEDIATION_NO_FILL", SketchAdLoadDiagnostics.ReasonFor(9, false));
            Assert.AreEqual("INVALID_REQUEST", SketchAdLoadDiagnostics.ReasonFor(1, false));
            Assert.AreEqual("INTERNAL_ERROR", SketchAdLoadDiagnostics.ReasonFor(0, false));
            Assert.AreEqual("NETWORK_ERROR", SketchAdLoadDiagnostics.ReasonFor(2, false));
            Assert.AreEqual("APP_ID_MISSING", SketchAdLoadDiagnostics.ReasonFor(8, false));
            Assert.AreEqual("CODE_77", SketchAdLoadDiagnostics.ReasonFor(77, false));
        }

        [Test]
        public void ReasonFor_IosCodes_UseIosTable()
        {
            Assert.AreEqual("NO_FILL", SketchAdLoadDiagnostics.ReasonFor(1, true));
            Assert.AreEqual("INVALID_REQUEST", SketchAdLoadDiagnostics.ReasonFor(0, true));
        }

        [Test]
        public void Describe_ContainsFormatCodeReasonResponseIdAndEveryAdapter()
        {
            string text = SketchAdLoadDiagnostics.Describe(NoFill(), false);
            StringAssert.Contains("format=interstitial", text);
            StringAssert.Contains("unit=ca-app-pub-1/2", text);
            StringAssert.Contains("code=3 NO_FILL", text);
            StringAssert.Contains("response_id=resp-1", text);
            StringAssert.Contains("Liftoff Monetize", text);
            StringAssert.Contains("com.vungle.VungleAdapter", text);
            StringAssert.Contains("AdMob Network", text);
            StringAssert.Contains("hint=", text);
        }

        [Test]
        public void Describe_NoAdapters_SaysWaterfallEmpty()
        {
            var failure = NoFill();
            failure.Adapters = null;
            StringAssert.Contains("adapters=none", SketchAdLoadDiagnostics.Describe(failure, false));
        }

        [Test]
        public void AdapterSummary_IsCompactAndCapped()
        {
            Assert.AreEqual("Liftoff Monetize:3/42ms;AdMob Network:3/120ms", SketchAdLoadDiagnostics.AdapterSummary(NoFill(), 100));
            Assert.AreEqual(20, SketchAdLoadDiagnostics.AdapterSummary(NoFill(), 20).Length);
        }

        [Test]
        public void AdapterSummary_LoadedAdapter_ShowsOk()
        {
            var failure = NoFill();
            failure.Adapters[0].ErrorCode = SketchAdapterAttempt.NO_ERROR;
            StringAssert.StartsWith("Liftoff Monetize:ok/42ms", SketchAdLoadDiagnostics.AdapterSummary(failure, 100));
        }

        [Test]
        public void Stats_CountLoadsAndFailuresPerFormatAndCode()
        {
            SketchAdLoadDiagnostics.RecordFailure("interstitial", 3);
            SketchAdLoadDiagnostics.RecordFailure("interstitial", 3);
            SketchAdLoadDiagnostics.RecordFailure("interstitial", 0);
            SketchAdLoadDiagnostics.RecordLoaded("interstitial");
            SketchAdLoadDiagnostics.RecordFailure("banner", 3);

            Assert.AreEqual("interstitial attempts=4 loaded=1 failed=3 match=25.0% [code0 x1, code3 x2]", SketchAdLoadDiagnostics.Summary("interstitial"));
            Assert.AreEqual("banner attempts=1 loaded=0 failed=1 match=0.0% [code3 x1]", SketchAdLoadDiagnostics.Summary("banner"));
            Assert.AreEqual("rewarded attempts=0", SketchAdLoadDiagnostics.Summary("rewarded"));
        }

        [Test]
        public void ParseJavaNativeFailure_ReadsCodeMessageResponseAndAdapters()
        {
            var failure = SketchAdLoadDiagnostics.ParseJavaNativeFailure("unit-n", "3|No fill.|com.google.android.gms.ads|resp-9|Liftoff Monetize:3/40ms;AdMob Network:3/90ms");
            Assert.AreEqual("native", failure.Format);
            Assert.AreEqual(3, failure.Code);
            Assert.AreEqual("No fill.", failure.Message);
            Assert.AreEqual("com.google.android.gms.ads", failure.Domain);
            Assert.AreEqual("resp-9", failure.ResponseId);
            Assert.AreEqual("Liftoff Monetize:3/40ms;AdMob Network:3/90ms", SketchAdLoadDiagnostics.AdapterSummary(failure, 100));
        }

        [Test]
        public void ParseJavaNativeFailure_OldTwoFieldPayload_StillParses()
        {
            var failure = SketchAdLoadDiagnostics.ParseJavaNativeFailure("unit-n", "3|No fill.");
            Assert.AreEqual(3, failure.Code);
            Assert.AreEqual("No fill.", failure.Message);
            Assert.IsNull(failure.ResponseId);
        }

        [Test]
        public void Router_Parameters_IncludeResponseIdAndAdapters()
        {
            var parameters = SketchAdsTelemetryRouter.BuildParameters(new SketchAdTelemetryEvent
            {
                Name = "ad_load_failed", Format = "interstitial", Code = "3", Message = "No fill.",
                ResponseId = "resp-1", Adapters = "AdMob Network:3/120ms",
            });
            Assert.AreEqual("resp-1", parameters["response_id"]);
            Assert.AreEqual("AdMob Network:3/120ms", parameters["adapters"]);
            Assert.AreEqual("3", parameters["code"]);
        }

        [Test]
        public void InspectorGesture_FiresOnceAfterFourFingersHeldTwoSeconds()
        {
            var gesture = new SketchAdInspectorGestureDetector();
            Assert.IsFalse(gesture.Step(4, 0.0));
            Assert.IsFalse(gesture.Step(4, 1.9));
            Assert.IsTrue(gesture.Step(4, 2.0));
            Assert.IsFalse(gesture.Step(4, 5.0), "must not refire while the same hold continues");
            Assert.IsFalse(gesture.Step(0, 5.1));
            Assert.IsFalse(gesture.Step(4, 6.0));
            Assert.IsTrue(gesture.Step(4, 8.0));
        }

        [Test]
        public void InspectorGesture_FewerFingersOrBrokenHold_NeverFires()
        {
            var gesture = new SketchAdInspectorGestureDetector();
            Assert.IsFalse(gesture.Step(3, 0.0));
            Assert.IsFalse(gesture.Step(3, 10.0));
            Assert.IsFalse(gesture.Step(4, 10.0));
            Assert.IsFalse(gesture.Step(2, 11.0));
            Assert.IsFalse(gesture.Step(4, 11.5));
            Assert.IsFalse(gesture.Step(4, 13.0));
            Assert.IsTrue(gesture.Step(4, 13.5));
        }
    }
}
