
public class ISSCFGGearCodeVocabulary 
    : ControlledVocabularyBase, IGearCodeVocabulary
{

    KeyDomain IControlledVocabulary.KeyDomain => KeyDomain.FleetSegment;
    public new KeyPurpose KeyPurpose => KeyPurpose.GearType;

    public new string VocabularyName => "ISSCFG";

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
