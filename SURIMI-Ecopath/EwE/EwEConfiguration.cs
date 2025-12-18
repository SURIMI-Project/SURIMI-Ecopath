using Ecopath.EwE.Wrapper;
using Eii.ControlledVocabularies.Common;
using Eii.ControlledVocabularies.Context;
using Eii.ControlledVocabularies.Core;
using Eii.ControlledVocabularies.Descriptors;
using Eii.ControlledVocabularies.Registries;
using Eii.ControlledVocabularies.Resolve;
using Eii.ControlledVocabularies.Vocabularies;
using Eii.ControlledVocabularies.Vocabularies.Country;
using Eii.ControlledVocabularies.Vocabularies.Gear;
using Eii.ControlledVocabularies.Vocabularies.LifeStage;
using Eii.ControlledVocabularies.Vocabularies.Species;
using EwECore;
using EwECore.Auxiliary;
using SURIMI.Datamodel;

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
        private readonly IKeyFieldDescriptorRegistry m_keyFieldDescriptorRegistry;
        private readonly VocabularyRegistry m_vocabularies = new();
        private readonly ASFISSpeciesCodeVocabulary m_asfisVocabulary;
        private readonly ISSCFGGearCodeVocabulary m_iSSCFGGearCodeVocabulary;
        private readonly ISO3166CountryCodeVocabulary m_iSO3166CountryCodeVocabulary;
        private readonly SURIMILifestageVocabulary m_SURIMILifestageVocabulary;
        private readonly IMultiLevelKeyFactory m_multiLevelKeyFactory;
        private readonly ILogger<EwEConfiguration> m_logger;

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

        public EwEConfiguration(ASFISSpeciesCodeVocabulary asfisVocabulary, ISSCFGGearCodeVocabulary iSSCFGGearCodeVocabulary, ISO3166CountryCodeVocabulary iSO3166CountryCodeVocabulary, SURIMILifestageVocabulary sURIMILifestageVocabulary, IKeyFieldDescriptorRegistry keyFieldDescriptorRegistry, IMultiLevelKeyFactory multiLevelKeyFactory, ILogger<EwEConfiguration> logger)
        {
            m_asfisVocabulary = asfisVocabulary;
            m_iSSCFGGearCodeVocabulary = iSSCFGGearCodeVocabulary;
            m_iSO3166CountryCodeVocabulary = iSO3166CountryCodeVocabulary;
            m_SURIMILifestageVocabulary = sURIMILifestageVocabulary;
            m_keyFieldDescriptorRegistry = keyFieldDescriptorRegistry;
            m_multiLevelKeyFactory = multiLevelKeyFactory;
            m_logger = logger;

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
            var context = new ModelContext(reg, m_multiLevelKeyFactory);

            // ToDo: make this real once the model has loaded
            context.SetModelName("EwE Demo");
            context.SetAreaName("NE Atlantic");
            context.SetBoundingBox(35.0, -25.0, 70.0, 20.0);
            context.SetYears(1990, 2020);
            context.SetTimestamp(DateTime.UtcNow);

            // Register the different species fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.SpeciesCode, KeyDomain.Species, KeyPurpose.Species, FieldKind.Code, true, 10));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Lifestage, KeyDomain.Species, KeyPurpose.Lifestage, FieldKind.Label, false, 3));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Length, KeyDomain.Species, KeyPurpose.Length, FieldKind.Label, false, 3));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(SpeciesFields.Age, KeyDomain.Species, KeyPurpose.Age, FieldKind.Label, false, 3));

            // Register the different gear fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(FishingFields.GearCode, KeyDomain.FleetSegment, KeyPurpose.Gear, FieldKind.Code, true, 10));
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(FishingFields.Flag, KeyDomain.FleetSegment, KeyPurpose.Country, FieldKind.Code, false, 3));

            // Register the different market fields that the application may be interested in
            m_keyFieldDescriptorRegistry.Register(new KeyFieldDescriptor(MarketFields.MarketCode, KeyDomain.FleetSegment, KeyPurpose.Market, FieldKind.Code, true, 10));

            // Register available look-up vocabularies
            m_vocabularies.Register(m_asfisVocabulary);
            m_vocabularies.Register(m_SURIMILifestageVocabulary);
            m_vocabularies.Register(m_iSSCFGGearCodeVocabulary);
            m_vocabularies.Register(m_iSO3166CountryCodeVocabulary);
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

        public bool Load(IEwECore core, SurimiConfiguration surimiConfiguration)
        {
            m_mappings.Clear();
            m_fishedGroups.Clear();
            m_externalFleets.Clear();

            string cfgtext = GetConfigBucket(core).Remark;

            // Register fleet segments as gear + flag pairs to match fleet + flag fishing
            m_mappings.Add(new EwEMapping("gearcode=TB; flag=ESP", KeyDomain.FleetSegment, 1, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=ESP", KeyDomain.FleetSegment, 2, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=LL; flag=ESP", KeyDomain.FleetSegment, 3, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; flag=ESP", KeyDomain.FleetSegment, 4, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=ESP", KeyDomain.FleetSegment, 2, m_keyFieldDescriptorRegistry));

            m_mappings.Add(new EwEMapping("gearcode=TB; flag=FRA", KeyDomain.FleetSegment, 5, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=TM; flag=FRA", KeyDomain.FleetSegment, 6, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=PS; flag=FRA", KeyDomain.FleetSegment, 7, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; flag=FRA", KeyDomain.FleetSegment, 8 , m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Recreational; flag=FRA", KeyDomain.FleetSegment, 9, m_keyFieldDescriptorRegistry));

            // Register fleet segments as gear + market code pairs to match fleet > market deliveries
            m_mappings.Add(new EwEMapping("gearcode=TB; marketcode=ESP", KeyDomain.Market, 1, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ESP", KeyDomain.Market, 2, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=LL; marketcode=ESP", KeyDomain.Market, 3, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; marketcode=ESP", KeyDomain.Market, 4, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=ESP", KeyDomain.Market, 5, m_keyFieldDescriptorRegistry));

            m_mappings.Add(new EwEMapping("gearcode=TB; marketcode=FRA", KeyDomain.Market, 5, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=TM; marketcode=FRA", KeyDomain.Market, 6, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=PS; marketcode=FRA", KeyDomain.Market, 7, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Artisanal; marketcode=FRA", KeyDomain.Market, 8, m_keyFieldDescriptorRegistry));
            m_mappings.Add(new EwEMapping("gearcode=EwE:Recreational; marketcode=FRA", KeyDomain.Market, 9, m_keyFieldDescriptorRegistry));

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
            MultiLevelKey key = m_multiLevelKeyFactory.FromPairs([(SpeciesFields.SpeciesCode, speciescode)], KeyDomain.Species, m_keyFieldDescriptorRegistry);
            return ResolveGroups(key);
        }

        public IEnumerable<EwEMappingMatch> ResolveGroups(SURIMI.Datamodel.Species species)
        {
            return ResolveGroups(m_multiLevelKeyFactory.FromObject(species, KeyDomain.Species, m_keyFieldDescriptorRegistry));
        }

        public IEnumerable<EwEMappingMatch> ResolveGroups(MultiLevelKey key)
        {
            if (key.Domain != KeyDomain.Species)
                yield break;

            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptorRegistry.GetAll(KeyDomain.Species));
            foreach (var match in resolver.FindAllMatches(key))
                yield return new EwEMappingMatch((EwEMapping)match.MatchedKey, match.Score);
        }

        public IEnumerable<EwEMappingMatch> ResolveFleets(SURIMI.Datamodel.FleetSegment fleetsegment)
        {
            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptorRegistry.GetAll(KeyDomain.FleetSegment));
            foreach (var match in resolver.FindAllMatches(m_multiLevelKeyFactory.FromObject(fleetsegment, KeyDomain.FleetSegment, m_keyFieldDescriptorRegistry)))
                yield return new EwEMappingMatch((EwEMapping)match.MatchedKey, match.Score);
        }

        public IEnumerable<EwEMappingMatch> ResolveMarkets(string gearcode, string marketcode)
        {
            MultiLevelKey key = m_multiLevelKeyFactory.FromPairs([(FishingFields.GearCode, gearcode), (MarketFields.MarketCode, marketcode)], KeyDomain.Market, m_keyFieldDescriptorRegistry);
            var resolver = new StrategyKeyResolver(this.m_mappings, this.m_keyFieldDescriptorRegistry.GetAll(KeyDomain.Market));
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
        /// Get all fished groups in the Ecopath model. Note that some of these groups
        /// may only be discarded, and some groups may not have known species attached.
        /// Not all fished groups may be accessible to the SURIMI framework.
        /// </summary>
        public int[] FishedGroups => m_fishedGroups.ToArray();

        #endregion // Mappings

        #region Smarts 


        /// <summary>
        /// Load the species key -> FN group mappings from the model. 
        /// </summary>
        /// <param name="core"></param>
        /// <remarks>
        /// The species mappings are intended for all fisheries-related accounting in SURIMI, and
        /// only applies to fished groups. Species that are not fished, or fished functional groups 
        /// without taxonomic records / species attached, are not registered here.
        /// </remarks>
        private void ReadSpeciesMappings(IEwECore core, bool writeCSV = false)
        {
            /// Dirty hack to test if the species is in the EwE_functional-group_species.csv
            /// TODO : remove when the Initialise test message is implemented
            string[] surimuCodes = ["MTS", "NEP", "ARS", "FAM", "LKO", "LKT", "SKM", "GRQ", "IOD", "OLV", "BOY", "EOI", "OCC", "OUL", "OUM", "SQM", "OQT", "HQB", "SQE", "SLM", "MUT", "WHB", "ANK", "MON", "HKE", "POD", "COE", "COE", "CIL", "LDB", "BLL", "LDV", "SRJ", "SBA", "PAC", "BRF", "SFS", "TSU", "GLI", "SPF", "SPC", "BOG", "SYC", "SCK", "SHO", "JDP", "RJC", "JRS", "ANE", "PIL", "SAA", /*"SPR",*/ "HMM", "JAA", "HOM", "MAC", "MAZ", "SWO", "BFT", "BON", "ALB", /*"BSK", "RMM", "MOX", */"BSH", /*"TTL", "CDI", "FPA", "DAZ", "LVH", "VCW", "LOW", "DTR", "DTY", "DKH", "CVV", "MVB", "ISY", "UIM", "UYE", "TVA", "PIW", "DRR", "SPW", "DST", "DBO", "FIW"*/];

            // The name of the vocabulary is implied here, but should be read from the species code
            IControlledVocabulary? vocSpecies = m_vocabularies.Get("asfis");
            IControlledVocabulary? vocLifeStage = m_vocabularies.Get("surimi.lifestage");

            if (vocSpecies == null || vocLifeStage == null) return;

            StreamWriter? csvWriter = null;
            if (writeCSV)
            {
                string csvFilePath = Path.GetFullPath(@".\EwE_functional-group_species.csv");

                csvWriter = new StreamWriter(csvFilePath);
                csvWriter.WriteLine("EwE_Group_ID,EwE_Group_Name,Taxon_ID,Common_Name,Genus,Species,FAO_Code,Lifestage_Code");
                m_logger.LogInformation("Writing species CSV file to {csvFilePath}", csvFilePath);
            }

            for (int iTaxa = 1; iTaxa <= core.nTaxon; iTaxa++)
            {
                cTaxon taxon = core.get_Taxon(iTaxa);
                var code = taxon.CodeFAO;
                if (String.IsNullOrEmpty(code))
                {
                    // Make robust to encoding imperfections
                    string common = taxon.Common;
                    if (string.IsNullOrEmpty(common))
                        common = taxon.Genus + " " + taxon.Species;
                    code = vocSpecies.FindCode(common);
                }

                // Skip species that could not be resolved to a code in the vocabulary
                if (string.IsNullOrEmpty(code))
                {
                    m_logger.LogInformation("Skipping taxon '{taxon}'; code not found in vocabulary {vocSpecies.VocabularyName}", taxon.Common, vocSpecies.VocabularyName);
                    continue;
                }

                 // TODO This is a dirty test to skip the species that are not in the EwE_functional-group_species.csv
                // It should be replaced by the Initialise test message
                if (!surimuCodes.Contains(code) && surimuCodes.Length > 0)
                {
                    m_logger.LogInformation("Skipping taxon '{taxon}'; code '{code}' not in EwE_functional-group_species.csv", taxon.Common, code);
                    continue;
                }

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

                            var key = new EwEMapping("", KeyDomain.Species, iGroup, m_keyFieldDescriptorRegistry, 1);
                            key.SetField(SpeciesFields.SpeciesCode, vocSpecies.VocabularyName + ":" + code, m_keyFieldDescriptorRegistry);

                            // Try to infer the stage from the group name
                            string ls = vocLifeStage.FindCode(grp.Name, 50);
                            key.SetField(SpeciesFields.Lifestage, vocLifeStage.VocabularyName + ":" + ls, m_keyFieldDescriptorRegistry);

                            this.m_mappings.Add(key);

                            if (csvWriter != null)
                            {
                                //csvWriter.WriteLine("EwE_Group_ID,EwE_Group_Name,Taxon_ID,Common_Name,Genus,Species,FAO_Code,Lifestage_Code");
                                csvWriter.WriteLine($"{grp.DBID},\"{grp.Name}\",{taxon.DBID},\"{taxon.Name}\",\"{taxon.Genus}\",\"{taxon.Species}\",{code},{ls}");
                            }
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
                        var key = new EwEMapping("", KeyDomain.Species, taxon.iGroup, m_keyFieldDescriptorRegistry, taxon.PropB / 100);
                        key.SetField(SpeciesFields.SpeciesCode, vocSpecies.VocabularyName + ":" + code, m_keyFieldDescriptorRegistry);

                        this.m_mappings.Add(key);

                        if (csvWriter != null)
                        {
                            //csvWriter.WriteLine("EwE_Group_ID,EwE_Group_Name,Taxon_ID,Common_Name,Genus,Species,FAO_Code,Lifestage_Code");
                            csvWriter.WriteLine($"{grp.DBID},\"{grp.Name}\",{taxon.DBID},\"{taxon.Name}\",\"{taxon.Genus}\",\"{taxon.Species}\",{code},");
                        }
                    }
                    else
                    {
                        // Not fished: do not register species for data exchange
                    }
                }
            }
            if (csvWriter != null)
            {
                csvWriter.Flush();
                csvWriter.Close();
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
