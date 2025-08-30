using ControlledVocabularies.Core;
using ControlledVocabularies.Registries;
using CsvHelper;
using System.Globalization;

namespace ControlledVocabularies.Vocabularies
{
    /// <summary>
    /// World Register of Marine Species (WoRMS) taxonomy vocabulary.
    /// Eventually will pull from WoRMS REST API with CSV fallback.
    /// </summary>
    public class WoRMSSpeciesVocabulary : ControlledVocabularyBase
    {
        /// <todo>Implement online source: http://www.marinespecies.org/rest/AphiaRecordsByName/[name]</todo>
        /// <todo>Add version tracking by comparing AphiaID records between refreshes</todo>
        private const string FileName = @"Includes\WoRMS_Data.csv";

        // Expected CSV columns from WoRMS export
        private const string COL_APHIA_ID = "AphiaID";           // Unique WoRMS identifier
        private const string COL_SCIENTIFIC_NAME = "ScientificName"; // Latin binomial
        private const string COL_COMMON_NAME = "commonname";    // Vernacular name
        private const string COL_KINGDOM = "Kingdom";           // Animalia, etc.
        private const string COL_PHYLUM = "Phylum";             // Chordata, etc.  
        private const string COL_CLASS = "Class";               // Actinopteri, etc.
        private const string COL_ORDER = "Order";               // Perciformes, etc.
        private const string COL_FAMILY = "Family";             // Gadidae, etc.
        private const string COL_GENUS = "Genus";               // Gadus, etc.
        private const string COL_STATUS = "Status";             // accepted, synonym, etc.
        private const string COL_FAO_CODE = "FAO_Code";         // Cross-reference to ASFIS

        public override string VocabularyName => "WoRMS";
        public override string CodeFieldName => COL_APHIA_ID;
        public override KeyDomain Domain => KeyDomain.Species;
        public override KeyPurpose Purpose => KeyPurpose.Species;

        /// <summary>
        /// Future: Online source URL for live updates
        /// </summary>
        public string OnlineSource => "http://www.marinespecies.org/rest/";

        /// <summary>
        /// Future: Last successful refresh timestamp
        /// </summary>
        public DateTime? LastRefresh { get; private set; }

        protected override bool LoadFromSource()
        {
            // Future: Try online source first, fall back to CSV
            return LoadFromCsvFile();
        }

        private bool LoadFromCsvFile()
        {
            if (!File.Exists(FileName))
            {
                // Create minimal sample file for development
                CreateSampleFile();
            }

            // Define field descriptors with cross-vocabulary mapping strategies
            AddField(COL_APHIA_ID, Domain, Purpose, FieldKind.Code, isRequired: true, weight: 10, strategy: MatchStrategy.Exact);
            AddField(COL_SCIENTIFIC_NAME, Domain, Purpose, FieldKind.Label, isRequired: true, weight: 9, strategy: MatchStrategy.Exact | MatchStrategy.Fuzzy);
            AddField(COL_COMMON_NAME, Domain, Purpose, FieldKind.Label, isRequired: false, weight: 7, strategy: MatchStrategy.Exact | MatchStrategy.Fuzzy);

            // Taxonomic hierarchy - useful for resolution
            AddField(COL_KINGDOM, Domain, Purpose, FieldKind.Label, isRequired: false, weight: 2, strategy: MatchStrategy.Exact);
            AddField(COL_PHYLUM, Domain, Purpose, FieldKind.Label, isRequired: false, weight: 3, strategy: MatchStrategy.Exact);
            AddField(COL_CLASS, Domain, Purpose, FieldKind.Label, isRequired: false, weight: 4, strategy: MatchStrategy.Exact);
            AddField(COL_ORDER, Domain, Purpose, FieldKind.Label, isRequired: false, weight: 5, strategy: MatchStrategy.Exact);
            AddField(COL_FAMILY, Domain, Purpose, FieldKind.Label, isRequired: false, weight: 6, strategy: MatchStrategy.Exact);
            AddField(COL_GENUS, Domain, Purpose, FieldKind.Label, isRequired: false, weight: 7, strategy: MatchStrategy.Exact);

            AddField(COL_STATUS, Domain, Purpose, FieldKind.Label, isRequired: false, weight: 2, strategy: MatchStrategy.Exact);

            // Cross-vocabulary bridge - critical for ASFIS mapping!
            AddField(COL_FAO_CODE, Domain, Purpose, FieldKind.Code, isRequired: false, weight: 8, strategy: MatchStrategy.Exact);

            try
            {
                using var reader = new StreamReader(FileName);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                using var dr = new CsvDataReader(csv);

                this.Table.Load(dr);
                LastRefresh = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex)
            {
                // Todo: log ex.Message
                return false;
            }
            return true;
        }

