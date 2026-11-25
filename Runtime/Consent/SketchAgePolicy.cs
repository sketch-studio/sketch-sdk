namespace Sketch.Sdk
{
    /// <summary>Age brackets of Google Play's default age ranges (and of a neutral in-game age screen).</summary>
    public enum SketchAgeBracket { Unknown = 0, Under13 = 1, Age13To15 = 2, Age16To17 = 3, Adult = 4 }

    /// <summary>Ad request signals for one age bracket (AdMob RequestConfiguration, UMP, npa extra, analytics ad consent).</summary>
    public readonly struct SketchAgeAdSignals
    {
        public readonly SketchAdMobTag ChildDirected;
        public readonly SketchAdMobTag UnderAgeOfConsent;
        public readonly SketchAdMobContentRating MaxContentRating;
        /// <summary>Send npa=1 on every ad request (contextual ads only).</summary>
        public readonly bool NonPersonalized;
        /// <summary>Analytics may grant ad_user_data / ad_personalization (still subject to UMP consent).</summary>
        public readonly bool AdPersonalizationAllowed;

        public SketchAgeAdSignals(SketchAdMobTag childDirected, SketchAdMobTag underAgeOfConsent, SketchAdMobContentRating maxContentRating,
            bool nonPersonalized, bool adPersonalizationAllowed)
        {
            ChildDirected = childDirected;
            UnderAgeOfConsent = underAgeOfConsent;
            MaxContentRating = maxContentRating;
            NonPersonalized = nonPersonalized;
            AdPersonalizationAllowed = adPersonalizationAllowed;
        }

        public override string ToString() =>
            $"tfcd={ChildDirected} tfua={UnderAgeOfConsent} max_rating={MaxContentRating} npa={NonPersonalized} ad_personalization={AdPersonalizationAllowed}";
    }

    /// <summary>
    /// Pure age rules. A player is never assumed adult: an unknown age gets minor treatment, and a birth year counts as the
    /// younger of its two possible ages. Under 13: COPPA child treatment (TFCD + TFUA, G). 13-15: under the GDPR digital
    /// age of consent in part of the EEA (TFUA). Every minor gets contextual (npa) ads capped at the minor rating.
    /// </summary>
    public static class SketchAgePolicy
    {
        const int MAX_AGE = 120;

        public static SketchAgeBracket FromBirthYear(int birthYear, int currentYear)
        {
            if (birthYear > currentYear) return SketchAgeBracket.Unknown;
            int youngestAge = currentYear - birthYear - 1;
            if (youngestAge > MAX_AGE) return SketchAgeBracket.Unknown;
            if (youngestAge < 13) return SketchAgeBracket.Under13;
            if (youngestAge < 16) return SketchAgeBracket.Age13To15;
            if (youngestAge < 18) return SketchAgeBracket.Age16To17;
            return SketchAgeBracket.Adult;
        }

        public static bool IsMinor(SketchAgeBracket bracket) => bracket != SketchAgeBracket.Adult;

        public static SketchAgeAdSignals Resolve(SketchAgeBracket bracket, SketchAdMobContentRating adultMaxRating, SketchAdMobContentRating minorMaxRating)
        {
            SketchAdMobContentRating minorCap = minorMaxRating < adultMaxRating ? minorMaxRating : adultMaxRating;
            switch (bracket)
            {
                case SketchAgeBracket.Adult:
                    return new SketchAgeAdSignals(SketchAdMobTag.False, SketchAdMobTag.False, adultMaxRating, false, true);
                case SketchAgeBracket.Age16To17:
                    return new SketchAgeAdSignals(SketchAdMobTag.False, SketchAdMobTag.False, minorCap, true, false);
                case SketchAgeBracket.Age13To15:
                    return new SketchAgeAdSignals(SketchAdMobTag.False, SketchAdMobTag.True, minorCap, true, false);
                case SketchAgeBracket.Under13:
                    return new SketchAgeAdSignals(SketchAdMobTag.True, SketchAdMobTag.True, SketchAdMobContentRating.General, true, false);
                default:
                    return new SketchAgeAdSignals(SketchAdMobTag.Unspecified, SketchAdMobTag.True, minorCap, true, false);
            }
        }

        public static string Format(SketchAgeBracket bracket) => ((int)bracket).ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static SketchAgeBracket Parse(string value)
        {
            if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int raw)) return SketchAgeBracket.Unknown;
            return raw >= (int)SketchAgeBracket.Under13 && raw <= (int)SketchAgeBracket.Adult ? (SketchAgeBracket)raw : SketchAgeBracket.Unknown;
        }
    }
}
