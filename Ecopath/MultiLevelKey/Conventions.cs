public enum KeyDomain
{
    Species,
    FleetSegment,
    Market,
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
