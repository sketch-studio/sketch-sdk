using Sketch.Sdk;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception("FAIL: " + message);
}

const SketchAdMobContentRating T = SketchAdMobContentRating.Teen;
const SketchAdMobContentRating PG = SketchAdMobContentRating.ParentalGuidance;

// Birth year -> bracket: the younger possible age counts (birthday may not have passed yet), never assume adult.
Check(SketchAgePolicy.FromBirthYear(2008, 2026) == SketchAgeBracket.Age16To17, "born 2008 in 2026 may still be 17");
Check(SketchAgePolicy.FromBirthYear(2007, 2026) == SketchAgeBracket.Adult, "born 2007 in 2026 is at least 18");
Check(SketchAgePolicy.FromBirthYear(2010, 2026) == SketchAgeBracket.Age13To15, "born 2010 may be 15");
Check(SketchAgePolicy.FromBirthYear(2012, 2026) == SketchAgeBracket.Age13To15, "born 2012 is at least 13");
Check(SketchAgePolicy.FromBirthYear(2013, 2026) == SketchAgeBracket.Under13, "born 2013 may be 12");
Check(SketchAgePolicy.FromBirthYear(2027, 2026) == SketchAgeBracket.Unknown, "future year is unknown");
Check(SketchAgePolicy.FromBirthYear(1900, 2026) == SketchAgeBracket.Unknown, "implausible year is unknown");
Check(SketchAgePolicy.FromBirthYear(2026 - 120, 2026) == SketchAgeBracket.Adult, "oldest year the age screen accepts maps to a bracket");
Check(SketchAgePolicy.FromBirthYear(2026, 2026) == SketchAgeBracket.Under13, "born this year is a child");

// Bracket -> ad request signals.
var child = SketchAgePolicy.Resolve(SketchAgeBracket.Under13, T, T);
Check(child.ChildDirected == SketchAdMobTag.True && child.UnderAgeOfConsent == SketchAdMobTag.True, "under 13: TFCD + TFUA");
Check(child.MaxContentRating == SketchAdMobContentRating.General && child.NonPersonalized && !child.AdPersonalizationAllowed, "under 13: G, npa, no ad personalization");

var young = SketchAgePolicy.Resolve(SketchAgeBracket.Age13To15, T, T);
Check(young.ChildDirected == SketchAdMobTag.False && young.UnderAgeOfConsent == SketchAdMobTag.True, "13-15: not child-directed, under GDPR age of consent");
Check(young.MaxContentRating == T && young.NonPersonalized && !young.AdPersonalizationAllowed, "13-15: teen cap, npa");

var teen = SketchAgePolicy.Resolve(SketchAgeBracket.Age16To17, T, T);
Check(teen.ChildDirected == SketchAdMobTag.False && teen.UnderAgeOfConsent == SketchAdMobTag.False, "16-17: may consent under GDPR");
Check(teen.MaxContentRating == T && teen.NonPersonalized && !teen.AdPersonalizationAllowed, "16-17: minor, still npa and teen cap");

var unknown = SketchAgePolicy.Resolve(SketchAgeBracket.Unknown, T, T);
Check(unknown.UnderAgeOfConsent == SketchAdMobTag.True && unknown.NonPersonalized && unknown.MaxContentRating == T, "unknown age is treated as a minor, never as adult");

var adult = SketchAgePolicy.Resolve(SketchAgeBracket.Adult, SketchAdMobContentRating.MatureAudience, T);
Check(adult.ChildDirected == SketchAdMobTag.False && adult.UnderAgeOfConsent == SketchAdMobTag.False, "adult: no age tags");
Check(!adult.NonPersonalized && adult.AdPersonalizationAllowed, "adult: personalization left to UMP consent");
Check(adult.MaxContentRating == SketchAdMobContentRating.MatureAudience, "adult: adult cap");

// A minor cap above the adult cap is clamped (minors never see more than adults).
Check(SketchAgePolicy.Resolve(SketchAgeBracket.Age16To17, PG, T).MaxContentRating == PG, "minor cap clamps to adult cap");

