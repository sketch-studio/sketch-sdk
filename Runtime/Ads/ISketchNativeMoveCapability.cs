using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Optional provider capability (1.5.14): move or resize the native that is on screen, keeping the same ad.</summary>
    public interface ISketchNativeMoveCapability
    {
        /// <summary>screenPixels: Unity screen rect, bottom-left origin. False when no native is shown or the view cannot move.</summary>
        bool MoveNative(Rect screenPixels);
    }
}
