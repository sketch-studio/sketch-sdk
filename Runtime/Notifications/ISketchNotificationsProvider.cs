using System;

namespace Sketch.Sdk
{
    public interface ISketchNotificationsProvider
    {
        void Initialize(Action<bool> onInitialized);
        bool Schedule(SketchNotificationRequest request);
        bool Cancel(string id);
        void CancelAll();
        void ResetOnLaunch();
    }
}
