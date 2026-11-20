namespace Sketch.Sdk
{
    /// <summary>Pure rule: may the native review flow be requested now? No clock, no IO.</summary>
    public static class SketchReviewGate
    {
        public const string BLOCK_DISABLED = "disabled";
        public const string BLOCK_NEVER_AGAIN = "never_again";
        public const string BLOCK_SESSIONS = "min_sessions";
        public const string BLOCK_WINS = "min_wins";
        public const string BLOCK_VERSION = "already_asked_this_version";
        public const string BLOCK_COOLDOWN = "cooldown";

        public const double SECONDS_PER_DAY = 86400d;

        public static bool CanRequest(SketchReviewGateSettings settings, SketchReviewState state, int sessions, string appVersion, double nowUnix, out string blockReason)
        {
            settings = settings ?? new SketchReviewGateSettings();
            if (!settings.Enabled) { blockReason = BLOCK_DISABLED; return false; }
            if (state.NeverAgain) { blockReason = BLOCK_NEVER_AGAIN; return false; }
            if (sessions < settings.MinSessions) { blockReason = BLOCK_SESSIONS; return false; }
            if (state.Wins < settings.MinWins) { blockReason = BLOCK_WINS; return false; }
            if (state.Requests > 0)
            {
                if (settings.OncePerVersion && string.Equals(state.LastRequestVersion ?? string.Empty, appVersion ?? string.Empty, System.StringComparison.Ordinal))
                {
                    blockReason = BLOCK_VERSION;
                    return false;
                }
                // A clock moved backwards (now < last) also counts as inside the cooldown.
                if (settings.CooldownDays > 0f && nowUnix - state.LastRequestUnix < settings.CooldownDays * SECONDS_PER_DAY)
                {
                    blockReason = BLOCK_COOLDOWN;
                    return false;
                }
            }
            blockReason = string.Empty;
            return true;
        }
    }
}
