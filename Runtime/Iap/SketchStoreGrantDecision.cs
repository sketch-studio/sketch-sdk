namespace Sketch.Sdk
{
    public enum SketchStoreGrantDecision
    {
        /// <summary>Call the game's fulfillment, record it, then confirm.</summary>
        Grant,
        /// <summary>Already granted: confirm with the store (if it still needs it) without granting again.</summary>
        ConfirmOnly,
        /// <summary>Nothing to do (restored history the game already has, or a restored consumable).</summary>
        Ignore,
        /// <summary>Unknown product or missing transaction id: leave the order unfinished.</summary>
        Reject,
    }
}
