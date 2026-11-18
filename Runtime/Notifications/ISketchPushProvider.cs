using System;

namespace Sketch.Sdk
{
    public interface ISketchPushProvider
    {
        void Initialize(Action<bool> onInitialized);
        void RequestPermission(Action<bool> onGranted);
        void GetToken(Action<string> onToken);
        void DeleteToken(Action<bool> onDeleted);
    }
}
