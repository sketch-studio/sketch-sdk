using System;

namespace Sketch.Sdk
{
    /// <summary>When the native review flow may be requested. All thresholds are inclusive minimums.</summary>
    [Serializable]
    public sealed class SketchReviewGateSettings
    {
        public bool Enabled = true;
        /// <summary>Minimum SDK session number (SketchIdentity.Session, 1 on first launch).</summary>
        public int MinSessions = 3;
        /// <summary>Minimum wins recorded with SketchReview.RecordWin().</summary>
        public int MinWins = 3;
        /// <summary>Days between two requests. 0 = no cooldown.</summary>
        public float CooldownDays = 30f;
        /// <summary>At most one request per app version (Application.version).</summary>
        public bool OncePerVersion = true;
        /// <summary>Open the store page when the native flow is unavailable or fails.</summary>
        public bool StoreFallback = true;
    }
}
