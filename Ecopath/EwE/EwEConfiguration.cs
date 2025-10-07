using Ecopath.EwE.Wrapper;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Context;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Match;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Resolve;
using Eii.ControlledVocabularies.Utils;
using Eii.ControlledVocabularies.Vocabularies;
using Eii.ControlledVocabularies.Vocabularies.Country;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;
using EwECore;
using EwECore.Auxiliary;

namespace Ecopath.EwE
{

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
        private readonly List<EwEMapping> m_mappings = new();
        private readonly KeyFieldDescriptorRegistry m_keyFieldDescriptors;
        private readonly VocabularyRegistry m_vocabularies = new();

        // The EwE indices of externally managed fleets.
        private readonly HashSet<int> m_externalFleets = new();
        // The EwE indices of fished groups
        private readonly HashSet<int> m_fishedGroups = new();

        #endregion // Data

        private class EwEMappingComparer : IComparer<EwEMapping>
        {
            public int Compare(EwEMapping? x, EwEMapping? y)
            {
                if (x == null && y == null)
                    return 0;
                else if (x == null)
                    return -1;
                else if (y == null)
                    return 1;

                if (x.Index < y.Index) return -1;
                if (x.Index > y.Index) return 1;
                return string.Compare(x.ToString(), y.ToString());
            }
        }

