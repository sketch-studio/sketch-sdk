using System;
using System.Collections.Generic;

namespace Sketch.Sdk
{
    /// <summary>
    /// Reads the US-state "do not sell / share / target" choice out of an IAB GPP string (what Google UMP's US-states
    /// message stores as IABGPP_HDR_GppString + IABGPP_GppSID). Layouts come from the IAB Global-Privacy-Platform specs
    /// (Core: header + Fibonacci range; US National v1/v2 legacy and v3 header-style; US state sections 8-27 as of
    /// 2026-11) and are checked against IABTechLab/iabgpp-java vectors. Anything not understood fails safe to opted out.
    /// </summary>
    public static class SketchGpp
    {
        const int FIRST_US_SECTION = 7, LAST_US_SECTION = 27;
        const int HEADER_TYPE = 3;

        /// <summary>A known core layout: 2-bit fields after the version (legacy) or after the core start (header-style).</summary>
        sealed class Layout
        {
            public readonly bool HeaderStyle;
            public readonly int LeadingBits;
            public readonly int[] OptOutFieldIndexes;
            public readonly int FieldCount;
            public Layout(bool headerStyle, int leadingBits, int fieldCount, params int[] optOutFieldIndexes)
            {
                HeaderStyle = headerStyle;
                LeadingBits = leadingBits;
                FieldCount = fieldCount;
                OptOutFieldIndexes = optOutFieldIndexes;
            }
        }

        // Legacy sections start with Version Int(6); the fields listed below are the Int(2) fields that follow it, up to the
        // last opt-out field. Indexes name the opt-out fields (value 1 = consumer opted out).
        // usnat v1/v2: SharingNotice, SaleOptOutNotice, SharingOptOutNotice, TargetedAdvertisingOptOutNotice,
        //   SensitiveDataProcessingOptOutNotice, SensitiveDataLimitUseNotice, SaleOptOut, SharingOptOut, TargetedAdvertisingOptOut
        static readonly Layout UsNatLegacy = new Layout(false, 6, 9, 6, 7, 8);
        // usca: SaleOptOutNotice, SharingOptOutNotice, SensitiveDataLimitUseNotice, SaleOptOut, SharingOptOut
        static readonly Layout UsCa = new Layout(false, 6, 5, 3, 4);
        // usva, usco, usct, usmt (SharingNotice) and usfl, usor, ustx, usde, usne, usnh, usnj, ustn, usmn (ProcessingNotice):
        //   Notice, SaleOptOutNotice, TargetedAdvertisingOptOutNotice, SaleOptOut, TargetedAdvertisingOptOut
        static readonly Layout FiveField = new Layout(false, 6, 5, 3, 4);
        // usut (SensitiveDataProcessingOptOutNotice) and usia (SensitiveDataOptOutNotice): one more notice before the opt-outs
        static readonly Layout SixField = new Layout(false, 6, 6, 4, 5);
        // Header-style cores (after the section header piece). usnat v3: MspaVersion Int(6), MspaNotice, MspaOptOut
        static readonly Layout UsNatV3 = new Layout(true, 6, 2, 1);
        // usmd, usin, usky, usri v1: MspaVersion Int(6), MspaCoveredTransaction, MspaMode, ProcessingNotice,
        //   SaleOptOutNotice, TargetedAdvertisingOptOutNotice, SaleOptOut, TargetedAdvertisingOptOut
        static readonly Layout StateV1HeaderStyle = new Layout(true, 6, 7, 5, 6);

        static Layout LegacyLayout(int sectionId, int version)
        {
            switch (sectionId)
            {
                case 7: return version == 1 || version == 2 ? UsNatLegacy : null;
                case 8: return version == 1 ? UsCa : null;
                case 11: case 18: return version == 1 ? SixField : null;
                case 9: case 10: case 12: case 13: case 14: case 15: case 16: case 17: case 19: case 20: case 21: case 22: case 23:
                    return version == 1 ? FiveField : null;
                default: return null;
            }
        }

        static Layout HeaderStyleLayout(int sectionId, int version)
        {
            switch (sectionId)
            {
                case 7: return version == 3 ? UsNatV3 : null;
                case 24: case 25: case 26: case 27: return version == 1 ? StateV1HeaderStyle : null;
                default: return null;
            }
        }

        public static bool IsUsSection(int sectionId) => sectionId >= FIRST_US_SECTION && sectionId <= LAST_US_SECTION;

