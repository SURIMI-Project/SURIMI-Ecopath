using Grpc.Net.Client.Balancer;

namespace Ecopath.EwE
{
    public class EwEConfiguration
    {
        private Dictionary<string, int> _speciesgroup = new Dictionary<string, int>();
        private Dictionary<string, float> _speciescontribution = new Dictionary<string, float>();

        public string ModelName { get; set; } = "";
        public int EcosimScenario { get; set; } = 0;
        public int EcosimTimeSeries { get; set; } = 0;
        public int EcospaceScenario { get; set; } = 0;
        public int SpinupYears { get; set; } = 0;
        public int StartYear { get; set; } = 0;
        public int MaxRunYears { get; set; } = 400;

        public int get_SpeciesGroup(string speccode)
        {
            return _speciesgroup.TryGetValue(speccode.ToLower(), out var group) ? group : 0;
        }
        public void set_SpeciesGroup(string speccode, int iGroup)
        {
            _speciesgroup.TryAdd(speccode.ToLower(), iGroup);
        }
        public float get_SpeciesContribution(string speccode)
        {
            return _speciescontribution.TryGetValue(speccode.ToLower(), out var contribution) ? contribution : 0!;
        }
        public void set_SpeciesContribution(string speccode, float contribution)
        {
            _speciescontribution.TryAdd(speccode.ToLower(), contribution);
        }

    }
}
