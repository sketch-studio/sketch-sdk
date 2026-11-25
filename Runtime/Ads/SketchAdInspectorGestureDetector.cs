namespace Sketch.Sdk
{
    /// <summary>Debug gesture: four fingers held for two seconds fires once per hold (opens AdMob Ad Inspector in development builds).</summary>
    public sealed class SketchAdInspectorGestureDetector
    {
        public const int FINGERS = 4;
        public const double HOLD_SECONDS = 2.0;

        double _holdStart = -1;
        bool _fired;

        public bool Step(int touchCount, double now)
        {
            if (touchCount < FINGERS)
            {
                _holdStart = -1;
                _fired = false;
                return false;
            }
            if (_holdStart < 0) _holdStart = now;
            if (_fired || now - _holdStart < HOLD_SECONDS) return false;
            _fired = true;
            return true;
        }
    }
}
