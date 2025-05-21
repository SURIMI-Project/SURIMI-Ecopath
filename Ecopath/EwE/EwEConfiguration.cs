using Grpc.Net.Client.Balancer;

namespace Ecopath.EwE
{
    public class EwEConfiguration : IEwEConfiguration
    {
        private Dictionary<string, int> _speciesgroup = new Dictionary<string, int>();
        private Dictionary<string, Single> _speciescontribution = new Dictionary<string, Single>();

        public EwEConfiguration() 
        {
            ModelName = @"Includes/Anchovy Bay Spatial.eiixml";
            EcosimScenario = 1;
            EcosimTimeSeries = 0;
            EcospaceScenario = 1;
            SpinupYears = 10;
            StartYear = 5;

            _speciesgroup.Add("PIL", 3);
            _speciescontribution.Add("PIL", 1);
        }

        public string ModelName { get; set; } = "";
        public int EcosimScenario { get; set; } = 0;
        public int EcosimTimeSeries { get; set; } = 0;
        public int EcospaceScenario { get; set; } = 0;
        public int SpinupYears { get; set; } = 0;
        public int StartYear { get; set; } = 0;
        public int MaxRunYears { get; set; } = 400;

        public int get_SpeciesGroup(string speccode)
        {
            return _speciesgroup.TryGetValue(speccode.ToUpper(), out var group) ? group : 0;
        }
        public void set_SpeciesGroup(string speccode, int iGroup)
        {
            _speciesgroup.TryAdd(speccode.ToUpper(), iGroup);
        }
        public Single get_SpeciesContribution(string speccode)
        {
            return _speciescontribution.TryGetValue(speccode.ToUpper(), out var contribution) ? contribution : 0!;
        }
        public void set_SpeciesContribution(string speccode, Single contribution)
        {
            _speciescontribution.TryAdd(speccode.ToUpper(), contribution);
        }

        public List<string> SpeciesOfInterest()
        {
            return [.. _speciesgroup.Keys]; // Gawd this is ugly
        }


    }
}
