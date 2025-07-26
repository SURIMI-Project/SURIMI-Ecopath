using EwECore;
using EwECore.Auxiliary;

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
        private readonly List<EwEMapping> m_mappings = new();
        private readonly KeyFieldDescriptorRegistry m_keyFieldDescriptors = new ();
        private readonly VocabularyRegistry m_vocabularies = new();

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
            StartYear = 2013; // The year that the simulation starts
            MaxRunYears = 25;

#if DEBUG
            // Just to speed up the process of testing :P
            SpinupYears = 1;
            StartYear = 2001;
#endif

            m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.SpeciesCode, true, 10));
            m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.Stage, false, 3));
            m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.Length, false, 3));
            m_keyFieldDescriptors.Register(KeyDomain.Species, new KeyFieldDescriptor(SpeciesFields.Age, false, 3));

            m_keyFieldDescriptors.Register(KeyDomain.FleetSegment, new KeyFieldDescriptor(FishingFields.GearCode, true, 10));
            m_keyFieldDescriptors.Register(KeyDomain.FleetSegment, new KeyFieldDescriptor(FishingFields.Flag, false, 3));

            m_keyFieldDescriptors.Register(KeyDomain.FleetSegment, new KeyFieldDescriptor(MarketFields.MarketCode, true, 10));

            m_vocabularies.Register(new ASFISSpeciesVocabulary());
            m_vocabularies.Register(new DwCLifestageVocabulary());
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

        #region Persistence

        public bool Load(cCore core)
        {
            m_mappings.Clear();
            m_fishedGroups.Clear();
            m_externalFleets.Clear();

            string cfgtext = GetConfigBucket(core).Remark;

            m_mappings.Add(new EwEMapping("gearcode=TB; flag=ESP", KeyDomain.FleetSegment, 1));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=ESP", KeyDomain.FleetSegment, 2));
            m_mappings.Add(new EwEMapping("gearcode=LL; flag=ESP", KeyDomain.FleetSegment, 3));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; flag=ESP", KeyDomain.FleetSegment, 4));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=ESP", KeyDomain.FleetSegment, 2));

            m_mappings.Add(new EwEMapping("gearcode=TB; flag=FRA", KeyDomain.FleetSegment, 5));
            m_mappings.Add(new EwEMapping("gearcode=TM; flag=FRA", KeyDomain.FleetSegment, 6));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=FRA", KeyDomain.FleetSegment, 7));
            m_mappings.Add(new EwEMapping("gearcode==EwE:Artisanal; flag=FRA", KeyDomain.FleetSegment, 8));
            m_mappings.Add(new EwEMapping("gearcode==EwE:Recreational; flag=FRA", KeyDomain.FleetSegment, 9));

            m_mappings.Add(new EwEMapping("gearcode=TB; marketcode=ESP", KeyDomain.Market, 1));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ESP", KeyDomain.Market, 2));
            m_mappings.Add(new EwEMapping("gearcode=LL; marketcode=ESP", KeyDomain.Market, 3));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; marketcode=ESP", KeyDomain.Market, 4));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ESP", KeyDomain.Market, 5));

            for (int iGroup = 1; iGroup <= core.nGroups; iGroup++)
                if (core.get_EcopathGroupInputs(iGroup).IsFished)
                    m_fishedGroups.Add(iGroup);

            this.ReadSpeciesMappings(core);

            //m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:juvenile", KeyDomain.Species, 22));       // Mullet (j)
            //m_mappings.Add(new EwEMapping("speciescode=ASFIS:MUR; stage=dwc:adult", KeyDomain.Species, 23));          // Mullet (a)
            //m_mappings.Add(new EwEMapping("speciescode=ASFIS:HKE; stage=dwc:juvenile", KeyDomain.Species, 26));       // European Hake (j)
            //m_mappings.Add(new EwEMapping("speciescode=ASFIS:HKE; stage=dwc:adult", KeyDomain.Species, 27));          // European Hake (a)
            //m_mappings.Add(new EwEMapping("speciescode=ASFIS:ANE; stage=dwc:juvenile", KeyDomain.Species, 39));       // Anchovy (j)
            //m_mappings.Add(new EwEMapping("speciescode=ASFIS:ANE; stage=dwc:adult", KeyDomain.Species, 40));          // Anchovy (a)
            //m_mappings.Add(new EwEMapping("speciescode=ASFIS:PIL; stage=dwc:juvenile", KeyDomain.Species, 41));       // Sardine (j)
            //m_mappings.Add(new EwEMapping("speciescode=ASFIS:PIL; stage=dwc:adult", KeyDomain.Species, 42));          // Sardine (a)

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

        #region Consulting the registry

        public IEnumerable<EwEMappingMatch> ResolveGroups(string speciescode)
        {
            MultiLevelKey key = new();
            key.SetField("SpeciesCode", speciescode);
            return ResolveGroups(key);
        }

        public IEnumerable<EwEMappingMatch> ResolveGroups(Ecopath.Models.Species species)
        {
            return ResolveGroups(MultiLevelKey.FromObject(species));
        }

        public IEnumerable<EwEMappingMatch> ResolveGroups(MultiLevelKey key)
        {
            StaticKeyResolver resolver = new(this.m_mappings, this.m_keyFieldDescriptors.Get(KeyDomain.Species));
            foreach (var match in resolver.FindAllMatches(key, KeyDomain.Species))
                yield return new EwEMappingMatch((EwEMapping)match.Key, match.Score);
        }

        public IEnumerable<EwEMappingMatch> ResolveFleets(Ecopath.Models.FleetSegment fleetsegment)
        {
            StaticKeyResolver resolver = new StaticKeyResolver(this.m_mappings, this.m_keyFieldDescriptors.Get(KeyDomain.FleetSegment));
            foreach (var match in resolver.FindAllMatches(MultiLevelKey.FromObject(fleetsegment), KeyDomain.FleetSegment))
                yield return new EwEMappingMatch((EwEMapping)match.Key, match.Score);
        }

        public IEnumerable<EwEMappingMatch> ResolveMarkets(string gearcode, string marketcode)
        {
            MultiLevelKey key = new();
            key.SetField("GearCode", gearcode);
            key.SetField("MarketCode", marketcode);

            StaticKeyResolver resolver = new StaticKeyResolver(this.m_mappings, this.m_keyFieldDescriptors.Get(KeyDomain.Market));
            foreach (var match in resolver.FindAllMatches(key, KeyDomain.Market))
                yield return new EwEMappingMatch((EwEMapping)match.Key, match.Score);
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
        private void ReadSpeciesMappings(cCore core)
        {
            ISpeciesCodeVocabulary vocSpecies = m_vocabularies.Get<ISpeciesCodeVocabulary>("asfis");
            ILifestageVocabulary? vocLifeStage = m_vocabularies.Get<ILifestageVocabulary>("dwc.lifestage");

            for (int iTaxa = 1; iTaxa <= core.nTaxon; iTaxa++) 
            {
                cTaxon taxon = core.get_Taxon(iTaxa);
                var code = taxon.CodeFAO;
                if (String.IsNullOrEmpty(code))
                    code = vocSpecies.SpeciesToCode(taxon.Common);

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

                                var key = new EwEMapping();
                                key.Domain = KeyDomain.Species;
                                key.SetField(SpeciesFields.SpeciesCode, vocSpecies.VocabularyName + ":" + code);

                                // Try to infer the stage from the group name
                                key.SetField(SpeciesFields.Lifestage, vocLifeStage.VocabularyName + ":" + vocLifeStage.MatchLifestage(grp.Name).match);
                                key.Index = iGroup;
                                key.Proportion = 1;

                                this.m_mappings.Add(key);
                            }                        
                        }
                    }
                    else
                    {
                        if (this.FishedGroups.Contains(taxon.iGroup))
                        {
                            var key = new EwEMapping();
                            key.Domain = KeyDomain.Species;
                            key.SetField(SpeciesFields.SpeciesCode, vocSpecies.VocabularyName + ":" + code);
                            key.Index = taxon.iGroup;
                            key.Proportion = taxon.PropB / 100;

                            this.m_mappings.Add(key);
                        }
                    }
                }
            }
        }

        #endregion // Smarts
    }
}
