using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>
    /// Pure scheduling rule for re-engagement notifications (no clock, no IO): fire time = last activity + delay,
    /// moved to the end of quiet hours (default 22:00-08:00 local) when it lands inside them, past times dropped,
    /// invalid or duplicate ids dropped, result ordered by fire time.
    /// </summary>
    public sealed class SketchNotificationSchedule
    {
        public const int DEFAULT_QUIET_START_HOUR = 22;
        public const int DEFAULT_QUIET_END_HOUR = 8;

        public int QuietStartHour { get; }
        public int QuietEndHour { get; }

        /// <summary>Quiet hours [start, end) in local hours 0-23; start == end disables them.</summary>
        public SketchNotificationSchedule(int quietStartHour = DEFAULT_QUIET_START_HOUR, int quietEndHour = DEFAULT_QUIET_END_HOUR)
        {
            QuietStartHour = Clamp(quietStartHour);
            QuietEndHour = Clamp(quietEndHour);
        }

        public bool IsQuiet(DateTime local)
        {
            if (QuietStartHour == QuietEndHour) return false;
            int hour = local.Hour;
            return QuietStartHour < QuietEndHour
                ? hour >= QuietStartHour && hour < QuietEndHour
                : hour >= QuietStartHour || hour < QuietEndHour;
        }

        /// <summary>Returns the time unchanged, or the next end of quiet hours when it falls inside them.</summary>
        public DateTime ShiftOutOfQuietHours(DateTime local)
        {
            if (!IsQuiet(local)) return local;
            DateTime end = local.Date.AddHours(QuietEndHour);
            if (end <= local) end = end.AddDays(1);
            return end;
        }

        /// <summary>
        /// Clears <paramref name="output"/> and fills it with the notifications to schedule, ordered by fire time.
        /// <paramref name="lastActiveLocal"/> is when the player was last in the game (normally the pause moment);
        /// a value in the future (clock change) is clamped to now. Returns the count. Disabled -> 0.
        /// </summary>
        public int Build(DateTime nowLocal, DateTime lastActiveLocal, IReadOnlyList<SketchNotificationSpec> specs, bool enabled, List<SketchScheduledNotification> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            output.Clear();
            if (!enabled || specs == null) return 0;
            DateTime anchor = lastActiveLocal > nowLocal ? nowLocal : lastActiveLocal;
            for (int i = 0; i < specs.Count; i++)
            {
                SketchNotificationSpec spec = specs[i];
                if (spec == null || string.IsNullOrEmpty(spec.Id) || spec.Delay <= TimeSpan.Zero) continue;
                if (Contains(output, spec.Id)) continue;
                DateTime fire = ShiftOutOfQuietHours(anchor + spec.Delay);
                if (fire <= nowLocal) continue;
                InsertSorted(output, new SketchScheduledNotification(spec, fire));
            }
            return output.Count;
        }

        static bool Contains(List<SketchScheduledNotification> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].Spec.Id, id, StringComparison.Ordinal)) return true;
            return false;
        }

        // Stable insertion: equal fire times keep config order. Lists are tiny (a handful of specs).
        static void InsertSorted(List<SketchScheduledNotification> list, SketchScheduledNotification item)
        {
            int index = list.Count;
            while (index > 0 && list[index - 1].FireTime > item.FireTime) index--;
            list.Insert(index, item);
        }

        static int Clamp(int hour) => hour < 0 ? 0 : hour > 23 ? 23 : hour;
    }
}
