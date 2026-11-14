using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Sketch.Sdk
{
    /// <summary>
    /// Main-thread tick and dispatcher for the SDK without a scene GameObject: one system is inserted into the PlayerLoop Update phase.
    /// Post() is thread-safe and runs the action on the next Update.
    /// </summary>
    public static class SketchSdkLoop
    {
        struct SketchSdkUpdate { }

        static readonly List<Action> Queue = new List<Action>();
        static readonly List<Action> Draining = new List<Action>();
        static readonly List<Action> Tickers = new List<Action>();
        static readonly object Gate = new object();
        static readonly List<Action> TickSnapshot = new List<Action>();
        static bool _installed;
        static int _mainThreadId = -1;
        static int _lastTickFrame = -1;

        /// <summary>Raised every frame on the main thread.</summary>
        public static event Action Update
        {
            add { EnsureInstalled(); if (value != null && !Tickers.Contains(value)) Tickers.Add(value); }
            remove { Tickers.Remove(value); }
        }

        public static bool IsMainThread => _mainThreadId == System.Threading.Thread.CurrentThread.ManagedThreadId;

        /// <summary>Run on the main thread at the next Update (immediately queued, never inline).</summary>
        public static void Post(Action action)
        {
            if (action == null) return;
            lock (Gate) Queue.Add(action);
        }

        /// <summary>Run inline when already on the main thread, otherwise queue.</summary>
        public static void Run(Action action)
        {
            if (action == null) return;
            if (IsMainThread) Invoke(action);
            else Post(action);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            lock (Gate) Queue.Clear();
            Tickers.Clear();
            _installed = false;
            _lastTickFrame = -1;
            EnsureInstalled();
            Application.onBeforeRender -= Heal;
            Application.onBeforeRender += Heal;
        }

        /// <summary>Another plugin may rebuild the PlayerLoop from the default one; put the SDK tick back when it stops running.</summary>
        static void Heal()
        {
            if (!Application.isPlaying) return;
            int frame = Time.frameCount;
            if (_lastTickFrame >= 0 && frame - _lastTickFrame < 3) return;
            if (_lastTickFrame < 0 && frame < 3) return;
            _installed = false;
            EnsureInstalled();
            _lastTickFrame = frame;
        }

        static void EnsureInstalled()
        {
            if (_installed) return;
            if (_mainThreadId < 0) _mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            if (root.subSystemList == null) return;
            for (int i = 0; i < root.subSystemList.Length; i++)
            {
                if (root.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.Update)) continue;
                PlayerLoopSystem update = root.subSystemList[i];
                var subs = new List<PlayerLoopSystem>(update.subSystemList ?? Array.Empty<PlayerLoopSystem>());
                subs.RemoveAll(s => s.type == typeof(SketchSdkUpdate));
                subs.Add(new PlayerLoopSystem { type = typeof(SketchSdkUpdate), updateDelegate = Tick });
                update.subSystemList = subs.ToArray();
                root.subSystemList[i] = update;
                PlayerLoop.SetPlayerLoop(root);
                _installed = true;
                return;
            }
        }

        static void Tick()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            _lastTickFrame = Time.frameCount;
            lock (Gate)
            {
                if (Queue.Count > 0)
                {
                    Draining.AddRange(Queue);
                    Queue.Clear();
                }
            }
            if (Draining.Count > 0)
            {
                for (int i = 0; i < Draining.Count; i++) Invoke(Draining[i]);
                Draining.Clear();
            }
            // Snapshot: a ticker may add or remove tickers (its own or others) while running.
            TickSnapshot.Clear();
            TickSnapshot.AddRange(Tickers);
            for (int i = 0; i < TickSnapshot.Count; i++)
                if (Tickers.Contains(TickSnapshot[i])) Invoke(TickSnapshot[i]);
            TickSnapshot.Clear();
        }

        static void Invoke(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
