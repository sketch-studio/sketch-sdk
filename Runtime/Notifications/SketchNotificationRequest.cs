using System;

namespace Sketch.Sdk
{
    public sealed class SketchNotificationRequest
    {
        public string Id { get; }
        public string Title { get; }
        public string Body { get; }
        public string Subtitle { get; }
        public string SmallIcon { get; }
        public string LargeIcon { get; }
        public DateTimeOffset FireAt { get; }
        public TimeSpan? RepeatEvery { get; }
        public int RepeatCycles { get; }

        public SketchNotificationRequest(string id, string title, string body, DateTimeOffset fireAt,
            string subtitle = null, string smallIcon = null, string largeIcon = null,
            TimeSpan? repeatEvery = null, int repeatCycles = 1)
        {
            Id = Normalize(id);
            Title = Normalize(title);
            Body = Normalize(body);
            Subtitle = Normalize(subtitle);
            SmallIcon = Normalize(smallIcon);
            LargeIcon = Normalize(largeIcon);
            FireAt = fireAt;
            RepeatEvery = repeatEvery;
            RepeatCycles = repeatEvery.HasValue ? Math.Max(1, Math.Min(3, repeatCycles)) : 1;
        }

        public bool IsValid => Id.Length > 0 && Title.Length > 0 && Body.Length > 0 && FireAt > DateTimeOffset.UtcNow;
        static string Normalize(string value) => (value ?? string.Empty).Trim();
    }
}