        /// <summary>
        /// null: no US section applies (nothing to forward). true: opted out of sale / sharing / targeted ads, GPC set, or a
        /// US section applies but cannot be read. false: US section read and no opt-out.
        /// </summary>
        /// <param name="gppString">IABGPP_HDR_GppString.</param>
        /// <param name="applicableSectionIds">IABGPP_GppSID, e.g. "7" or "7_8"; empty = every section in the string.</param>
        public static bool? UsOptOut(string gppString, string applicableSectionIds)
        {
            List<int> applicable = ParseSids(applicableSectionIds);
            bool sidListed = applicable.Count > 0;
            bool anyUsApplies = sidListed && applicable.Exists(IsUsSection);
            if (string.IsNullOrEmpty(gppString)) return anyUsApplies ? true : (bool?)null;
            if (sidListed && !anyUsApplies) return null;

            string[] pieces = gppString.Split('~');
            int[] ids;
            try { ids = HeaderSectionIds(pieces[0]); }
            catch (FormatException) { return anyUsApplies ? true : (bool?)null; }
            if (ids.Length != pieces.Length - 1) return anyUsApplies ? true : (bool?)null;

            bool? result = null;
            for (int i = 0; i < ids.Length; i++)
            {
                int sid = ids[i];
                if (!IsUsSection(sid) || (sidListed && !applicable.Contains(sid))) continue;
                if (SectionOptedOut(sid, pieces[i + 1])) return true;
                result = false;
            }
            if (sidListed)
            {
                // A listed US section that is missing from the string cannot be read: fail safe.
                foreach (int sid in applicable)
                    if (IsUsSection(sid) && Array.IndexOf(ids, sid) < 0) return true;
            }
            return result;
        }

        /// <summary>True when the section says opted out (or GPC), or when it cannot be decoded.</summary>
        public static bool SectionOptedOut(int sectionId, string section)
        {
            if (!IsUsSection(sectionId) || string.IsNullOrEmpty(section)) return true;
            try
            {
                string[] parts = section.Split('.');
                var first = new BitReader(parts[0]);
                int lead = first.Int(6);
                Layout layout;
                BitReader core;
                int nextPart;
                if (lead == sectionId && parts.Length >= 2)
                {
                    int version = first.Int(6);
                    layout = HeaderStyleLayout(sectionId, version);
                    core = new BitReader(parts[1]);
                    nextPart = 2;
                }
                else
                {
                    layout = LegacyLayout(sectionId, lead);
                    core = first;   // already past Version Int(6)
                    nextPart = 1;
                }
                if (layout == null) return true;
                if (CoreOptedOut(core, layout)) return true;
                for (int i = nextPart; i < parts.Length; i++)
                {
                    var sub = new BitReader(parts[i]);
                    if (sub.Int(2) == 1 && sub.Int(1) == 1) return true;   // GPC subsection, Gpc = true
                }
                return false;
            }
            catch (FormatException) { return true; }
        }

        static bool CoreOptedOut(BitReader core, Layout layout)
        {
            if (layout.HeaderStyle) core.Int(layout.LeadingBits);   // MspaVersion Int(6)
            for (int field = 0; field < layout.FieldCount; field++)
            {
                int value = core.Int(2);
                if (value == 1 && Array.IndexOf(layout.OptOutFieldIndexes, field) >= 0) return true;
            }
            return false;
        }

        /// <summary>Section ids of a GPP header piece (Type 3, Version, Range(Fibonacci)).</summary>
        public static int[] HeaderSectionIds(string header)
        {
            string piece = header.Split('~')[0];
            var reader = new BitReader(piece);
            if (reader.Int(6) != HEADER_TYPE) throw new FormatException("not a GPP header");
            reader.Int(6);
            int count = reader.Int(12);
            var ids = new List<int>(count);
            int last = 0;
            for (int i = 0; i < count; i++)
            {
                bool group = reader.Int(1) == 1;
                int start = last + reader.Fibonacci();
                ids.Add(start);
                last = start;
                if (!group) continue;
                int length = reader.Fibonacci();
                for (int k = 1; k <= length; k++) ids.Add(start + k);
                last = start + length;
            }
            return ids.ToArray();
        }

        static List<int> ParseSids(string sids)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(sids)) return list;
            foreach (string part in sids.Split('_', ',', ' '))
                if (int.TryParse(part, out int id) && id > 0) list.Add(id);
            return list;
        }

        /// <summary>MSB-first reader over GPP's base64url-style alphabet (also accepts + and /).</summary>
        sealed class BitReader
        {
            readonly string _text;
            int _bit;

            public BitReader(string text)
            {
                if (string.IsNullOrEmpty(text)) throw new FormatException("empty piece");
                _text = text;
            }

            int Bit()
            {
                int index = _bit / 6;
                if (index >= _text.Length) throw new FormatException("piece too short");
                int value = Sextet(_text[index]);
                int bit = (value >> (5 - _bit % 6)) & 1;
                _bit++;
                return bit;
            }

            public int Int(int bits)
            {
                int value = 0;
                for (int i = 0; i < bits; i++) value = (value << 1) | Bit();
                return value;
            }

            /// <summary>Fibonacci code (1, 2, 3, 5, 8, ...) terminated by two consecutive 1 bits.</summary>
            public int Fibonacci()
            {
                int a = 1, b = 2, value = 0, previous = 0;
                for (int i = 0; i < 32; i++)
                {
                    int bit = Bit();
                    if (bit == 1 && previous == 1) return value;
                    if (bit == 1) value += a;
                    previous = bit;
                    int next = a + b;
                    a = b;
                    b = next;
                }
                throw new FormatException("fibonacci code too long");
            }

            static int Sextet(char c)
            {
                if (c >= 'A' && c <= 'Z') return c - 'A';
                if (c >= 'a' && c <= 'z') return c - 'a' + 26;
                if (c >= '0' && c <= '9') return c - '0' + 52;
                if (c == '-' || c == '+') return 62;
                if (c == '_' || c == '/') return 63;
                throw new FormatException("invalid character");
            }
        }
    }
}
