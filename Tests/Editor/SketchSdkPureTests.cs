using System.Collections.Generic;
using NUnit.Framework;

namespace Sketch.Sdk.Tests
{
    public sealed class SketchSdkPureTests
    {
        [Test]
        public void Json_RoundTrip_KeepsTypesAndEscapes()
        {
            var obj = (Dictionary<string, object>)SketchJson.Parse("{\"a\":1.5,\"b\":\"x\\\"y\\n\",\"c\":[true,null,{\"d\":-2e3}],\"u\":\"\\u00e9\"}");
            Assert.AreEqual(1.5, obj["a"]);
            Assert.AreEqual("x\"y\n", obj["b"]);
            var list = (List<object>)obj["c"];
            Assert.AreEqual(true, list[0]);
            Assert.IsNull(list[1]);
            Assert.AreEqual(-2000.0, ((Dictionary<string, object>)list[2])["d"]);
            Assert.AreEqual("é", obj["u"]);
            string again = SketchJson.Serialize(obj);
            Assert.AreEqual(SketchJson.Serialize(SketchJson.Parse(again)), again);
            Assert.IsFalse(SketchJson.TryParse("{\"a\":}", out _));
            Assert.IsFalse(SketchJson.TryParse("[1,2", out _));
        }

        [Test]
        public void ConfigValues_RemoteOverridesDefaults_TypedGetters()
        {
            var v = new SketchConfigValues();
            v.SetDefault("lives", "3");
            v.SetDefault("hard", "false");
            Assert.AreEqual(3, v.GetInt("lives"));
            Assert.IsFalse(v.GetBool("hard", true));
            Assert.IsTrue(SketchConfigValues.TryParseServerResponse("{\"ok\":true,\"configId\":\"T1_b\",\"config\":{\"lives\":5,\"hard\":true,\"obj\":{\"k\":1}}}", out var cfg, out string id));
            v.SetRemote(cfg, id);
            Assert.AreEqual(5, v.GetInt("lives"));
            Assert.IsTrue(v.GetBool("hard"));
            Assert.AreEqual("T1_b", v.ConfigId);
            Assert.AreEqual("{\"k\":1}", v.GetString("obj"));
            Assert.AreEqual(9, v.GetInt("missing", 9));
            Assert.IsFalse(SketchConfigValues.TryParseServerResponse("{\"ok\":false}", out _, out _));
            Assert.IsFalse(SketchConfigValues.TryParseServerResponse("not json", out _, out _));
        }

        [Test]
        public void RemoteKeys_OverridePacing_AndPlacements()
        {
            var source = new SketchAdsPolicySettings { InterstitialCooldownSeconds = 60, InterstitialMinSession = 2 };
            var values = new SketchConfigValues();
            values.SetRemote(new Dictionary<string, object>
            {
                [SketchAdsRemoteKeys.INTER_COOLDOWN] = 45.0,
                [SketchAdsRemoteKeys.APP_OPEN_RESUME] = false,
                [SketchAdsRemoteKeys.PLACEMENTS] = "[{\"placement\":\"level_end\",\"enabled\":false},{\"placement\":\"shop\",\"cooldown\":10}]",
            }, "Default");
            SketchAdsPolicySettings result = SketchAdsRemoteKeys.Apply(source, values);
            Assert.AreEqual(45f, result.InterstitialCooldownSeconds);
            Assert.IsFalse(result.AppOpenOnResume);
            Assert.AreEqual(2, result.InterstitialMinSession);
            Assert.IsFalse(result.FindPlacement("level_end").Enabled);
            Assert.AreEqual(10f, result.FindPlacement("shop").CooldownSeconds);
            Assert.AreEqual(2, result.FindPlacement("shop").MinSession, "new placement inherits the global session gate");
            Assert.AreEqual(60f, source.InterstitialCooldownSeconds, "source not mutated");
        }

        [Test]
        public void Policy_PlacementRules()
        {
            var settings = new SketchAdsPolicySettings
            {
                InterstitialStartDelaySeconds = 0, InterstitialCooldownSeconds = 60,
                Placements = new List<SketchPlacementRule>
                {
                    new SketchPlacementRule { Placement = "off", Enabled = false },
                    new SketchPlacementRule { Placement = "late", MinSession = 3 },
                    new SketchPlacementRule { Placement = "fast", CooldownSeconds = 5 },
                },
            };
            var policy = new SketchAdsPolicy(settings, 0) { Session = 2 };
            Assert.IsFalse(policy.CanShowInterstitial("off", 100));
            Assert.AreEqual("placement_disabled", policy.LastBlockReason);
            Assert.IsFalse(policy.CanShowInterstitial("late", 100));
            Assert.AreEqual("session_gate", policy.LastBlockReason);
            policy.OnFullscreenClosed(100);
            Assert.IsFalse(policy.CanShowInterstitial("any", 130));
            Assert.IsTrue(policy.CanShowInterstitial("fast", 106));
            Assert.IsTrue(policy.CanShowInterstitial("any", 130, skipCooldown: true));
        }

