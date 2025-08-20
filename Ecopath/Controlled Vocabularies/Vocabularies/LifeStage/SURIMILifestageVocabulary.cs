using ControlledVocabularies.Core;

namespace ControlledVocabularies.Vocabularies
{
    /// <summary>
    /// A friendly and legible controlled dictionary to express life stages.
    /// </summary>
    public class SURIMILifestageVocabulary : ControlledVocabularyBase
    {
        private const string COL_CODE = "id";
        private const string COL_VALUES = "values";

        public override string CodeFieldName => COL_CODE;
        public override string VocabularyName => "surimi.lifestage";
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Lifestage;

        protected override bool LoadFromSource()
        {
            AddField(COL_CODE, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_VALUES, Domain, Purpose, true, 1, MatchStrategy.Exact | MatchStrategy.Fuzzy);

            AddRow("juvenile", "young juvenile small");
            AddRow("adult", "adult large old");
            AddRow("larva", "larva spawn hatchling");
            AddRow("egg", "egg");

            return true;
        }

        private void AddRow(string code, string values)
        {
            var drow = this.Table.NewRow();
            drow[COL_CODE] = code;
            drow[COL_VALUES] = values;
            this.Table.Rows.Add(drow);
        }
    }
}