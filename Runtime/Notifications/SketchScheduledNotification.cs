using System;

namespace Sketch.Sdk
{
    /// <summary>A spec with its computed local fire time (output of <see cref="SketchNotificationSchedule"/>).</summary>
    public readonly struct SketchScheduledNotification
    {
        public readonly SketchNotificationSpec Spec;
        public readonly DateTime FireTime;

        public SketchScheduledNotification(SketchNotificationSpec spec, DateTime fireTime)
        {
            Spec = spec;
            FireTime = fireTime;
        }
    }
}
