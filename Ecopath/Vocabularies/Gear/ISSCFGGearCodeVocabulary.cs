
public class ISSCFGGearCodeVocabulary : IGearCodeVocabulary
{
    KeyDomain IControlledVocabulary.KeyDomain => KeyDomain.FleetSegment;
    public KeyPurpose KeyPurpose => KeyPurpose.GearType;

    string IControlledVocabulary.VocabularyName => "ISSCFG";

    public IEnumerable<MultiLevelKey> Records => new List<MultiLevelKey>{ };

    public bool Load()
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
