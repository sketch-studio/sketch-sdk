using System;

namespace Sketch.Sdk
{
    /// <summary>Editor stand-in: logs what would be scheduled. The permission answer is configurable for testing.</summary>
    public sealed class SketchEditorNotificationProvider : ISketchNotificationProvider
    {
        public bool GrantOnRequest { get; set; } = true;
        public bool HasPermission { get; private set; }
        public int ScheduledCount { get; private set; }

        public void RequestPermission(Action<bool> done)
        {
            HasPermission = GrantOnRequest;
            SketchLog.Info("[Editor] Notification permission " + (HasPermission ? "granted" : "denied") + ".");
            done?.Invoke(HasPermission);
        }

        public void Schedule(SketchNotificationSpec spec, DateTime fireTimeLocal)
        {
            ScheduledCount++;
            SketchLog.Info($"[Editor] Notification '{spec?.Id}' at {fireTimeLocal:yyyy-MM-dd HH:mm}: {spec?.Title}");
        }

        public void CancelAll()
        {
            if (ScheduledCount > 0) SketchLog.Info("[Editor] Notifications cancelled.");
            ScheduledCount = 0;
        }
    }
}
