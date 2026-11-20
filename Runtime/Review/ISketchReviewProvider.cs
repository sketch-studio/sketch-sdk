using System;

namespace Sketch.Sdk
{
    /// <summary>Native in-app review backend (Google Play In-App Review, Editor fake).</summary>
    public interface ISketchReviewProvider
    {
        /// <summary>
        /// Runs the native flow; done(true) when it completed without error, done(false) when it could not be shown.
        /// Success does not mean the player rated (the platform never tells). Invoked once.
        /// </summary>
        void RequestReview(Action<bool> done);
    }
}
