
public class ISSCFGGearCodeVocabulary 
    : ControlledVocabularyBase, IGearCodeVocabulary
{
    public override IEnumerable<string> FieldNames => [];
    public override string VocabularyName => "ISSCFG";
    public override KeyDomain KeyDomain => KeyDomain.FleetSegment;
    public override KeyPurpose KeyPurpose => KeyPurpose.Fleet | KeyPurpose.Gear;


    protected override bool LoadFromSource()
    {
        return true;
    }

    string IGearCodeVocabulary.CodeToGears(string gearcode)
    {
        return string.Empty;
    }

    string IGearCodeVocabulary.GearToCode(string gearname)
    {
        return string.Empty;
    }

    (string match, int score) IGearCodeVocabulary.MatchGearName(string gearname, int iMinScore)
    {
        return (string.Empty, 42);
    }
}
