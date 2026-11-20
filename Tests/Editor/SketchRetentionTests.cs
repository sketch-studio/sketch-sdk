using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Sketch.Sdk.Tests
{
    public sealed class SketchRetentionTests
    {
        static readonly DateTime Noon = new DateTime(2026, 11, 21, 12, 0, 0, DateTimeKind.Local);

        static SketchNotificationSpec Spec(string id, double hours) =>
            new SketchNotificationSpec(id, "t_" + id, "b_" + id, TimeSpan.FromHours(hours));

        // ---------- notification schedule ----------

        [Test]
        public void Schedule_FireTimeInsideQuietHours_MovesToNextMorning()
        {
            var schedule = new SketchNotificationSchedule();
            var output = new List<SketchScheduledNotification>();
            // 12:00 + 11h = 23:00 (quiet) -> next day 08:00; 12:00 + 15h = 03:00 (quiet) -> same next day 08:00.
            schedule.Build(Noon, Noon, new[] { Spec("late", 11), Spec("night", 15) }, true, output);
            Assert.AreEqual(2, output.Count);
            Assert.AreEqual(new DateTime(2026, 11, 22, 8, 0, 0), output[0].FireTime);
            Assert.AreEqual(new DateTime(2026, 11, 22, 8, 0, 0), output[1].FireTime);
        }

        [Test]
        public void Schedule_FireTimeOutsideQuietHours_IsUnchanged()
        {
            var schedule = new SketchNotificationSchedule();
            var output = new List<SketchScheduledNotification>();
            schedule.Build(Noon, Noon, new[] { Spec("d1", 24) }, true, output);
            Assert.AreEqual(Noon.AddHours(24), output[0].FireTime);
            Assert.IsFalse(schedule.IsQuiet(new DateTime(2026, 1, 1, 8, 0, 0)), "end hour is exclusive");
            Assert.IsTrue(schedule.IsQuiet(new DateTime(2026, 1, 1, 22, 0, 0)), "start hour is inclusive");
        }

        [Test]
        public void Schedule_OrdersByFireTime_DropsDuplicatesInvalidAndPast()
        {
            var schedule = new SketchNotificationSchedule();
            var output = new List<SketchScheduledNotification>();
            var specs = new[]
            {
                Spec("d3", 72), Spec("d1", 24), Spec("d1", 48), Spec("zero", 0), null,
                new SketchNotificationSpec("", "x", "y", TimeSpan.FromHours(5)), Spec("h2", 2),
            };
            // Player left 3 hours ago: "h2" (left + 2h) is already in the past.
            schedule.Build(Noon, Noon.AddHours(-3), specs, true, output);
            Assert.AreEqual(2, output.Count);
            Assert.AreEqual("d1", output[0].Spec.Id);
            Assert.AreEqual(Noon.AddHours(21), output[0].FireTime, "anchored at last activity, first d1 wins");
            Assert.AreEqual("d3", output[1].Spec.Id);
        }

        [Test]
        public void Schedule_LastActiveInFuture_ClampedToNow()
        {
            var schedule = new SketchNotificationSchedule(0, 0);   // quiet hours off
            var output = new List<SketchScheduledNotification>();
            schedule.Build(Noon, Noon.AddDays(5), new[] { Spec("d1", 1) }, true, output);
            Assert.AreEqual(Noon.AddHours(1), output[0].FireTime);
        }

        [Test]
        public void Schedule_Disabled_SchedulesNothing()
        {
            var schedule = new SketchNotificationSchedule();
            var output = new List<SketchScheduledNotification> { new SketchScheduledNotification(Spec("stale", 1), Noon) };
            Assert.AreEqual(0, schedule.Build(Noon, Noon, new[] { Spec("d1", 24) }, false, output));
            Assert.AreEqual(0, output.Count, "output is cleared");
        }

        [Test]
        public void KillSwitch_RemoteFalseDisables_MissingKeyEnables()
        {
            var values = new SketchConfigValues();
            Assert.IsTrue(SketchNotificationsRemoteKeys.IsEnabled(values));
            values.SetRemote(new Dictionary<string, object> { [SketchNotificationsRemoteKeys.ENABLED] = false }, "Default");
            Assert.IsFalse(SketchNotificationsRemoteKeys.IsEnabled(values));
        }

        // ---------- review gate ----------

        static readonly SketchReviewGateSettings Gate = new SketchReviewGateSettings { MinSessions = 3, MinWins = 2, CooldownDays = 10, OncePerVersion = true };
        const double Day = SketchReviewGate.SECONDS_PER_DAY;

        static bool Can(SketchReviewState state, int sessions, string version, double now, out string reason) =>
            SketchReviewGate.CanRequest(Gate, state, sessions, version, now, out reason);

        [Test]
        public void ReviewGate_SessionAndWinThresholds()
        {
            Assert.IsFalse(Can(new SketchReviewState { Wins = 5 }, 2, "1.0", 0, out string reason));
            Assert.AreEqual(SketchReviewGate.BLOCK_SESSIONS, reason);
            Assert.IsFalse(Can(new SketchReviewState { Wins = 1 }, 3, "1.0", 0, out reason));
            Assert.AreEqual(SketchReviewGate.BLOCK_WINS, reason);
            Assert.IsTrue(Can(new SketchReviewState { Wins = 2 }, 3, "1.0", 0, out reason), "thresholds are inclusive");
        }

        [Test]
        public void ReviewGate_OncePerVersion_ThenCooldown()
        {
            var asked = new SketchReviewState { Wins = 9, Requests = 1, LastRequestUnix = 1000, LastRequestVersion = "1.0" };
            Assert.IsFalse(Can(asked, 9, "1.0", 1000 + 100 * Day, out string reason));
            Assert.AreEqual(SketchReviewGate.BLOCK_VERSION, reason);
            Assert.IsFalse(Can(asked, 9, "1.1", 1000 + 9 * Day, out reason));
            Assert.AreEqual(SketchReviewGate.BLOCK_COOLDOWN, reason);
            Assert.IsTrue(Can(asked, 9, "1.1", 1000 + 10 * Day, out _));
            Assert.IsFalse(Can(asked, 9, "1.1", 500, out reason), "clock moved back stays in cooldown");
            Assert.AreEqual(SketchReviewGate.BLOCK_COOLDOWN, reason);
        }

        [Test]
        public void ReviewGate_NeverAgainAndDisabled_AlwaysBlock()
        {
            Assert.IsFalse(Can(new SketchReviewState { Wins = 9, NeverAgain = true }, 9, "1.0", 0, out string reason));
            Assert.AreEqual(SketchReviewGate.BLOCK_NEVER_AGAIN, reason);
            var off = new SketchReviewGateSettings { Enabled = false, MinSessions = 0, MinWins = 0 };
            Assert.IsFalse(SketchReviewGate.CanRequest(off, default, 9, "1.0", 0, out reason));
            Assert.AreEqual(SketchReviewGate.BLOCK_DISABLED, reason);
        }

        [Test]
        public void ReviewFacade_StateRoundTripsThroughInjectedStore()
        {
            ISketchKeyValueStore previous = SketchReview.Store;
            try
            {
                SketchReview.Store = new MemoryStore();
                SketchReview.RecordWin();
                SketchReview.RecordWin();
                Assert.AreEqual(2, SketchReview.State.Wins);
                Assert.IsFalse(SketchReview.State.NeverAgain);
                SketchReview.NeverAskAgain();
                Assert.IsTrue(SketchReview.State.NeverAgain);
                Assert.IsFalse(SketchReview.CanRequest(out string reason));
                Assert.AreEqual(SketchReviewGate.BLOCK_NEVER_AGAIN, reason);
            }
            finally { SketchReview.Store = previous; }
        }

        // ---------- force update ----------

        static SketchAppUpdateInfo Available(bool flexible = true, bool immediate = true) =>
            new SketchAppUpdateInfo { UpdateAvailable = true, AvailableVersionCode = 20, FlexibleAllowed = flexible, ImmediateAllowed = immediate };

        [Test]
        public void AppUpdate_BelowForceMin_Immediate()
        {
            Assert.AreEqual(SketchAppUpdateDecision.Immediate, SketchAppUpdatePolicy.Decide(Available(), 10, 15));
        }

        [Test]
        public void AppUpdate_AtOrAboveForceMinOrUnset_Flexible()
        {
            Assert.AreEqual(SketchAppUpdateDecision.Flexible, SketchAppUpdatePolicy.Decide(Available(), 15, 15));
            Assert.AreEqual(SketchAppUpdateDecision.Flexible, SketchAppUpdatePolicy.Decide(Available(), 10, 0));
            Assert.AreEqual(SketchAppUpdateDecision.Flexible, SketchAppUpdatePolicy.Decide(Available(), 0, 15), "unknown versionCode is never forced");
        }

        [Test]
        public void AppUpdate_NoUpdateOrNothingAllowed_None()
        {
            Assert.AreEqual(SketchAppUpdateDecision.None, SketchAppUpdatePolicy.Decide(default, 10, 15));
            Assert.AreEqual(SketchAppUpdateDecision.None, SketchAppUpdatePolicy.Decide(Available(false, false), 10, 15));
            Assert.AreEqual(SketchAppUpdateDecision.Flexible, SketchAppUpdatePolicy.Decide(Available(true, false), 10, 15), "forced but immediate not allowed falls back to flexible");
        }

        sealed class MemoryStore : ISketchKeyValueStore
        {
            readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
            readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
            public int GetInt(string key, int fallback = 0) => _ints.TryGetValue(key, out int v) ? v : fallback;
            public void SetInt(string key, int value) => _ints[key] = value;
            public string GetString(string key, string fallback = "") => _strings.TryGetValue(key, out string v) ? v : fallback;
            public void SetString(string key, string value) => _strings[key] = value;
            public void Save() { }
        }
    }
}
