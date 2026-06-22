namespace Ecopath.EwE
{

    public partial class EwEConfiguration : IEwEConfiguration
    {
        public string ModelName { get; set; } = "";
        public string LocalModelFile { get; set; } = "";
        public int EcosimScenario { get; set; } = 0;
        public int EcosimTimeSeries { get; set; } = 0;
        public int EcospaceScenario { get; set; } = 0;
        public int SpinupYears { get; set; } = 0;
        public int StartYear { get; set; } = 0;
        public int MaxRunYears { get; set; } = 400;
        public string OutputPath { get; set; } = @".\";
        public bool WriteOutput { get; set; } = false;

        /// <summary>
        /// Get/set whether MultiLevelKeys sent out to SURIMI should include vocabularies (e.g., "stage=dwc.lifestage:juvenile")
        /// </summary>
        public bool IncludeVocabularies { get; set; } = false;

        /// <summary>
        /// The EwE indices of fished groups in the Ecopath model. Note that some of these groups
        /// may only be discarded, and some groups may not have known species attached.
        /// Not all fished groups may be accessible to the SURIMI framework.
        /// </summary>
        private readonly List<int> m_FishedGroups = new();
        private readonly List<int> m_ExternalFleets = new();

        List<int> IEwEConfiguration.FishedGroups { get => m_FishedGroups; }
        List<int> IEwEConfiguration.ExternalFleets { get => m_ExternalFleets; }
    }
}
