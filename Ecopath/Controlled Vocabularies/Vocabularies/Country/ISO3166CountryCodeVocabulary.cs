using ControlledVocabularies.Core;
using CsvHelper;
using System.Globalization;

namespace ControlledVocabularies.Vocabularies
{
    public class ISO3166CountryCodeVocabulary 
        : ControlledVocabularyBase
    {
        /// <todo>Use a live online source, with a local version as backup. Need some future smarts here</todo>
        private const string FileName = @"Includes\ISO-3166-Countries-with-Regional-Codes.csv";
        private const string COL_CODE = "alpha-3";
        private const string COL_NAME = "name";

        public override string VocabularyName => "ISO-3166";
        public override string CodeFieldName => COL_CODE;
        public override KeyDomain Domain => KeyDomain.Country;
        public override KeyPurpose Purpose => KeyPurpose.Country;

        protected override bool LoadFromSource()
        {
            using var reader = new StreamReader(FileName);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            using var dr = new CsvDataReader(csv);

            AddField(COL_CODE, Domain, Purpose, FieldKind.Code, true, 1, MatchStrategy.Exact);
            AddField(COL_NAME, Domain, Purpose, FieldKind.Label, true, 1, MatchStrategy.Exact | MatchStrategy.Fuzzy);

            try
            {
                this.Table.Load(dr);
                return true;
            }
            catch (Exception ex)
            {
                // ToDo: log ex.Message or ex.ToString()
                return false;
            }
        }
    }
}