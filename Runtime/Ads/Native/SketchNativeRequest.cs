using UnityEngine;

namespace Sketch.Sdk
{
    public struct SketchNativeRequest
    {
        public string Placement;
        public Rect ScreenRect;
        public float AttachTimeoutSeconds;
        public bool CloseInsideAd;

        public SketchNativeRequest(string placement, Rect screenRect)
        {
            Placement = placement;
            ScreenRect = screenRect;
            AttachTimeoutSeconds = -1f;
            CloseInsideAd = false;
        }
    }
}
