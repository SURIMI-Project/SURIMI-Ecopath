
public class ISSCFGGearCodeVocabulary 
    : ControlledVocabularyBase
{
    public override string VocabularyName => "ISSCFG";
    public override IEnumerable<string> FieldNames => [];
    public override KeyDomain KeyDomain => KeyDomain.FleetSegment;
    public override KeyPurpose KeyPurpose => KeyPurpose.Fleet | KeyPurpose.Gear;
    public override string CodeFieldName => "";

    protected override bool LoadFromSource()
    {
        return true;
    }
}
