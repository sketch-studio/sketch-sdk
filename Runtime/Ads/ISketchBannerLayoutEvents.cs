using System;

namespace Sketch.Sdk
{
    /// <summary>
    /// Optional capability for asynchronous banner occupancy changes. Implementations must raise the event on the Unity main thread.
    /// Legacy banner providers can omit this capability; their current height remains available through polling.
    /// </summary>
    public interface ISketchBannerLayoutEvents
    {
        event Action<float> BannerLayoutChanged;
    }
}