        public EwEConfiguration()
        {
            ModelName = @"Includes/GSA0607EwENBS.eiixml";
            EcosimScenario = 1;
            EcosimTimeSeries = 0;
            EcospaceScenario = 1;
            SpinupYears = 10;
            StartYear = 2013; // The year that the simulation starts
            MaxRunYears = 25;

#if DEBUG
            // Just to speed up the process of testing :P
            SpinupYears = 1;
            StartYear = 2001;
#endif
            var reg = ModelContextDescriptorRegistry.Create();
            var context = new ModelContext(reg);

            // ToDo: make this real once the model has loaded
            context.SetModelName("EwE Demo");
            context.SetAreaName("NE Atlantic");
            context.SetBoundingBox(35.0, -25.0, 70.0, 20.0);
            context.SetYears(1990, 2020);
            context.SetTimestamp(DateTime.UtcNow);
            GlobalServiceLocator.Replace(context);

            m_keyFieldDescriptors = GlobalServiceLocator.Get<KeyFieldDescriptorRegistry>()!;

            // Register the different species fields that the application may be interested in
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.SpeciesCode, KeyDomain.Species, KeyPurpose.Species, FieldKind.Code, true, 10));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Stage, KeyDomain.Species, KeyPurpose.Lifestage, FieldKind.Label, false, 3));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Length, KeyDomain.Species, KeyPurpose.Length, FieldKind.Label, false, 3));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(SpeciesFields.Age, KeyDomain.Species, KeyPurpose.Age, FieldKind.Label, false, 3));

            // Register the different gear fields that the application may be interested in
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(FishingFields.GearCode, KeyDomain.FleetSegment, KeyPurpose.Gear, FieldKind.Code, true, 10));
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(FishingFields.Flag, KeyDomain.FleetSegment, KeyPurpose.Country, FieldKind.Code, false, 3));

            // Register the different market fields that the application may be interested in
            m_keyFieldDescriptors.Register(new KeyFieldDescriptor(MarketFields.MarketCode, KeyDomain.FleetSegment, KeyPurpose.Market, FieldKind.Code, true, 10));

            // Register available look-up vocabularies
            m_vocabularies.Register(new ASFISSpeciesCodeVocabulary());
            m_vocabularies.Register(new SURIMILifestageVocabulary());
            m_vocabularies.Register(new ISSCFGGearCodeVocabulary());
            m_vocabularies.Register(new ISO3166CountryCodeVocabulary());
        }


        public string ModelName { get; set; } = "";
        public int EcosimScenario { get; set; } = 0;
        public int EcosimTimeSeries { get; set; } = 0;
        public int EcospaceScenario { get; set; } = 0;
        public int SpinupYears { get; set; } = 0;
        public int StartYear { get; set; } = 0;
        public int MaxRunYears { get; set; } = 400;

        /// <summary>
        /// Get/set whether MultiLevelKeys sent out to SURIMI should include vocabularies (e.g., "stage=dwc.lifestage:juvenile")
        /// </summary>
        public bool IncludeVocabularies { get; set; } = false;

        #region Persistence

        public bool Load(IEwECore core)
        {
            m_mappings.Clear();
            m_fishedGroups.Clear();
            m_externalFleets.Clear();

            string cfgtext = GetConfigBucket(core).Remark;

            // Register fleet segments as gear + flag pairs to match fleet + flag fishing
            m_mappings.Add(new EwEMapping("gearcode=TB; flag=ESP", KeyDomain.FleetSegment, 1));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=ESP", KeyDomain.FleetSegment, 2));
            m_mappings.Add(new EwEMapping("gearcode=LL; flag=ESP", KeyDomain.FleetSegment, 3));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; flag=ESP", KeyDomain.FleetSegment, 4));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=ESP", KeyDomain.FleetSegment, 2));

            m_mappings.Add(new EwEMapping("gearcode=TB; flag=FRA", KeyDomain.FleetSegment, 5));
            m_mappings.Add(new EwEMapping("gearcode=TM; flag=FRA", KeyDomain.FleetSegment, 6));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=FRA", KeyDomain.FleetSegment, 7));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; flag=FRA", KeyDomain.FleetSegment, 8));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Recreational; flag=FRA", KeyDomain.FleetSegment, 9));

            // Register fleet segments as gear + market code pairs to match fleet > market deliveries
            m_mappings.Add(new EwEMapping("gearcode=TB; marketcode=ESP", KeyDomain.Market, 1));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ESP", KeyDomain.Market, 2));
            m_mappings.Add(new EwEMapping("gearcode=LL; marketcode=ESP", KeyDomain.Market, 3));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; marketcode=ESP", KeyDomain.Market, 4));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ESP", KeyDomain.Market, 5));

            m_mappings.Add(new EwEMapping("gearcode=TB; marketcode=FRA", KeyDomain.Market, 5));
            m_mappings.Add(new EwEMapping("gearcode=TM; marketcode=FRA", KeyDomain.Market, 6));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=FRA", KeyDomain.Market, 7));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; marketcode=FRA", KeyDomain.Market, 8));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Recreational; marketcode=FRA", KeyDomain.Market, 9));

            for (int iGroup = 1; iGroup <= core.nGroups; iGroup++)
                if (core.get_EcopathGroupInputs(iGroup).IsFished)
                    m_fishedGroups.Add(iGroup);

            this.ReadSpeciesMappings(core);
            this.ReadFleetMappings();

            m_mappings.Sort(new EwEMappingComparer());

            return true;
        }

        public bool Save(cCore core)
        {
            return true;
        }

        private cAuxiliaryData GetConfigBucket(IEwECore core)
        {
            cEcospaceModelParameters parms = core.EcospaceModelParameters;
            return core.AuxillaryData("SURIMI_link_" + parms.DBID);
        }

        #endregion // Persistence

        #region Consulting the registry

        public IEnumerable<EwEMappingMatch> ResolveGroups(string speciescode)
        {
            MultiLevelKey key = MultiLevelKey.FromPairs([(SpeciesFields.SpeciesCode, speciescode)], KeyDomain.Species);
            return ResolveGroups(key);
        }

        public IEnumerable<EwEMappingMatch> ResolveGroups(Ecopath.Models.Species species)
        {
            return ResolveGroups(MultiLevelKey.FromObject(species, KeyDomain.Species));
        }

        public IEnumerable<EwEMappingMatch> ResolveGroups(MultiLevelKey key)
        {
            if (key.Domain != KeyDomain.Species)
                yield break;

            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptors.GetAll(KeyDomain.Species));
            foreach (var match in resolver.FindAllMatches(key))
                yield return new EwEMappingMatch((EwEMapping)match.MatchedKey, match.Score);
        }

        public IEnumerable<EwEMappingMatch> ResolveFleets(Ecopath.Models.FleetSegment fleetsegment)
        {
            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptors.GetAll(KeyDomain.FleetSegment));
            foreach (var match in resolver.FindAllMatches(MultiLevelKey.FromObject(fleetsegment, KeyDomain.FleetSegment)))
                yield return new EwEMappingMatch((EwEMapping)match.MatchedKey, match.Score);
        }

        public IEnumerable<EwEMappingMatch> ResolveMarkets(string gearcode, string marketcode)
        {
            MultiLevelKey key = MultiLevelKey.FromPairs([(FishingFields.GearCode, gearcode), (MarketFields.MarketCode, marketcode)], KeyDomain.Market);
            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptors.GetAll(KeyDomain.Market));
            foreach (var match in resolver.FindAllMatches(key))
                yield return new EwEMappingMatch((EwEMapping)match.MatchedKey, match.Score);
        }

        public EwEMapping? Find(int iIndex, KeyDomain domain)
        {
            return m_mappings.FirstOrDefault(m => m.Index == iIndex && m.Domain == domain);
        }

        #endregion // Consulting the registry

        #region Mappings

        /// <summary>
        /// Get all item mappings for a specific domain
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        public IEnumerable<EwEMapping> Mappings(KeyDomain domain)
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
        public int[] FishedGroups => m_fishedGroups.ToArray();

        #endregion // Mappings

        #region Smarts 

        /// <summary>
        /// Load the species -> FAO code mappings from the model
        /// </summary>
        /// <param name="core"></param>
        private void ReadSpeciesMappings(IEwECore core)
        {
            // The name of the vocabulary is implied here, but should be read from the species code
            IControlledVocabulary? vocSpecies = m_vocabularies.Get("asfis");
            IControlledVocabulary? vocLifeStage = m_vocabularies.Get("surimi.lifestage");

            if (vocSpecies == null ||  vocLifeStage == null) return;

            GenericVocabularyMatcher m = new();

            for (int iTaxa = 1; iTaxa <= core.nTaxon; iTaxa++)
            {
                cTaxon taxon = core.get_Taxon(iTaxa);
                var code = taxon.CodeFAO;
                if (String.IsNullOrEmpty(code))
                    code = vocSpecies.FindCode(taxon.Common);

                if (!string.IsNullOrEmpty(code))
                {
                    // Taxon refers to a multi-stanza configuration?
                    if (taxon.iStanza > 0)
                    {
                        // #Yes: iterate over life stages
                        // Bug workaround - taxon.iStanza is one based, but core accessor is zero based. Ugh
                        cStanzaGroup stz = core.get_StanzaGroups(taxon.iStanza - 1);
                        for (int iLS = 1; iLS <= stz.nLifeStages; iLS++)
                        {
                            // Is given life stage fished?
                            int iGroup = stz.get_iGroups(iLS);
                            if (this.FishedGroups.Contains(iGroup))
                            {
                                // #Yes: add life stage to mappings
                                cEcoPathGroupInput grp = core.get_EcopathGroupInputs(iGroup);

                                var key = new EwEMapping("", KeyDomain.Species, iGroup, 1);
                                key.SetField(SpeciesFields.SpeciesCode, vocSpecies.VocabularyName + ":" + code);

                                // Try to infer the stage from the group name
                                string ls = vocLifeStage.FindCode(grp.Name);
                                key.SetField(SpeciesFields.Lifestage, vocLifeStage.VocabularyName + ":" + ls);

                                this.m_mappings.Add(key);
                            }
                            else
                            {
                                // Not fished: do not register species for data exchange
                            }
                        }
                    }
                    else
                    {
                        cEcoPathGroupInput grp = core.get_EcopathGroupInputs(taxon.iGroup);
                        if (grp.iStanza > 0)
                        {
                            Console.WriteLine("EwE Config error: regular taxon {0} attached to stanza group {1}", taxon.DBID, taxon.iGroup);
                            continue;   
                        }

                        if (this.FishedGroups.Contains(taxon.iGroup))
                        {
                            var key = new EwEMapping("", KeyDomain.Species, taxon.iGroup, taxon.PropB / 100);
                            key.SetField(SpeciesFields.SpeciesCode, vocSpecies.VocabularyName + ":" + code);

                            this.m_mappings.Add(key);
                        }
                        else
                        {
                            // Not fished: do not register species for data exchange
                        }
                    }
                }
            }
        }
        
        private void ReadFleetMappings()
        {
            // The name of the vocabulary is implied here, but should be read from the fields
            IControlledVocabulary vocGear = m_vocabularies.Get("ISSCFG")!;
            IControlledVocabulary vocCountry = m_vocabularies.Get("ISO-3166")!;

        }
        #endregion // Smarts
    }
}
