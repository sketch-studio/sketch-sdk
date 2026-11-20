using System;

namespace Sketch.Sdk
{
    /// <summary>Platform notification backend (Android Mobile Notifications, Editor fake). Called on the main thread.</summary>
    public interface ISketchNotificationProvider
    {
        /// <summary>True when notifications can be posted (true below Android 13 unless the user blocked them).</summary>
        bool HasPermission { get; }

        /// <summary>Shows the OS permission dialog; done(granted) is invoked once on the main thread.</summary>
        void RequestPermission(Action<bool> done);

        /// <summary>Schedules one notification at a local time.</summary>
        void Schedule(SketchNotificationSpec spec, DateTime fireTimeLocal);

        /// <summary>Cancels scheduled notifications and clears the ones already shown.</summary>
        void CancelAll();
    }
}
