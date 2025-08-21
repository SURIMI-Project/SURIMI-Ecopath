using ControlledVocabularies.Core;
using CsvHelper;
using System.Globalization;

namespace ControlledVocabularies.Vocabularies
{
    /// <summary>
    /// FAO ASFIS species vocabulary
    /// </summary>
    public class ASFISSpeciesCodeVocabulary
    : ControlledVocabularyBase
    {
        private const string COL_CODE = "Alpha3_Code";
        private const string COL_NAME = "Scientific_Name";

        public override string VocabularyName => "ASFIS";
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Species;

        public override string CodeFieldName => COL_CODE;

        protected override bool LoadFromSource()
        {

            string fin = @"Includes\ASFIS_sp_2024.csv";
            using var reader = new StreamReader(fin);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            using var dr = new CsvDataReader(csv);

            AddField(COL_CODE, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_NAME, Domain, Purpose, true, 1, MatchStrategy.Exact | MatchStrategy.Fuzzy | MatchStrategy.TokenOverlap);

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