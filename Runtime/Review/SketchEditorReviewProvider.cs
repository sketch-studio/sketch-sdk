using System;

namespace Sketch.Sdk
{
    /// <summary>Editor stand-in: logs and answers with <see cref="Succeed"/> (false exercises the store fallback).</summary>
    public sealed class SketchEditorReviewProvider : ISketchReviewProvider
    {
        public bool Succeed { get; set; } = true;

        public void RequestReview(Action<bool> done)
        {
            SketchLog.Info("[Editor] Native review flow " + (Succeed ? "shown." : "failed."));
            done?.Invoke(Succeed);
        }
    }
}