// Persisted value round trip and robustness.
Check(SketchAgePolicy.Parse(SketchAgePolicy.Format(SketchAgeBracket.Age13To15)) == SketchAgeBracket.Age13To15, "round trip");
Check(SketchAgePolicy.Parse("garbage") == SketchAgeBracket.Unknown && SketchAgePolicy.Parse(null) == SketchAgeBracket.Unknown, "bad store value -> unknown");

// Mediation networks: calls that AdMob adapters do not forward on their own.
bool Has(IReadOnlyList<SketchMediationPrivacyCall> calls, string network, string method, params object[] args) =>
    calls.Any(c => c.Network == network && c.Method == method && c.Args.SequenceEqual(args));

var off = SketchMediationPrivacy.Plan(ageSignalActive: false, restrictSale: false, nonBehavioral: false);
Check(off.Count == 0, "no age signal and no sale restriction: no network calls (old behaviour)");

var minor = SketchMediationPrivacy.Plan(ageSignalActive: true, restrictSale: true, nonBehavioral: true);
Check(Has(minor, "Liftoff", "SetCCPAStatus", false), "minor: Liftoff do-not-sell");
Check(Has(minor, "BIGO", "SetCcpaConsent", false), "minor: BIGO do-not-sell");
Check(Has(minor, "UnityAds", "SetConsentMetaData", "privacy.consent", false), "minor: Unity Ads do-not-sell");
Check(Has(minor, "UnityAds", "SetConsentMetaData", "user.nonbehavioral", true), "minor: Unity Ads mixed-audience contextual only");
Check(!minor.Any(c => c.Network == "InMobi"), "InMobi gets TFCD/TFUA from the adapter; no extra call");

var grown = SketchMediationPrivacy.Plan(ageSignalActive: true, restrictSale: false, nonBehavioral: false);
Check(Has(grown, "UnityAds", "SetConsentMetaData", "user.nonbehavioral", false), "adult: Unity Ads behavioural allowed (else mixed audience defaults to contextual)");
Check(!grown.Any(c => c.Method.Contains("CCPA", StringComparison.OrdinalIgnoreCase) || c.Method == "SetCcpaConsent"), "adult: no forced do-not-sell");
Check(minor.All(c => c.TypeName.Contains(", GoogleMobileAds.Mediation.")), "type names carry the adapter assembly");

// US-state opt-out from GPP (UMP) for adults and for games without age signals.
var adultOptOut = SketchMediationPrivacy.Plan(ageSignalActive: true, restrictSale: false, nonBehavioral: false, gppUsOptOut: true);
Check(Has(adultOptOut, "Liftoff", "SetCCPAStatus", false) && Has(adultOptOut, "BIGO", "SetCcpaConsent", false) && Has(adultOptOut, "UnityAds", "SetConsentMetaData", "privacy.consent", false), "adult GPP opt-out: do-not-sell on every network");
Check(Has(adultOptOut, "UnityAds", "SetConsentMetaData", "user.nonbehavioral", true), "adult GPP opt-out: no targeted ads on Unity Ads");
var adultStays = SketchMediationPrivacy.Plan(ageSignalActive: true, restrictSale: false, nonBehavioral: false, gppUsOptOut: false);
Check(!Has(adultStays, "Liftoff", "SetCCPAStatus", false) && Has(adultStays, "UnityAds", "SetConsentMetaData", "user.nonbehavioral", false), "adult did not opt out: personalised ads kept");
var noAgeOptOut = SketchMediationPrivacy.Plan(ageSignalActive: false, restrictSale: false, nonBehavioral: false, gppUsOptOut: true);
Check(Has(noAgeOptOut, "BIGO", "SetCcpaConsent", false) && !noAgeOptOut.Any(c => c.Args.Length == 2 && (string)c.Args[0] == "user.nonbehavioral"), "game without age signals still forwards the US opt-out");
Check(SketchMediationPrivacy.Plan(false, false, false, null).Count == 0, "no US section: nothing");
Check(Has(SketchMediationPrivacy.Plan(true, true, true, false), "Liftoff", "SetCCPAStatus", false), "minor stays do-not-sell even when GPP says no opt-out");

assertions += GppTests.Run();
assertions += AdapterVersionTests.Run();
Console.WriteLine($"Age policy tests: {assertions} assertions passed");
