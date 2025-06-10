using Grpc.Net.Client.Balancer;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;

namespace Ecopath.EwE
{
    public partial class EwEConfiguration : IEwEConfiguration
    {
        /// <summary>
        /// Mapping of species code -> Ecopath iGroup
        /// </summary>
        /// <remarks>
        /// A group can represent multiple species; a species belongs to only one group.
        /// </remarks>
        private readonly Dictionary<string, int> m_speciesgroup = new();

        /// <summary>
        /// Mapping of species code -> Amount [0, 1] of <seealso cref="m_speciesgroup"/>.
        /// </summary>
        /// <remarks>
        /// A group can represent multiple species; a species belongs to only one group.
        /// </remarks>
        private readonly Dictionary<string, float> m_speciescontribution = new();

        /// <summary>
        /// Mapping of gear code + market -> Ecopath iFleet
        /// </summary>
        private readonly Dictionary<DualKey, int> m_gearfleet = new();

        /// <summary>
        /// A list of gear codes managed externally (e.g., not fishing within EwE).
        /// </summary>
        private readonly List<string> m_externalGears = new List<string>();

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

        /// <summary>
        /// Get the single Ecopath functional group that corresponds to the given Species Code.
        /// </summary>
        /// <param name="speccode"></param>
        /// <returns></returns>
        /// <remarks>
        /// This mapping should be connected to the EwE taxonomy tables.
        /// </remarks>
        public int get_SpeciesGroup(string speccode)
        {
            if (string.IsNullOrEmpty(speccode)) return 0;
            return m_speciesgroup.TryGetValue(speccode.ToUpper(), out var group) ? group : 0;
        }

        /// <summary>
        /// Set the single Ecopath functional group that corresponds to a Species Code.
        /// </summary>
        /// <param name="speccode"></param>
        /// <returns></returns>
        /// <remarks>
        /// This mapping should be connected to the EwE taxonomy tables.
        /// </remarks>
        public void set_SpeciesGroup(string speccode, int iGroup)
        {
            if (string.IsNullOrEmpty(speccode)) return;
            m_speciesgroup.TryAdd(speccode.ToUpper(), iGroup);
        }

        /// <summary>
        /// Get the single Species Code that corresponds to an Ecopath functional group.
        /// </summary>
        /// <param name="speccode"></param>
        /// <returns></returns>
        /// <remarks>
        /// This mapping should be connected to the EwE taxonomy tables.
        /// </remarks>
        /// <seealso cref="get_SpeciesGroup(string)"/>
        /// <seealso cref="set_SpeciesGroup(string, int)"/>
        public string get_GroupSpecies(int iGroup)
        {
            foreach (string speccode in this.m_speciesgroup.Keys)
                if (this.m_speciesgroup[speccode] == iGroup)
                    return speccode;
            return string.Empty;
        }

        /// <summary>
        /// Get the biomass proportion that a species code contributes to the functional group;
        /// there may be other species present.
        /// </summary>
        /// <param name="speccode"></param>
        /// <returns></returns>
        /// <remarks>
        /// This mapping should be connected to the EwE taxonomy tables - biomass proportion.
        /// </remarks>
        public Single get_SpeciesContribution(string speccode)
        {
            if (string.IsNullOrEmpty(speccode)) return 0;
            return m_speciescontribution.TryGetValue(speccode.ToUpper(), out var contribution) ? contribution : 0!;
        }

        /// <summary>
        /// Set the biomass proportion that a species code contributes to the functional group;
        /// there may be other species present.
        /// </summary>
        /// <param name="speccode"></param>
        /// <returns></returns>
        /// <remarks>
        /// This mapping should be connected to the EwE taxonomy tables - biomass proportion.
        /// </remarks>
        public void set_SpeciesContribution(string speccode, Single contribution)
        {
            if (string.IsNullOrEmpty(speccode)) return;
            m_speciescontribution.TryAdd(speccode.ToUpper(), contribution);
        }

