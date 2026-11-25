using System;

namespace Sketch.Sdk
{
    /// <summary>
    /// Optional capability: open the ad network's debug inspector (AdMob Ad Inspector: per ad unit waterfall, adapter status, test ads).
    /// onClosed gets null on success or an error message (AdMob only opens it on a registered test device).
    /// </summary>
    public interface ISketchAdInspectorProvider
    {
        void OpenAdInspector(Action<string> onClosed);
    }
}
