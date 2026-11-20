namespace Sketch.Sdk
{
    /// <summary>Outcome of one SketchStore.Purchase call (the callback fires exactly once).</summary>
    public readonly struct SketchIapResult
    {
        public readonly string ProductId;
        public readonly SketchIapResultStatus Status;
        public readonly string TransactionId;
        public readonly string Message;

        public SketchIapResult(string productId, SketchIapResultStatus status, string transactionId = null, string message = null)
        {
            ProductId = productId ?? string.Empty;
            Status = status;
            TransactionId = transactionId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        /// <summary>True when the player has the product now (granted, or already owned).</summary>
        public bool IsSuccess => Status == SketchIapResultStatus.Success || Status == SketchIapResultStatus.AlreadyOwned;

        public override string ToString() => string.IsNullOrEmpty(Message) ? ProductId + ": " + Status : ProductId + ": " + Status + " (" + Message + ")";
    }
}
