namespace Sketch.Sdk
{
    /// <summary>Persisted review gate state (loaded and saved by SketchReview through its ISketchKeyValueStore).</summary>
    public struct SketchReviewState
    {
        public int Wins;
        public int Requests;
        public double LastRequestUnix;
        public string LastRequestVersion;
        public bool NeverAgain;
    }
}
