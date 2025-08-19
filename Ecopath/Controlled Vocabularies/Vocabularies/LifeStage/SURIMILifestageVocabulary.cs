using ControlledVocabularies.Core;

namespace ControlledVocabularies.Vocabularies
{
    /// <summary>
    /// A friendly and legible controlled dictionary to express life stages.
    /// </summary>
    public class SURIMILifestageVocabulary
    : ControlledVocabularyBase
    {
        private const string COL_CODE = "id";
        private const string COL_VALUES = "values";

        public override IEnumerable<string> FieldNames => [COL_CODE, COL_VALUES];
        public override string CodeFieldName => COL_CODE;
        public override string VocabularyName => "surimi.lifestage";
        public override KeyDomain KeyDomain => KeyDomain.Species;
        public override KeyPurpose KeyPurpose => KeyPurpose.Lifestage;

        protected override bool LoadFromSource()
        {
            m_data.Clear();

            AddValue(COL_CODE + "=juvenile;" + COL_VALUES + "=young juvenile small");
            AddValue(COL_CODE + "=adult;" + COL_VALUES + "=adult large old");
            AddValue(COL_CODE + "=larva;" + COL_VALUES + "=larva spawn hatchling");
            AddValue(COL_CODE + "=egg;" + COL_VALUES + "=egg");

            return true;
        }

        private void AddValue(string value)
        {
            var key = MultiLevelKey.FromString(value);
            m_data[key.GetField(COL_CODE)!.Value] = key;
        }
    }
}