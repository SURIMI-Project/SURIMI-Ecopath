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
        /// A list if indexes of groups that are fished (IsFished == false) in cCore.
        /// </summary>
        public List<int> FishedGroups { get; }

        /// <summary>
        /// A list of indexes of fleets (in cCore. 1 based) that are not handled by Ecopath.
        /// </summary>
        public List<int> ExternalFleets { get; }
    }
}
