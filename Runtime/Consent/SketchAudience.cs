using System;
using UnityEngine;

namespace Sketch.Sdk
{
    /// <summary>Durable age bracket. Default is PlayerPrefs; a game may back it with its own save (SketchAudience.UseStore).</summary>
    public interface ISketchAgeStore
    {
        SketchAgeBracket Load();
        /// <summary>Must be durable when it returns.</summary>
        void Save(SketchAgeBracket bracket);
    }

    /// <summary>
    /// The player's age bracket, the input of every age-based ad signal. With SketchSdkSettings.RequireAgeSignal the SDK
    /// asks consent and starts ads only after a bracket is known (from the neutral age screen or the game).
    /// The bracket is written once: the age screen never re-opens to let a player change the answer.
    /// </summary>
    public static class SketchAudience
    {
        sealed class PlayerPrefsAgeStore : ISketchAgeStore
        {
            const string KEY = "sketchsdk.age_bracket";
            public SketchAgeBracket Load() => SketchAgePolicy.Parse(PlayerPrefs.GetString(KEY, string.Empty));
            public void Save(SketchAgeBracket bracket)
            {
                PlayerPrefs.SetString(KEY, SketchAgePolicy.Format(bracket));
                PlayerPrefs.Save();
            }
        }

        static ISketchAgeStore _store;
        static bool _loaded;
        static SketchAgeBracket _bracket;

        /// <summary>Raised on the main thread when a bracket becomes known.</summary>
        public static event Action Changed;

        static ISketchAgeStore Store => _store ??= new PlayerPrefsAgeStore();

        public static SketchAgeBracket Bracket
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    try { _bracket = Store.Load(); }
                    catch (Exception e) { SketchLog.Warn("Age store load failed: " + e.Message); _bracket = SketchAgeBracket.Unknown; }
                }
                return _bracket;
            }
        }

        public static bool IsKnown => Bracket != SketchAgeBracket.Unknown;

        /// <summary>Use the game's save for the bracket. Call before SketchSdkApp starts (e.g. AutoInitialize off) or at boot.</summary>
        public static void UseStore(ISketchAgeStore store)
        {
            _store = store;
            _loaded = false;
            if (IsKnown) RaiseChanged();
        }

        /// <summary>Record the bracket (age screen or game). Unknown is ignored; a known bracket is never overwritten.</summary>
        public static bool SetBracket(SketchAgeBracket bracket)
        {
            if (bracket == SketchAgeBracket.Unknown || IsKnown) return false;
            _bracket = bracket;
            try { Store.Save(bracket); }
            catch (Exception e) { SketchLog.Warn("Age store save failed: " + e.Message); }
            SketchAnalytics.Log("sketch_age_bracket", "minor", SketchAgePolicy.IsMinor(bracket));
            RaiseChanged();
            return true;
        }

        /// <summary>Ad signals for the current bracket under the settings' rating caps.</summary>
        public static SketchAgeAdSignals Signals(SketchSdkSettings settings) =>
            SketchAgePolicy.Resolve(Bracket, settings.MaxContentRating, settings.MinorMaxContentRating);

        /// <summary>Testing only: forget the stored bracket so the age screen shows again on next launch.</summary>
        public static void ResetForTesting()
        {
            _bracket = SketchAgeBracket.Unknown;
            _loaded = true;
            try { Store.Save(SketchAgeBracket.Unknown); }
            catch (Exception e) { SketchLog.Warn("Age store reset failed: " + e.Message); }
        }

        static void RaiseChanged()
        {
            try { Changed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _store = null;
            _loaded = false;
            _bracket = SketchAgeBracket.Unknown;
            Changed = null;
        }
    }
}
