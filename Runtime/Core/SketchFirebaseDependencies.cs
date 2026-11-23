using System;
using System.Threading.Tasks;

namespace Sketch.Sdk
{
    /// <summary>
    /// OR-824: one shared FirebaseApp.CheckAndFixDependenciesAsync for every SDK module (analytics, remote config, ...).
    /// Firebase forbids any other Firebase call while that check runs, so two modules starting it in parallel throw
    /// InvalidOperationException on device. Modules pass the check delegate and all await the same task.
    /// </summary>
    public static class SketchFirebaseDependencies
    {
        static readonly object Gate = new object();
        static Task _check;

        public static Task<T> Once<T>(Func<Task<T>> startCheck)
        {
            lock (Gate)
            {
                if (_check == null) _check = startCheck();
                return (Task<T>)_check;
            }
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetForTests()
        {
            lock (Gate) _check = null;
        }
    }
}