        [Test]
        public void ServerQueue_BatchesEvents_AndKeepsOrder()
        {
            var q = new SketchServerQueue { MaxItems = 250 };
            for (int i = 0; i < 120; i++) q.Enqueue(new SketchServerItem { Kind = "event", Json = "{\"i\":" + i + "}" });
            q.Enqueue(new SketchServerItem { Kind = "ad_revenue", Json = "{}" });
            Assert.IsTrue(q.TryPeekRequest(out string kind, out var items));
            Assert.AreEqual("event", kind);
            Assert.AreEqual(100, items.Count);
            StringAssert.StartsWith("{\"events\":[{\"i\":0},", SketchServerQueue.BuildBatchBody(items));
            q.Complete(items.Count);
            q.TryPeekRequest(out kind, out items);
            Assert.AreEqual(20, items.Count);
            q.Complete(20);
            q.TryPeekRequest(out kind, out items);
            Assert.AreEqual("ad_revenue", kind);
            Assert.AreEqual(1, items.Count);
            Assert.AreEqual(1.0, q.Fail());
            Assert.AreEqual(2.0, q.Fail());
            Assert.IsTrue(SketchServerQueue.IsPermanentRejection(400));
            Assert.IsTrue(SketchServerQueue.IsPermanentRejection(422));
            Assert.IsFalse(SketchServerQueue.IsPermanentRejection(404), "wrong URL keeps the queue");
            Assert.IsFalse(SketchServerQueue.IsPermanentRejection(413));
            Assert.IsFalse(SketchServerQueue.IsPermanentRejection(401));
            Assert.IsFalse(SketchServerQueue.IsPermanentRejection(429));
            Assert.IsFalse(SketchServerQueue.IsPermanentRejection(503));
        }

        [Test]
        public void ServerQueue_CapDropsOldest()
        {
            var q = new SketchServerQueue { MaxItems = 3 };
            for (int i = 0; i < 5; i++) q.Enqueue(new SketchServerItem { Kind = "event", Json = i.ToString() });
            Assert.AreEqual(3, q.Count);
            Assert.AreEqual("2", q.Items[0].Json);
            Assert.AreEqual(2, q.DroppedForCap);
        }

        [Test]
        public void ServerQueue_CapNeverTrimsTheRequestInFlight()
        {
            var q = new SketchServerQueue { MaxItems = 5 };
            for (int i = 0; i < 5; i++) q.Enqueue(new SketchServerItem { Kind = "event", Json = i.ToString() });
            q.TryPeekRequest(out _, out var sent, maxBatch: 3);
            Assert.AreEqual(3, sent.Count);
            q.Reserve(sent.Count);
            for (int i = 5; i < 9; i++) q.Enqueue(new SketchServerItem { Kind = "event", Json = i.ToString() });
            Assert.AreEqual("0", q.Items[0].Json, "in-flight head kept");
            Assert.AreEqual("2", q.Items[2].Json);
            q.Complete(sent.Count);
            Assert.AreEqual(2, q.Count);
            Assert.AreEqual("7", q.Items[0].Json, "oldest unsent items were the ones dropped");
            Assert.AreEqual(4, q.DroppedForCap);
        }

        [Test]
        public void Names_AreSanitizedForServer()
        {
            Assert.AreEqual("level_complete", SketchAnalytics.SanitizeName("level_complete"));
            Assert.AreEqual("e1st_open", SketchAnalytics.SanitizeName("1st open"));
            Assert.AreEqual(80, SketchAnalytics.SanitizeName(new string('a', 120)).Length);
            Assert.AreEqual("abc.d-1:2", SketchServerClient.Identifier("abc.d-1:2"));
            Assert.AreEqual("GPA_123_45", SketchServerClient.Identifier("GPA 123/45"));
            Assert.AreEqual("reach_rev_0_05", SketchRevenueMilestones.EventName(0.05f));
            Assert.AreEqual("reach_rev_1", SketchRevenueMilestones.EventName(1f));
            var crossed = new List<float>();
            SketchRevenueMilestones.Crossed(0.009, 0.2, new List<float> { 0.01f, 0.05f, 0.1f, 0.5f }, crossed);
            CollectionAssert.AreEqual(new[] { 0.01f, 0.05f, 0.1f }, crossed);
        }

        [Test]
        public void Settings_TestMode_SwapsOnlyFilledFormats()
        {
            var s = UnityEngine.ScriptableObject.CreateInstance<SketchSdkSettings>();
            s.Android.Rewarded = " ca-app-pub-1/2 ";
            s.Ios.Rewarded = " ca-app-pub-1/2 ";
            s.ForceTestAds = true;
            SketchAdMobConfig config = s.BuildAdMobConfig();
            Assert.IsTrue(SketchAdMobTestIds.IsTestId(config.RewardedId));
            Assert.AreEqual(string.Empty, config.InterstitialId, "blank format stays off in test mode");
            s.ForceTestAds = false;
            s.TestAdsInDevelopmentBuild = false;
            Assert.AreEqual("ca-app-pub-1/2", s.BuildAdMobConfig().RewardedId, "trimmed real id");
            UnityEngine.Object.DestroyImmediate(s);
        }
    }
}
