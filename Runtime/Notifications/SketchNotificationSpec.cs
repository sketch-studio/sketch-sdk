using System;

namespace Sketch.Sdk
{
    /// <summary>
    /// One re-engagement notification. It fires <see cref="Delay"/> after the player leaves the game
    /// (shifted out of quiet hours). Icons are the ids registered in Project Settings > Mobile Notifications (e.g. "icon_0");
    /// null keeps the platform default.
    /// </summary>
    public sealed class SketchNotificationSpec
    {
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public TimeSpan Delay { get; }
        public string SmallIcon { get; }
        public string LargeIcon { get; }

        public SketchNotificationSpec(string id, string title, string body, TimeSpan delay, string smallIcon = null, string largeIcon = null)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Delay = delay;
            SmallIcon = smallIcon;
            LargeIcon = largeIcon;
        }
    }
}
