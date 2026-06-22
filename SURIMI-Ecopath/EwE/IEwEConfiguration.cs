namespace Ecopath.EwE
{
    public interface IEwEConfiguration
    {
        string ModelName { get; }
        string LocalModelFile { get; }
        int EcosimScenario { get; set; }
        int EcosimTimeSeries { get; set; }
        int MaxRunYears { get; set; }
        int EcospaceScenario { get; set; }
        int SpinupYears { get; set; }
        bool IncludeVocabularies { get; set; }
        int StartYear { get; set; }
        string OutputPath { get; set; }
        bool WriteOutput { get; set; }
        
        /// <summary>
        /// All fished groups in the Ecopath model.
        /// </summary>
        public List<int> FishedGroups { get; }
        /// <summary>
        /// Indicates whether fishing by a given gear fleet is managed outside the EwE software.
        /// </summary>
        public List<int> ExternalFleets { get; }
    }
}
