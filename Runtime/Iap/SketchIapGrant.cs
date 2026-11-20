namespace Sketch.Sdk
{
    /// <summary>What the game must grant in its fulfillment callback.</summary>
    public readonly struct SketchIapGrant
    {
        public readonly SketchIapProduct Product;
        public readonly string TransactionId;
        public readonly int Quantity;
        public readonly bool IsRestore;

        public SketchIapGrant(SketchIapProduct product, string transactionId, int quantity, bool isRestore)
        {
            Product = product;
            TransactionId = transactionId;
            Quantity = quantity < 1 ? 1 : quantity;
            IsRestore = isRestore;
        }
    }
}
