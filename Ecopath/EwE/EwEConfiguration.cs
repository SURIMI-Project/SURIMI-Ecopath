using Grpc.Net.Client.Balancer;

namespace Ecopath.EwE
{
    public class EwEConfiguration : IEwEConfiguration
    {
        /// <summary>
        /// Mapping of three-letter species code to Ecopath group index
        /// </summary>
        private Dictionary<string, int> m_speciesgroup = new Dictionary<string, int>();
        /// <summary>
        /// Amount [0, 1] that three-letter species code contributes to an Ecopath group
        /// </summary>
        private Dictionary<string, Single> m_speciescontribution = new Dictionary<string, Single>();

        /// <summary>
        /// Mapping of gear code to Ecopath fleet index
        /// </summary>
        private Dictionary<string, int> m_gearfleet = new Dictionary<string, int>();

        /// <summary>
        /// A list of gear codes managed externally (e.g., not fishing within EwE).
        /// </summary>
        private List<string> m_externalGears = new List<string>();

        public EwEConfiguration() 
        {
            ModelName = @"Includes/Anchovy Bay Spatial.eiixml";
            EcosimScenario = 1;
            EcosimTimeSeries = 0;
            EcospaceScenario = 1;
            SpinupYears = 10;
            StartYear = 5;

            m_speciesgroup.Add("PIL", 3);
            m_speciescontribution.Add("PIL", 1);
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
            return m_speciesgroup.TryGetValue(speccode.ToUpper(), out var group) ? group : 0;
        }
        public void set_SpeciesGroup(string speccode, int iGroup)
        {
            m_speciesgroup.TryAdd(speccode.ToUpper(), iGroup);
        }
        public Single get_SpeciesContribution(string speccode)
        {
            return m_speciescontribution.TryGetValue(speccode.ToUpper(), out var contribution) ? contribution : 0!;
        }
        public void set_SpeciesContribution(string speccode, Single contribution)
        {
            m_speciescontribution.TryAdd(speccode.ToUpper(), contribution);
        }

        public string[] SpeciesOfInterest()
        {
            return [.. m_speciesgroup.Keys]; // Gawd! Who invented this syntaxis, and was it really necessary?
        }

        public int get_GearFleet(string gearcode)
        {
            return m_gearfleet.TryGetValue(gearcode.ToUpper(), out var group) ? group : 0;
        }
        public void set_GearFleet(string gearcode, int iFleet)
        {
            m_gearfleet.TryAdd(gearcode.ToUpper(), iFleet);
        }

        public string[] GearsOfInterest()
        {
            return [.. m_gearfleet.Keys]; // Doesn't get any prettier second time around
        }

        public void set_ExternalGear(string gearcode, bool isExternal)
        { 
            if (string.IsNullOrEmpty(gearcode)) return;
            gearcode = gearcode.ToUpper();
            if (isExternal)
                m_externalGears.Remove(gearcode);
            else
                m_externalGears.Add(gearcode);
        }

        public bool get_ExternalGear(string gearcode)
        {
            if (string.IsNullOrEmpty(gearcode)) return false;
            return m_externalGears.Contains(gearcode.ToUpper());
        }

        public string[] GearsExternal()
        {
            return m_externalGears.ToArray(); // Ahhh, this is better
        }

    }
}
