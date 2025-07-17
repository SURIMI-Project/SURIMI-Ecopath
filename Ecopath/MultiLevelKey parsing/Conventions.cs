public enum KeyDomain
{
    Species,
    FleetSegment,
    Market
}

public class SpeciesFields
{
    public static readonly string SpeciesCode = "speciescode";
    public static readonly string Lifestage = "stage";
    public static string Stage => Lifestage;
    public static readonly string Age = "age";
    public static readonly string Length = "length";
}

public class FleetSegmentFields
{
    public static readonly string FleetSegmentCode = "gearcode";
    public static readonly string Flag = "flag";
}
