using System.Collections.Generic;
using NUnit.Framework;

namespace Sketch.Sdk.Tests
{
    // OR-802/806/808/810 analytics: schema limits, per-event sink routing (Firebase = revenue only), ad_impression mapping, IAP revenue, remote log switch.
    public sealed class SketchAnalyticsParityTests
    {
        sealed class CaptureSink : ISketchAnalyticsSink, ISketchRoutedSink
        {
            public CaptureSink(SketchAnalyticsRoute route) => Route = route;
            public SketchAnalyticsRoute Route { get; }
            public readonly List<string> Events = new List<string>();
            public readonly List<IReadOnlyDictionary<string, object>> Params = new List<IReadOnlyDictionary<string, object>>();
            public readonly List<SketchAdRevenue> Revenue = new List<SketchAdRevenue>();
            public readonly List<SketchPurchase> Purchases = new List<SketchPurchase>();
            public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters) { Events.Add(eventName); Params.Add(parameters); }
            public void LogAdRevenue(SketchAdRevenue revenue) => Revenue.Add(revenue);
            public void LogPurchase(SketchPurchase purchase) => Purchases.Add(purchase);
        }

        sealed class PlainSink : ISketchAnalyticsSink
        {
            public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters) { }
            public void LogAdRevenue(SketchAdRevenue revenue) { }
            public void LogPurchase(SketchPurchase purchase) { }
        }

        CaptureSink _server, _firebase;
        IList<float> _milestones;

        [SetUp]
        public void SetUp()
        {
            // EditMode has no PlayerLoop tick: subscribing installs the SDK loop and binds the main thread, so Run() is inline.
            SketchSdkLoop.Update += Noop;
            _server = new CaptureSink(SketchAnalyticsRoute.Server);
            _firebase = new CaptureSink(SketchAnalyticsRoute.Firebase);
            _milestones = SketchAnalytics.RevenueMilestonesUsd;
            SketchAnalytics.RevenueMilestonesUsd = null;   // no LTV milestone events in these tests
            SketchAnalytics.EventsEnabled = true;
            SketchAnalytics.AddSink(_server);
            SketchAnalytics.AddSink(_firebase);
        }

        static void Noop() { }

        [TearDown]
        public void TearDown()
        {
            SketchSdkLoop.Update -= Noop;
            SketchAnalytics.RemoveSink(_server);
            SketchAnalytics.RemoveSink(_firebase);
            SketchAnalytics.RevenueMilestonesUsd = _milestones;
            SketchAnalytics.EventsEnabled = true;
        }

        [Test]
        public void Schema_EventNames_FollowFirebaseRules()
        {
            Assert.IsTrue(SketchAnalyticsSchema.IsValidEventName("level_complete"));
            Assert.IsTrue(SketchAnalyticsSchema.IsValidEventName(new string('a', 40)));
            Assert.IsFalse(SketchAnalyticsSchema.IsValidEventName(new string('a', 41)));
            Assert.IsFalse(SketchAnalyticsSchema.IsValidEventName("1level"));
            Assert.IsFalse(SketchAnalyticsSchema.IsValidEventName("level-complete"));
            Assert.IsFalse(SketchAnalyticsSchema.IsValidEventName("firebase_x"));
            Assert.IsFalse(SketchAnalyticsSchema.IsValidEventName("ga_x"));
        }

        [Test]
        public void Schema_Validate_FlagsTooManyParamsAndLongStrings()
        {
            var p = new Dictionary<string, object>();
            for (int i = 0; i < 26; i++) p["p" + i] = i;
            p["s"] = new string('x', 101);
            var issues = new List<string>();
            Assert.IsFalse(SketchAnalyticsSchema.Validate("ok_event", p, issues));
            Assert.AreEqual(2, issues.Count, string.Join("\n", issues));
            Assert.IsTrue(SketchAnalyticsSchema.Validate("ok_event", new Dictionary<string, object> { ["level"] = 3 }, issues));
        }

        [Test]
        public void GameEvents_GoToServerOnly_NeverFirebase()
        {
            SketchAnalytics.Log("level_complete", "level", 3);
            CollectionAssert.AreEqual(new[] { "level_complete" }, _server.Events);
            Assert.AreEqual(3, _server.Params[0]["level"]);
            Assert.AreEqual(0, _firebase.Events.Count, "OR-806: Firebase receives revenue only");
        }

        [Test]
        public void ExplicitRoute_ReachesOnlyMaskedSinks()
        {
            SketchAnalytics.LogTo(SketchAnalyticsRoute.Firebase, "reach_rev_1");
            Assert.AreEqual(0, _server.Events.Count);
            CollectionAssert.AreEqual(new[] { "reach_rev_1" }, _firebase.Events);
        }

        [Test]
        public void UnroutedSink_TreatedAsOther_GetsDefaultEvents()
        {
            Assert.AreEqual(SketchAnalyticsRoute.Other, SketchAnalyticsRouting.RouteOf(new PlainSink()));
            Assert.IsTrue(SketchAnalyticsRouting.Accepts(SketchAnalyticsRouting.DefaultEvents, new PlainSink()));
            Assert.IsFalse(SketchAnalyticsRouting.Accepts(SketchAnalyticsRouting.DefaultEvents, _firebase));
        }

        [Test]
        public void AdRevenue_ReachesFirebaseAndServer()
        {
            new SketchAdsTelemetryRouter().TrackRevenue(new SketchAdRevenue { Format = "rewarded", Placement = "free_gems", ValueMicros = 0, Currency = "USD" });
            Assert.AreEqual(1, _server.Revenue.Count);
            Assert.AreEqual(1, _firebase.Revenue.Count);
            Assert.AreEqual("free_gems", _firebase.Revenue[0].Placement);
        }

        [Test]
        public void AdImpression_FromPaidEvent_MapsGa4RevenueParams()
        {
            var result = new List<KeyValuePair<string, object>>();
            SketchAnalyticsSchema.AdImpressionParams(new SketchAdRevenue
            {
                Format = "interstitial", Placement = "level_end", Currency = "EUR", ValueMicros = 12_345,
                AdUnitId = "ca-app-pub-x/1", AdSource = "Meta Audience Network",
            }, result);
            var d = new Dictionary<string, object>();
            foreach (var kv in result) d[kv.Key] = kv.Value;
            Assert.AreEqual("AdMob", d["ad_platform"]);
            Assert.AreEqual("Meta Audience Network", d["ad_source"]);
            Assert.AreEqual("interstitial", d["ad_format"]);
            Assert.AreEqual("ca-app-pub-x/1", d["ad_unit_name"]);
            Assert.AreEqual("EUR", d["currency"]);
            Assert.AreEqual(0.012345, (double)d["value"], 1e-12);
            Assert.AreEqual("level_end", d["placement"]);

            SketchAnalyticsSchema.AdImpressionParams(new SketchAdRevenue { Format = "banner", ValueMicros = 1 }, result);
            d.Clear();
            foreach (var kv in result) d[kv.Key] = kv.Value;
            Assert.AreEqual("USD", d["currency"], "missing currency defaults to USD");
            Assert.AreEqual("AdMob", d["ad_source"], "missing source defaults to AdMob");
        }

        [Test]
        public void IapPurchase_ReachesFirebaseAndServer_WithRevenueParams()
        {
            var purchase = new SketchPurchase { ProductId = "gem_pack_1", Price = 25000, Currency = "VND", PriceUsd = 0.99, Store = "GooglePlay", Placement = "gem_shop", TransactionId = "t1" };
            SketchAnalytics.LogPurchase(purchase);
            Assert.AreSame(purchase, _server.Purchases[0]);
            Assert.AreSame(purchase, _firebase.Purchases[0]);
            var p = SketchAnalyticsSchema.PurchaseParams(purchase);
            Assert.AreEqual("gem_pack_1", p["product_id"]);
            Assert.AreEqual(25000d, p["value"]);
            Assert.AreEqual("VND", p["currency"]);
            Assert.AreEqual(0.99, p["value_usd"]);
            Assert.AreEqual("gem_shop", p["placement"]);
        }

        [Test]
        public void AdLifecycleLogs_RateLimitedToTenPerSecond_LikeBounce()
        {
            double t = 100;
            var router = new SketchAdsTelemetryRouter { Clock = () => t };
            for (int i = 0; i < 15; i++) router.Track(new SketchAdTelemetryEvent { Name = "ad_clicked", Format = "banner", Placement = "Banner" });
            Assert.AreEqual(10, _server.Events.Count);
            t = 101.01;
            router.Track(new SketchAdTelemetryEvent { Name = "ad_clicked", Format = "banner", Placement = "Banner" });
            Assert.AreEqual(11, _server.Events.Count);
        }

        [Test]
        public void IapStart_GoesToFirebaseToo_LikeBounce()
        {
            SketchIap.ReportPurchaseStart("gem_pack_1", "gem_shop");
            CollectionAssert.AreEqual(new[] { "iap_start" }, _firebase.Events);
            CollectionAssert.AreEqual(new[] { "iap_start" }, _server.Events);
        }

        [Test]
        public void RemoteLogDisable_DropsEventsButKeepsRevenue()
        {
            SketchAnalytics.EventsEnabled = false;
            SketchAnalytics.Log("level_complete", "level", 1);
            SketchAnalytics.LogAdRevenue(new SketchAdRevenue { Format = "banner", ValueMicros = 0 });
            Assert.AreEqual(0, _server.Events.Count);
            Assert.AreEqual(1, _server.Revenue.Count, "Bounce never gates revenue on logEnable");
        }
    }
}
