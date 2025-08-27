using ControlledVocabularies.Core;
using CsvHelper;
using System.Globalization;

namespace ControlledVocabularies.Vocabularies
{
    public class AquaMapsSpeciesCodeVocabulary
            : ControlledVocabularyBase
    {
        private const string COL_CODE = "SPECIESID";
        private const string COL_FK = "SpecCode";     // FishBase key
        private const string COL_FB_NAME = "FB_NAME"; // FishBase name => common name
        private const string COL_KINGDOM = "Kingdom";
        private const string COL_PHYLUM = "Phylum";
        private const string COL_CLASS = "Class";
        private const string COL_ORDER = "Order";
        private const string COL_FAMILY = "Family";
        private const string COL_GENUS = "Genus";
        private const string COL_SPECIES = "Species";
        private const string COL_NAME = "ScientificName";

        public override string VocabularyName => "AquaMaps.species";
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Species;

        public override string CodeFieldName => COL_CODE;

        protected override bool LoadFromSource()
        {

            string fin = @"Includes\speciesoccursum_ver0816c.csv";
            using var reader = new StreamReader(fin);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            using var dr = new CsvDataReader(csv);

            AddField(COL_CODE, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_FK, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_NAME, Domain, Purpose, true, 1, MatchStrategy.Exact | MatchStrategy.Fuzzy | MatchStrategy.TokenOverlap);
            AddField(COL_FB_NAME, Domain, Purpose, true, 1, MatchStrategy.Exact | MatchStrategy.Fuzzy | MatchStrategy.TokenOverlap);
            AddField(COL_KINGDOM, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_PHYLUM, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_CLASS, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_ORDER, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_FAMILY, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_GENUS, Domain, Purpose, true, 1, MatchStrategy.Exact);
            AddField(COL_SPECIES, Domain, Purpose, true, 1, MatchStrategy.Exact);

            this.Table.Columns[COL_NAME]!.Expression = $"TRIM(ISNULL({COL_GENUS}, '') + ' ' + ISNULL({COL_SPECIES}, ''))"; // Let's see if this works...

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
