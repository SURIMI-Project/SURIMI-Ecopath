public enum KeyDomain
{
    Species,
    FleetSegment,
    Market,
    Country
}

public enum KeyPurpose
{
    NotSet,
    SpeciesName,
    LifeStage,
    AgeClass,
    LengthClass,
    GearType,
    Country
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
    DontBother = None,
    Exact = 1 << 0,
    Synonym = 1 << 1,
    Fuzzy = 1 << 2,
    Keyword = 1 << 3,
    TokenOverlap = 1 << 4,
    Regex = 1 << 5,
    NumericRange = 1 << 6
}
