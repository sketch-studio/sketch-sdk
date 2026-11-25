using Sketch.Sdk;

// Vectors: IAB GPP Core spec header examples and IABTechLab/iabgpp-java tests (UsNat, UsCa, UsVa, UsTx, UsMd, GppModel).
static class GppTests
{
    public static int Run()
    {
        int n = 0;
        void Check(bool condition, string message)
        {
            n++;
            if (!condition) throw new Exception("FAIL: " + message);
        }

        Check(string.Join(",", SketchGpp.HeaderSectionIds("DBABM")) == "2", "header example 1 -> [2]");
        Check(string.Join(",", SketchGpp.HeaderSectionIds("DBACNY")) == "2,6", "header example 2 -> [2,6]");
        Check(string.Join(",", SketchGpp.HeaderSectionIds("DBABjw")) == "5,6", "header example 3 (group) -> [5,6]");
        Check(string.Join(",", SketchGpp.HeaderSectionIds("DBABLA")) == "7", "usnat header -> [7]");

        // Section level: true = opted out, false = present without opt-out.
        Check(SketchGpp.SectionOptedOut(7, "CVVVkkkkkpFY.YA") == true, "usnat v2 all opt-outs = 1");
        Check(SketchGpp.SectionOptedOut(7, "BVQqAAAACg") == false, "usnat v1 opt-outs = 2 (did not opt out)");
        Check(SketchGpp.SectionOptedOut(7, "CAAAAAAAAABA.QA") == false, "usnat defaults (0 = not applicable)");
        Check(SketchGpp.SectionOptedOut(8, "BVWSSSVY") == true, "usca opted out");
        Check(SketchGpp.SectionOptedOut(8, "BAAAAABA.QA") == false, "usca defaults");
        Check(SketchGpp.SectionOptedOut(9, "BVWSSVY") == true, "usva opted out");
        Check(SketchGpp.SectionOptedOut(9, "BAAAABA") == false, "usva defaults");
        Check(SketchGpp.SectionOptedOut(16, "BVWSSVWA") == true, "ustx opted out");
        Check(SketchGpp.SectionOptedOut(24, "YBABYA.BVVU.YA") == true, "usmd header-style section opted out");
        Check(SketchGpp.SectionOptedOut(24, "YBAA.AQAA") == false, "usmd header-style defaults");
        Check(SketchGpp.SectionOptedOut(8, "BAAAAABA.YA") == true, "GPC subsection = true counts as opt-out");
        Check(SketchGpp.SectionOptedOut(7, "B") == true, "truncated section fails safe to opted out");
        Check(SketchGpp.SectionOptedOut(7, "B*%") == true, "invalid characters fail safe to opted out");
        Check(SketchGpp.SectionOptedOut(28, "BAAAA") == true, "unknown US section id fails safe to opted out");
        Check(SketchGpp.SectionOptedOut(8, "CAAAAABA") == true, "unknown section version fails safe to opted out");

        // Whole string + applicable section ids (IABGPP_GppSID).
        Check(SketchGpp.UsOptOut("DBABLA~CVVVkkkkkpFY.YA", "7") == true, "string: usnat opted out");
        Check(SketchGpp.UsOptOut("DBABLA~BVQqAAAACg", "7") == false, "string: usnat not opted out");
        Check(SketchGpp.UsOptOut("DBABLA~CVVVkkkkkpFY.YA", "2") == null, "only TCF applies: US section ignored");
        Check(SketchGpp.UsOptOut("DBABM~CPSG_8APSG_8ANwAAAENAwCAAAAAAAAAAAAAAAAAAAAA.IAAA", "2") == null, "EU only: no US decision");
        Check(SketchGpp.UsOptOut(null, null) == null && SketchGpp.UsOptOut("", "") == null, "no GPP stored: no US decision");
        Check(SketchGpp.UsOptOut("DBABLA~CVVVkkkkkpFY.YA", null) == true, "no SID list: every US section in the string counts");
        Check(SketchGpp.UsOptOut("garbage~x", "7") == true, "US section applies but string unreadable: fail safe");
        const string all = "DBACOcGA~CPSG_8APSG_8AAAAAAENAAFgAAAAAAAAAAAAAAAAAAAA.IAAA~BPSG_8APSG_8AAAAAAENAACAAAAAAAAAAAAAAAAAAA.YAAAAAAAAAA~1---~CAAAAAAAAABA.QA~BAAAAABA.QA~BAAAABA~BAAAAEA.QA~BAAAAAQA~BAAAAAEA.QA~BAAAAABA~BAAAAABA.QA~BAAAAAABAA.QA~BAAAAAQA.QA~BAAAAAABAA.QA~BAAAAAQA.QA~BAAAAAQA.QA~BAAAAABA.QA~BAAAAAAAQA.QA~BAAAAAQA.QA~BAAAAAQA.QA~YBAA.AQAA~ZBAA.AQAA~aBAA.AQAA~bBAA.AQAA";
        Check(SketchGpp.HeaderSectionIds(all).Length == 24, "iabgpp-java all-sections header: 2, 5, 6 and 7..27");
        Check(SketchGpp.UsOptOut(all, "7_8_9_10_11_12_13_14_15_16_17_18_19_20_21_22_23_24_25_26_27") == false, "all 21 US sections at defaults decode without fail-safe");
        // Field-position vectors encoded from the spec tables: every notice = 1 (yes), every opt-out = 2 (did not opt
        // out) must read false; each opt-out alone = 1 must read true. Catches a shifted field index.
        void Layout(int sid, int version, bool headerStyle, string[] fields)
        {
            var values = new int[fields.Length];
            for (int i = 0; i < fields.Length; i++) values[i] = fields[i].EndsWith("OptOut") ? 2 : 1;
            Check(SketchGpp.SectionOptedOut(sid, Encode(sid, version, headerStyle, values)) == false, $"sid {sid}: notices only, no opt-out");
            for (int i = 0; i < fields.Length; i++)
            {
                if (!fields[i].EndsWith("OptOut")) continue;
                var one = (int[])values.Clone();
                one[i] = 1;
                Check(SketchGpp.SectionOptedOut(sid, Encode(sid, version, headerStyle, one)) == true, $"sid {sid}: {fields[i]} = 1 is an opt-out");
            }
        }
        string[] usnat = { "SharingNotice", "SaleOptOutNotice", "SharingOptOutNotice", "TargetedAdvertisingOptOutNotice", "SensitiveDataProcessingOptOutNotice", "SensitiveDataLimitUseNotice", "SaleOptOut", "SharingOptOut", "TargetedAdvertisingOptOut" };
        Layout(7, 1, false, usnat);
        Layout(7, 2, false, usnat);
        Layout(8, 1, false, new[] { "SaleOptOutNotice", "SharingOptOutNotice", "SensitiveDataLimitUseNotice", "SaleOptOut", "SharingOptOut" });
        string[] five = { "SharingNotice", "SaleOptOutNotice", "TargetedAdvertisingOptOutNotice", "SaleOptOut", "TargetedAdvertisingOptOut" };
        foreach (int sid in new[] { 9, 10, 12, 13, 14, 15, 16, 17, 19, 20, 21, 22, 23 }) Layout(sid, 1, false, five);
        string[] six = { "SharingNotice", "SaleOptOutNotice", "TargetedAdvertisingOptOutNotice", "SensitiveDataProcessingOptOutNotice", "SaleOptOut", "TargetedAdvertisingOptOut" };
        Layout(11, 1, false, six);
        Layout(18, 1, false, six);
        Layout(7, 3, true, new[] { "MspaNotice", "MspaOptOut" });
        string[] stateHeader = { "MspaCoveredTransaction", "MspaMode", "ProcessingNotice", "SaleOptOutNotice", "TargetedAdvertisingOptOutNotice", "SaleOptOut", "TargetedAdvertisingOptOut" };
        foreach (int sid in new[] { 24, 25, 26, 27 }) Layout(sid, 1, true, stateHeader);
        return n;
    }

    /// <summary>Legacy: Version Int(6) + Int(2) fields. Header-style: header piece (SectionId, Version, empty range) + core piece (MspaVersion Int(6) + fields).</summary>
    static string Encode(int sid, int version, bool headerStyle, int[] twoBitFields)
    {
        var core = new System.Text.StringBuilder();
        core.Append(Convert.ToString(headerStyle ? 1 : version, 2).PadLeft(6, '0'));
        foreach (int v in twoBitFields) core.Append(Convert.ToString(v, 2).PadLeft(2, '0'));
        if (!headerStyle) return Sextets(core.ToString());
        string header = Convert.ToString(sid, 2).PadLeft(6, '0') + Convert.ToString(version, 2).PadLeft(6, '0') + new string('0', 12);
        return Sextets(header) + "." + Sextets(core.ToString());
    }

    static string Sextets(string bits)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        while (bits.Length % 6 != 0) bits += "0";
        var text = new System.Text.StringBuilder();
        for (int i = 0; i < bits.Length; i += 6) text.Append(alphabet[Convert.ToInt32(bits.Substring(i, 6), 2)]);
        return text.ToString();
    }
}
