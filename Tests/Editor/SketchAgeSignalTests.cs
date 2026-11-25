using NUnit.Framework;
using UnityEngine;

namespace Sketch.Sdk.Tests
{
    public sealed class SketchAgeSignalTests
    {
        sealed class MemoryAgeStore : ISketchAgeStore
        {
            public SketchAgeBracket Value;
            public int Saves;
            public SketchAgeBracket Load() => Value;
            public void Save(SketchAgeBracket bracket) { Value = bracket; Saves++; }
        }

        SketchSdkSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SketchSdkSettings>();
            _settings.Android = new SketchAdUnitIds { AppId = "ca-app-pub-1~1", Interstitial = "ca-app-pub-1/1" };
            _settings.MaxContentRating = SketchAdMobContentRating.Teen;
        }

        [TearDown]
        public void TearDown()
        {
            SketchAudience.UseStore(null);
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void UnknownAge_BuildsMinorAdMobConfig()
        {
            SketchAudience.UseStore(new MemoryAgeStore());
            _settings.RequireAgeSignal = true;
            SketchAdMobConfig config = _settings.BuildAdMobConfig();
            Assert.AreEqual(SketchAdMobTag.True, config.UnderAgeOfConsent);
            Assert.IsTrue(config.NonPersonalized);
            Assert.IsTrue(config.RestrictDataSale);
        }

        [Test]
        public void ChildBracket_SetsCoppaTagsAndGeneralRating()
        {
            SketchAudience.UseStore(new MemoryAgeStore { Value = SketchAgeBracket.Under13 });
            _settings.RequireAgeSignal = true;
            SketchAdMobConfig config = _settings.BuildAdMobConfig();
            Assert.AreEqual(SketchAdMobTag.True, config.ChildDirected);
            Assert.AreEqual(SketchAdMobContentRating.General, config.MaxContentRating);
        }

        [Test]
        public void AdultBracket_LeavesPersonalizationToConsent()
        {
            SketchAudience.UseStore(new MemoryAgeStore { Value = SketchAgeBracket.Adult });
            _settings.RequireAgeSignal = true;
            SketchAdMobConfig config = _settings.BuildAdMobConfig();
            Assert.AreEqual(SketchAdMobTag.False, config.UnderAgeOfConsent);
            Assert.IsFalse(config.NonPersonalized);
            Assert.IsFalse(config.RestrictDataSale);
            Assert.AreEqual(SketchAdMobContentRating.Teen, config.MaxContentRating);
        }

        [Test]
        public void AgeSignalOff_KeepsStaticSettings()
        {
            SketchAudience.UseStore(new MemoryAgeStore { Value = SketchAgeBracket.Under13 });
            _settings.ChildDirected = SketchAdMobTag.Unspecified;
            SketchAdMobConfig config = _settings.BuildAdMobConfig();
            Assert.AreEqual(SketchAdMobTag.Unspecified, config.ChildDirected);
            Assert.IsFalse(config.NonPersonalized);
        }

        [Test]
        public void SetBracket_WritesOnceAndRaisesChanged()
        {
            var store = new MemoryAgeStore();
            SketchAudience.UseStore(store);
            int changed = 0;
            void Count() => changed++;
            SketchAudience.Changed += Count;
            try
            {
                Assert.IsFalse(SketchAudience.SetBracket(SketchAgeBracket.Unknown));
                Assert.IsTrue(SketchAudience.SetBracket(SketchAgeBracket.Age13To15));
                Assert.IsFalse(SketchAudience.SetBracket(SketchAgeBracket.Adult), "the answer can not be changed afterwards");
                Assert.AreEqual(SketchAgeBracket.Age13To15, SketchAudience.Bracket);
                Assert.AreEqual(1, store.Saves);
                Assert.AreEqual(1, changed);
            }
            finally { SketchAudience.Changed -= Count; }
        }
    }
}
