namespace ControlledVocabularies.Core
{
    public enum KeyDomain
    {
        NotSet = 0,
        Species,
        FleetSegment,
        Market,
        Country
    }

    [Flags]
    public enum KeyPurpose : ulong
    {
        NotSet = 0,
        Species = 1UL << 0,
        Lifestage = 1UL << 1,
        Age = 1UL << 2,
        Length = 1UL << 3,
        Gear = 1UL << 4,
        Fleet = 1UL << 5,
        Country = 1UL << 6,
        Market = 1UL << 7,
        // reserve some spare bits for internal growth
    }

    public class SpeciesFields
    {
        public static readonly string SpeciesCode = "speciescode";
        public static readonly string Lifestage = "stage";
        public static string Stage => Lifestage;
        public static readonly string Age = "age";
        public static readonly string Length = "length";
    }

    public class FishingFields
    {
        public static readonly string GearCode = "gearcode";
        public static readonly string Flag = "flag";
    }

    public class MarketFields
    {
        public static readonly string MarketCode = "marketcode";
    }

    [Flags]
    public enum MatchStrategy
    {
        None = 0,
        Exact = 1 << 0,
        Synonym = 1 << 1,
        Fuzzy = 1 << 2,
        Keyword = 1 << 3,
        TokenOverlap = 1 << 4,
        Regex = 1 << 5,
        NumericRange = 1 << 6
    }
}
