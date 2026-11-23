using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sketch.Sdk.Editor;
using UnityEngine;

namespace Sketch.Sdk.Tests
{
    public sealed class SketchRemoteKeysTests
    {
        [Test]
        public void OverrideLayer_WinsOverRemoteProviderAndDefault()
        {
            var values = new SketchConfigValues();
            values.SetDefault("k", 1.0);
            values.SetProvider(new Dictionary<string, object> { { "k", 2.0 } });
            values.SetRemote(new Dictionary<string, object> { { "k", 3.0 } }, "c");
            Assert.AreEqual(3, values.GetInt("k"));
            values.SetOverrides(new Dictionary<string, object> { { "k", "4" } });
            Assert.AreEqual(4, values.GetInt("k"));
            values.SetOverrides(null);
            Assert.AreEqual(3, values.GetInt("k"));
        }

        [Test]
        public void OverrideOnlyKey_IsListedAndReadable()
        {
            var values = new SketchConfigValues();
            values.SetOverrides(new Dictionary<string, object> { { "only", "true" } });
            Assert.IsTrue(values.Has("only"));
            Assert.IsTrue(values.GetBool("only"));
            Assert.IsTrue(values.Keys.Contains("only"));
        }

        [Test]
        public void KeyDef_FactoriesFormatDefaultsInvariant()
        {
            Assert.AreEqual("0.5", SketchRemoteKeyDef.Float("f", 0.5f, "d").DefaultValue);
            Assert.AreEqual("true", SketchRemoteKeyDef.Bool("b", true, "d").DefaultValue);
            Assert.AreEqual(SketchRemoteValueType.Int, SketchRemoteKeyDef.Int("i", 3, "d").Type);
        }

        [Test]
        public void SdkKeys_CoverEveryAdsKeyAndReadSettings()
        {
            var settings = ScriptableObject.CreateInstance<SketchSdkSettings>();
            settings.Pacing.InterstitialCooldownSeconds = 45f;
            var keys = SketchRemoteConfig.SdkKeys(settings);
            Object.DestroyImmediate(settings);
            Assert.AreEqual("45", keys.First(k => k.Key == SketchAdsRemoteKeys.INTER_COOLDOWN).DefaultValue);
            Assert.IsTrue(keys.All(k => !string.IsNullOrEmpty(k.Description)));
            Assert.AreEqual(keys.Count, keys.Select(k => k.Key).Distinct().Count());
        }

        [Test]
        public void FirebaseTemplate_HasTypedParameters()
        {
            var keys = new List<SketchRemoteKeyDef>
            {
                SketchRemoteKeyDef.Float("sketch_inter_cooldown", 30f, "Cooldown"),
                SketchRemoteKeyDef.Bool("flag", true, "Flag \"quoted\""),
                SketchRemoteKeyDef.Json("placements", "[]", "List"),
                SketchRemoteKeyDef.String("name", "a", "Name"),
            };
            string json = SketchRemoteExport.FirebaseTemplateJson(keys);
            Assert.IsTrue(SketchJson.TryParse(json, out object root), json);
            var parameters = (Dictionary<string, object>)((Dictionary<string, object>)root)["parameters"];
            var cooldown = (Dictionary<string, object>)parameters["sketch_inter_cooldown"];
            Assert.AreEqual("NUMBER", cooldown["valueType"]);
            Assert.AreEqual("30", ((Dictionary<string, object>)cooldown["defaultValue"])["value"]);
            Assert.AreEqual("BOOLEAN", ((Dictionary<string, object>)parameters["flag"])["valueType"]);
            Assert.AreEqual("JSON", ((Dictionary<string, object>)parameters["placements"])["valueType"]);
            Assert.AreEqual("STRING", ((Dictionary<string, object>)parameters["name"])["valueType"]);
        }

        [Test]
        public void DefaultsXml_EscapesValues()
        {
            string xml = SketchRemoteExport.DefaultsXml(new List<SketchRemoteKeyDef> { SketchRemoteKeyDef.String("k", "a<b&c", "d") });
            StringAssert.Contains("<key>k</key>", xml);
            StringAssert.Contains("<value>a&lt;b&amp;c</value>", xml);
            StringAssert.StartsWith("<?xml", xml);
        }

        [Test]
        public void OverridesJson_RoundTrips()
        {
            var map = new Dictionary<string, string> { { "a", "1" }, { "b", "x y" } };
            var back = SketchRemoteExport.ParseOverrides(SketchRemoteExport.OverridesToJson(map));
            CollectionAssert.AreEquivalent(map, back);
            Assert.IsEmpty(SketchRemoteExport.ParseOverrides("not json"));
        }
    }
}
