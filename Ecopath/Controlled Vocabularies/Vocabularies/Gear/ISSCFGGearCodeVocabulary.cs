using ControlledVocabularies.Core;

namespace ControlledVocabularies.Vocabularies
{
    public class ISSCFGGearCodeVocabulary
    : ControlledVocabularyBase
    {
        public override string VocabularyName => "ISSCFG";
        public override KeyDomain Domain => KeyDomain.FleetSegment;
        public override KeyPurpose Purpose => KeyPurpose.Fleet | KeyPurpose.Gear;
        public override string CodeFieldName => "";

        protected override bool LoadFromSource()
        {
            return true;
        }
    }
}
