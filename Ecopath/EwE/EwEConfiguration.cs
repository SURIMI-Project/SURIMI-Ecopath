using EwECore;
using EwECore.Auxiliary;
using EwEUtils.Core;
using Grpc.Net.Client.Balancer;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace Ecopath.EwE
{
    // This code is going to have to change
    // Use multi-level keys to identify groups
    // Do not use ontologies; hard code keys to the SURIMI standard
    // However, use reflection to map between EwE items and SURIMI entities
    //
    //

    public partial class EwEConfiguration : IEwEConfiguration
    {
        #region Data


        //private readonly MatcherRegistry m_registry = new(); // Overkill for now

        /// <summary>
        /// MultiLevelKey -> EwE item mapping
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>"Species.SpeciesCode=<value>{;Species.Stage=<value>}" -> iGroup + proportion"</item>
        /// <item>"FleetSegment.GearCode=<value>;FleetSegment.Flag=<value>" -> iFleet</item>
        /// <item>"GearCode=<value>;MarketCode=<value>" -> iFleet</item>
        /// </list>
        /// </remarks>
        private readonly List<MultiLevelKey> m_mappings = new();

        // The EwE indices of externally managed fleets.
        private readonly HashSet<int> m_externalFleets = new();
        // The EwE indices of fished groups
        private readonly HashSet<int> m_fishedGroups = new();

        #endregion // Data

        public EwEConfiguration()
        {
            ModelName = @"Includes/GSA0607EwENBS.eiixml";
            EcosimScenario = 1;
            EcosimTimeSeries = 0;
            EcospaceScenario = 1;
            SpinupYears = 10;
            StartYear = 13;
        }


        public string ModelName { get; set; } = "";
        public int EcosimScenario { get; set; } = 0;
        public int EcosimTimeSeries { get; set; } = 0;
        public int EcospaceScenario { get; set; } = 0;
        public int SpinupYears { get; set; } = 0;
        public int StartYear { get; set; } = 0;
        public int MaxRunYears { get; set; } = 400;


        #region Consulting the registry

        public IEnumerable<(int index, int score, float propertion)> ResolveGroups(string speciescode)
        {
            MultiLevelKey key = new();
            key.Fields.Add("SpeciesCode", speciescode);

            return ResolveGroups(key);
        }

        public IEnumerable<(int index, int score, float propertion)> ResolveGroups(Ecopath.Models.Species species)
        {
            return ResolveGroups(MultiLevelKey.FromObject(species));
        }


        public IEnumerable<(int index, int score, float propertion)> ResolveGroups(MultiLevelKey key)
        {
            StaticKeyResolver resolver = new StaticKeyResolver(this.m_mappings);
            return resolver.FindAllMatches(key, KeyDomain.Species);
        }

        public (int index, int score, float propertion) ResolveFleet(Ecopath.Models.FleetSegment fleetsegment)
        {
            StaticKeyResolver resolver = new StaticKeyResolver(this.m_mappings);
            return resolver.FindAllMatches(MultiLevelKey.FromObject(fleetsegment), KeyDomain.FleetSegment).First();
        }

        public (int index, int score, float propertion) ResolveFleet(string gearcode, string marketcode)
        {
            MultiLevelKey key = new();
            key.Fields.Add("GearCode", gearcode);
            key.Fields.Add("MarketCode", marketcode);

            StaticKeyResolver resolver = new StaticKeyResolver(this.m_mappings);
            return resolver.FindAllMatches(key, KeyDomain.FleetSegment).First();
        }

        public MultiLevelKey? Find(int iIndex, KeyDomain domain)
        {
            StaticKeyResolver resolver = new StaticKeyResolver(this.m_mappings);
            return resolver.GetKey(iIndex, domain);
        }

        #endregion // Consulting the registry

        #region Persistence

        public bool Load(cCore core)
        {
            m_mappings.Clear();
            m_fishedGroups.Clear();
            m_externalFleets.Clear();

            string cfgtext = GetConfigBucket(core).Remark;

            m_mappings.Add(MultiLevelKey.Parse("species=SAR", KeyDomain.Species, 5));
            m_mappings.Add(MultiLevelKey.Parse("species=HKE; stage=juvenile", KeyDomain.Species, 6));
            m_mappings.Add(MultiLevelKey.Parse("species=HKE; stage=adult", KeyDomain.Species, 7));

            m_mappings.Add(MultiLevelKey.Parse("gearcode=LLN; flag=ES", KeyDomain.FleetSegment, 2));

            m_mappings.Add(MultiLevelKey.Parse("gearcode=LLN; marketcode=ES", KeyDomain.Market, 2));

            for (int iGroup = 1; iGroup <= core.nGroups; iGroup++)
                if (core.get_EcopathGroupInputs(iGroup).IsFished)
                    m_fishedGroups.Add(iGroup);

            return true;
        }

        public bool Save(cCore core)
        {
            return true;
        }

        private cAuxiliaryData GetConfigBucket(cCore core)
        {
            cEcospaceModelParameters parms = core.EcospaceModelParameters;
            return core.get_AuxillaryData("SURIMI_link_" + parms.DBID);
        }

        #endregion // Persistence

        #region Mappings

        /// <summary>
        /// Get all item mappings for a specific domain
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public IEnumerable<MultiLevelKey> Mappings(KeyDomain domain)
        {
            foreach (var kvp in m_mappings.Where(n => n.Domain == domain))
               yield return kvp;
        }

         /// <summary>
        /// Set whether fishing by a given gear fleet is managed outside the EwE software.
        /// </summary>
        public void SetExternalFleet(int iFleet, bool isExternal)
        {
            if (iFleet <= 0) return;

            // Add or remove without needing to check IsExternalFleet first. Also, mind the inconspicious black hole of oblivion '_'
            _ = isExternal ? m_externalFleets.Add(iFleet) : m_externalFleets.Remove(iFleet);
        }

        /// <summary>
        /// Get whether fishing by a given gear fleet is managed outside the EwE software.
        /// </summary>
        public bool IsExternalFleet(int iFleet) => iFleet > 0 && m_externalFleets.Contains(iFleet);

        /// <summary>
        /// Get all externally managed fleets.
        /// </summary>
        public int[] ExternalFleets() => m_externalFleets.ToArray();

        /// <summary>
        /// Get all fished groups.
        /// </summary>
        public int[] FishedGroups() => m_fishedGroups.ToArray();

        #endregion // Mappings
    }
}
