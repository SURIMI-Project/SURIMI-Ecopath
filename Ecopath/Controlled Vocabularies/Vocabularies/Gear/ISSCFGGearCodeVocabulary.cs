using ControlledVocabularies.Core;
using CsvHelper;
using System.Globalization;

namespace ControlledVocabularies.Vocabularies
{
    /// <summary>
    /// FAO ISSCFG gear codes from the IOTC taxonomy export (or similar).
    /// </summary>
    public class ISSCFGGearCodeVocabulary : ControlledVocabularyBase
    {
        // Adjust to your actual file path/name
        private const string FileName = @"Includes\ISSCFG_2024.csv";

        private const string COL_GEAR_CODE = "GEAR_CODE";   // e.g., "DL", "LL", "PS", "TB", …
        private const string COL_GEAR_NAME = "GEAR_NAME";   // e.g., "Drifting longlines"
        private const string COL_ISSCFG_CODE = "ISSCFG_CODE"; // e.g., "09.32"
        private const string COL_ISSCFG_NAME = "ISSCFG";      // e.g., "Drifting longlines"
        private const string COL_GROUP_CODE = "ISSCFG_GROUP_CODE"; // e.g., "09"
        private const string COL_GROUP_NAME = "ISSCFG_GROUP";      // e.g., "Hooks and lines"
        private const string COL_CONF_CODE = "GEAR_CONFIGURATION_CODE"; // e.g., "FR"
        private const string COL_CONF_NAME = "GEAR_CONFIGURATION_NAME"; // e.g., "Up to 1800 hooks per line"

        public override string VocabularyName => "ISSCFG";
        public override KeyDomain Domain => KeyDomain.FleetSegment;
        public override KeyPurpose Purpose => KeyPurpose.Fleet | KeyPurpose.Gear;

        // Canonical ID for EwE mappings (“gearcode=XX”) — keep this tight
        public override string CodeFieldName => COL_GEAR_CODE;

        protected override bool LoadFromSource()
        {
            // Declare fields with pragmatic strategies
            AddField(COL_GEAR_CODE, Domain, Purpose, isRequired: true, weight: 10, strategy: MatchStrategy.Exact);
            AddField(COL_GEAR_NAME, Domain, Purpose, isRequired: true, weight: 7, strategy: MatchStrategy.Exact | MatchStrategy.Fuzzy | MatchStrategy.TokenOverlap);

            // Nice-to-have structure for later joins/search
            AddField(COL_ISSCFG_CODE, Domain, Purpose, isRequired: false, weight: 6, strategy: MatchStrategy.Exact);
            AddField(COL_ISSCFG_NAME, Domain, Purpose, isRequired: false, weight: 5, strategy: MatchStrategy.Exact | MatchStrategy.Fuzzy);
            AddField(COL_GROUP_CODE, Domain, Purpose, isRequired: false, weight: 3, strategy: MatchStrategy.Exact);
            AddField(COL_GROUP_NAME, Domain, Purpose, isRequired: false, weight: 3, strategy: MatchStrategy.Exact | MatchStrategy.Fuzzy);
            AddField(COL_CONF_CODE, Domain, Purpose, isRequired: false, weight: 2, strategy: MatchStrategy.Exact);
            AddField(COL_CONF_NAME, Domain, Purpose, isRequired: false, weight: 2, strategy: MatchStrategy.Exact | MatchStrategy.Fuzzy);

            try
            {
                using var reader = new StreamReader(FileName);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                using var dr = new CsvDataReader(csv);

                this.Table.Load(dr);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
