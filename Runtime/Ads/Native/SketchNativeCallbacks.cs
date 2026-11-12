using System;

namespace Sketch.Sdk
{
    public sealed class SketchNativeCallbacks
    {
        public Action OnShown;
        public Action OnClicked;
        public Action<SketchNativeCloseReason> OnClosed;

        public void Shown() => OnShown?.Invoke();
        public void Clicked() => OnClicked?.Invoke();
        public void Closed(SketchNativeCloseReason reason) => OnClosed?.Invoke(reason);

        public sealed class Gate
        {
            readonly Action _shown;
            readonly Action _clicked;
            readonly Action<SketchNativeCloseReason> _closed;
            bool _decided;
            bool _accepted;
            bool _pendingShown;
            bool _pendingClicked;
            bool _pendingClosed;
            bool _shownDelivered;
            bool _closedDelivered;
            SketchNativeCloseReason _pendingReason;

            public Gate(Action shown, Action clicked, Action<SketchNativeCloseReason> closed)
            {
                _shown = shown;
                _clicked = clicked;
                _closed = closed;
                ProviderCallbacks = new SketchNativeCallbacks
                {
                    OnShown = ProviderShown,
                    OnClicked = ProviderClicked,
                    OnClosed = ProviderClosed,
                };
            }

            public SketchNativeCallbacks ProviderCallbacks { get; }

            public void Accept()
            {
                if (_decided) return;
                _decided = true;
                _accepted = true;
                if (_pendingShown) DeliverShown();
                if (_pendingClicked) _clicked?.Invoke();
                if (_pendingClosed) DeliverClosed(_pendingReason);
            }

            public void Reject()
            {
                if (_decided) return;
                _decided = true;
                _accepted = false;
            }

            void ProviderShown()
            {
                if (!_decided) { _pendingShown = true; return; }
                if (_accepted) DeliverShown();
            }

            void ProviderClicked()
            {
                if (!_decided) { _pendingClicked = true; return; }
                if (_accepted) _clicked?.Invoke();
            }

            void ProviderClosed(SketchNativeCloseReason reason)
            {
                if (!_decided) { _pendingClosed = true; _pendingReason = reason; return; }
                if (_accepted) DeliverClosed(reason);
            }

            void DeliverShown()
            {
                if (_shownDelivered) return;
                _shownDelivered = true;
                _shown?.Invoke();
            }

            void DeliverClosed(SketchNativeCloseReason reason)
            {
                if (_closedDelivered) return;
                _closedDelivered = true;
                _closed?.Invoke(reason);
            }
        }
    }
}