        /// <summary>
        /// Get an array of all species codes mapped to EwE functional groups.
        /// </summary>
        /// <returns></returns>
        public string[] SpeciesCodes()
        {
            //return [.. m_speciesgroup.Keys]; // This is just too ugly. I ain't using this. Bwech.
            return this.m_speciesgroup.Keys.ToArray();
        }

        /// <summary>
        /// Get the Ecopath fleet code assigned to a specific gear code and market code.
        /// </summary>
        /// <returns></returns>
        public int get_GearFleet(string gearcode, string marketcode)
        {
            return this.m_gearfleet.TryGetValue(DualKey.Make(gearcode, marketcode), out var group) ? group : 0;
        }

        /// <summary>
        /// Set the Ecopath fleet index assigned to a specific gear code and market code.
        /// </summary>
        /// <returns></returns>
        public void set_GearFleet(string gearcode, string marketcode, int iFleet)
        {
            m_gearfleet.TryAdd(DualKey.Make(gearcode, marketcode), iFleet);
        }

        /// <summary>
        /// Get the gear code assigned to a specific Ecopath fleet index. There can be only one.
        /// </summary>
        /// <returns></returns>
        public string get_FleetGear(int iFleet)
        {
            foreach (DualKey k in this.m_gearfleet.Keys)
                if (this.m_gearfleet[k] == iFleet)
                    return k.c1;
            return string.Empty;
        }

        /// <summary>
        /// Get the market code assigned to a specific Ecopath fleet index. There can be only one.
        /// </summary>
        /// <returns></returns>
        public string get_FleetMarket(int iFleet)
        {
            foreach (DualKey k in this.m_gearfleet.Keys)
                if (this.m_gearfleet[k] == iFleet)
                    return k.c2;
            return string.Empty;
        }

        /// <summary>
        /// Get an array of gear codes assigned to Ecopath fleets.
        /// </summary>
        /// <returns></returns>
        public string[] GearCodes()
        {
            List<string> gears = new();
            foreach (DualKey keys in this.m_gearfleet.Keys)
                gears.Add(keys.c1);
            return gears.Distinct().ToArray();
        }

        /// <summary>
        /// Get an array of market codes assigned to EwE fleets.
        /// </summary>
        /// <returns></returns>
        public string[] MarketCodes()
        {
            List<string> markets = new();
            foreach (DualKey keys in this.m_gearfleet.Keys)
                markets.Add(keys.c2);
            return markets.Distinct().ToArray();
        }

        /// <summary>
        /// Get an array of market codes that a given gear code sells to.
        /// </summary>
        /// <returns></returns>
        public string[] MarketCodes(string gearcode)
        {
            List<string> markets = new();
            if (!string.IsNullOrEmpty(gearcode))
            {
                foreach (DualKey keys in this.m_gearfleet.Keys)
                    if (string.Compare(gearcode, keys.c1, true) ==0 )
                    markets.Add(keys.c2);
            }
            return markets.Distinct().ToArray();
        }

        /// <summary>
        /// Set whether fishing by a given gear code is managed outside the EwE software.
        /// </summary>
        /// <returns></returns>
        public void set_ExternalGear(string gearcode, bool isExternal)
        { 
            if (string.IsNullOrEmpty(gearcode)) return;
            gearcode = gearcode.ToUpper();
            if (isExternal)
                this.m_externalGears.Remove(gearcode);
            else
                this.m_externalGears.Add(gearcode);
        }

        /// <summary>
        /// Get whether fishing by a given gear code is managed outside the EwE software.
        /// </summary>
        /// <returns></returns>
        public bool get_ExternalGear(string gearcode)
        {
            if (string.IsNullOrEmpty(gearcode)) return false;
            return this.m_externalGears.Contains(gearcode.ToUpper());
        }

        public string[] ExternalGearCodes()
        {
            return this.m_externalGears.ToArray(); 
        }
    }
}
