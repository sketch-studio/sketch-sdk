namespace Sketch.Sdk
{
    public enum SketchIapResultStatus
    {
        /// <summary>Granted by the game and confirmed (or queued for confirmation) with the store.</summary>
        Success,
        /// <summary>A durable product that the player already owns; nothing was charged.</summary>
        AlreadyOwned,
        Cancelled,
        /// <summary>Waiting for approval (Ask to Buy, pending payment). The grant arrives later through SketchStore.Granted.</summary>
        Deferred,
        Failed,
        /// <summary>Store not ready, product unknown or not purchasable.</summary>
        Unavailable,
        /// <summary>Another purchase is still in progress.</summary>
        Busy,
    }
}