        /// <summary>
        /// Create sample WoRMS data for development/testing
        /// </summary>
        private void CreateSampleFile()
        {
            var sampleData = new[]
            {
                new { AphiaID = "126436", ScientificName = "Gadus morhua", CommonName = "Atlantic cod",
                      Kingdom = "Animalia", Phylum = "Chordata", Class = "Actinopteri",
                      Order = "Gadiformes", Family = "Gadidae", Genus = "Gadus",
                      Status = "accepted", FAO_Code = "COD" },

                new { AphiaID = "126437", ScientificName = "Melanogrammus aeglefinus", CommonName = "Haddock",
                      Kingdom = "Animalia", Phylum = "Chordata", Class = "Actinopteri",
                      Order = "Gadiformes", Family = "Gadidae", Genus = "Melanogrammus",
                      Status = "accepted", FAO_Code = "HAD" },

                new { AphiaID = "127186", ScientificName = "Merlangius merlangus", CommonName = "Whiting",
                      Kingdom = "Animalia", Phylum = "Chordata", Class = "Actinopteri",
                      Order = "Gadiformes", Family = "Gadidae", Genus = "Merlangius",
                      Status = "accepted", FAO_Code = "WHG" },

                new { AphiaID = "127023", ScientificName = "Sardina pilchardus", CommonName = "European sardine",
                      Kingdom = "Animalia", Phylum = "Chordata", Class = "Actinopteri",
                      Order = "Clupeiformes", Family = "Clupeidae", Genus = "Sardina",
                      Status = "accepted", FAO_Code = "PIL" },

                new { AphiaID = "126747", ScientificName = "Engraulis encrasicolus", CommonName = "European anchovy",
                      Kingdom = "Animalia", Phylum = "Chordata", Class = "Actinopteri",
                      Order = "Clupeiformes", Family = "Engraulidae", Genus = "Engraulis",
                      Status = "accepted", FAO_Code = "ANE" }
            };

            using var writer = new StreamWriter(FileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(sampleData);
        }

        /// <summary>
        /// Future: Refresh vocabulary from online source
        /// </summary>
        public async Task<bool> RefreshFromOnlineAsync()
        {
            // Todo: Implement WoRMS REST API integration
            // 1. Fetch updated records from WoRMS API
            // 2. Compare with existing records (version detection)
            // 3. Update local cache
            // 4. Log changes for audit trail

            await Task.CompletedTask; // Placeholder
            return false;
        }

        /// <summary>
        /// Future: Get specific species by AphiaID from online source
        /// </summary>
        public async Task<MultiLevelKey?> GetSpeciesByAphiaIdAsync(string aphiaId)
        {
            // Todo: Direct WoRMS API call for single species
            // http://www.marinespecies.org/rest/AphiaRecordByAphiaID/{aphiaId}

            await Task.CompletedTask;
            return null;
        }

        /// <summary>
        /// Configure cross-vocabulary foreign keys for automatic resolution
        /// </summary>
        public void ConfigureCrossReferences(IVocabularyRegistry registry)
        {
            // Set up foreign key to ASFIS vocabulary via FAO codes
            var asfisVocab = registry.Get("ASFIS");
            if (asfisVocab != null)
            {
                this.SetForeignKey(COL_FAO_CODE, asfisVocab, asfisVocab.CodeFieldName);
            }
        }
    }
}